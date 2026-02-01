using Apolon.Core.Migrations.Models;

namespace Apolon.Core.Migrations.Utils;

/// <summary>
///     Sorts migration operations to respect dependency order, particularly for foreign key constraints.
///     Ensures tables are created before they are referenced, and constraints are added in the correct order.
/// </summary>
internal static class MigrationOperationSorter
{
    /// <summary>
    ///     Sorts migration operations to ensure safe execution order.
    /// </summary>
    /// <param name="operations">The unsorted operations.</param>
    /// <returns>Operations sorted by dependency order.</returns>
    public static IReadOnlyList<MigrationOperation> Sort(IReadOnlyList<MigrationOperation> operations)
    {
        var sorted = new List<MigrationOperation>();

        // Phase 1: Create schemas (must come first)
        var schemaOps = operations
            .Where(op => op.Type == MigrationOperationType.CreateSchema)
            .DistinctBy(op => op.Schema)
            .ToList();
        sorted.AddRange(schemaOps);

        // Phase 2: Create tables (topologically sorted by FK dependencies)
        var createTableOps = operations
            .Where(op => op.Type == MigrationOperationType.CreateTable)
            .ToList();

        var sortedTables = TopologicalSortTables(createTableOps, operations);
        sorted.AddRange(sortedTables);

        // Phase 3: Add columns (grouped by table, respecting table creation order)
        var addColumnOps = operations
            .Where(op => op.Type == MigrationOperationType.AddColumn)
            .ToList();

        var sortedColumns = SortColumnsByTableOrder(addColumnOps, sortedTables);
        sorted.AddRange(sortedColumns);

        // Phase 4: Alter column operations (type, nullability, defaults)
        sorted.AddRange(operations.Where(op => op.Type == MigrationOperationType.AlterColumnType));
        sorted.AddRange(operations.Where(op => op.Type == MigrationOperationType.AlterNullability));
        sorted.AddRange(operations.Where(op => op.Type == MigrationOperationType.SetDefault));
        sorted.AddRange(operations.Where(op => op.Type == MigrationOperationType.DropDefault));

        // Phase 5: Add unique constraints (after all columns exist)
        sorted.AddRange(operations.Where(op => op.Type == MigrationOperationType.AddUnique));

        // Phase 6: Add foreign keys (after all tables and columns exist)
        // Sort FKs so referenced tables' FKs come after referencing tables' FKs
        var fkOps = operations
            .Where(op => op.Type == MigrationOperationType.AddForeignKey)
            .ToList();

        var sortedFks = TopologicalSortForeignKeys(fkOps, sortedTables);
        sorted.AddRange(sortedFks);

        // Phase 7: Drop operations (reverse order - constraints, columns, tables)
        sorted.AddRange(operations.Where(op => op.Type == MigrationOperationType.DropConstraint));
        sorted.AddRange(operations.Where(op => op.Type == MigrationOperationType.DropColumn));
        sorted.AddRange(operations.Where(op => op.Type == MigrationOperationType.DropTable));

        return sorted;
    }

    /// <summary>
    ///     Topologically sorts CreateTable operations based on foreign key dependencies.
    ///     Tables with no dependencies come first, tables that reference others come later.
    /// </summary>
    private static List<MigrationOperation> TopologicalSortTables(
        List<MigrationOperation> createTableOps,
        IReadOnlyList<MigrationOperation> allOperations)
    {
        // Build dependency graph: table -> tables it depends on (references via FK)
        var dependencies = new Dictionary<(string Schema, string Table), HashSet<(string Schema, string Table)>>();

        foreach (var tableOp in createTableOps)
        {
            var key = (tableOp.Schema, tableOp.Table);
            dependencies[key] = [];
        }

        // Find all FKs for these tables
        var fkOps = allOperations
            .Where(op => op.Type == MigrationOperationType.AddForeignKey)
            .ToList();

        foreach (var fkOp in fkOps)
        {
            var fromKey = (fkOp.Schema, fkOp.Table);
            var toKey = (fkOp.RefSchema ?? fkOp.Schema, fkOp.RefTable!);

            // Only track dependencies between tables being created
            if (!dependencies.ContainsKey(fromKey) || !dependencies.ContainsKey(toKey)) continue;

            // Skip self-references (will be handled as deferred constraints)
            if (fromKey != toKey) dependencies[fromKey].Add(toKey);
        }

        // Perform topological sort using Kahn's algorithm
        var sorted = new List<MigrationOperation>();
        var inDegree = new Dictionary<(string Schema, string Table), int>();

        foreach (var key in dependencies.Keys) inDegree[key] = 0;

        foreach (var deps in dependencies.Values)
        foreach (var dep in deps)
            if (inDegree.TryGetValue(dep, out var value))
                inDegree[dep] = ++value;

        var queue = new Queue<(string Schema, string Table)>();
        foreach (var kvp in inDegree.Where(kvp => kvp.Value == 0)) queue.Enqueue(kvp.Key);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var tableOp = createTableOps.First(op => op.Schema == current.Schema && op.Table == current.Table);
            sorted.Add(tableOp);

            foreach (var dependent in dependencies[current])
            {
                if (!inDegree.TryGetValue(dependent, out var value)) continue;

                inDegree[dependent] = --value;
                if (inDegree[dependent] == 0) queue.Enqueue(dependent);
            }
        }

        // If there are remaining tables, we have a circular dependency
        // Add them in original order (they'll need deferred constraints)
        if (sorted.Count < createTableOps.Count)
        {
            var remaining = createTableOps.Where(op => !sorted.Contains(op)).ToList();
            sorted.AddRange(remaining);
        }

        return sorted;
    }

    /// <summary>
    ///     Sorts AddColumn operations by the order tables were created.
    /// </summary>
    private static List<MigrationOperation> SortColumnsByTableOrder(
        List<MigrationOperation> columnOps,
        List<MigrationOperation> sortedTables)
    {
        var tableOrder = new Dictionary<(string Schema, string Table), int>();
        for (var i = 0; i < sortedTables.Count; i++)
        {
            var table = sortedTables[i];
            tableOrder[(table.Schema, table.Table)] = i;
        }

        return columnOps
            .OrderBy(op => tableOrder.GetValueOrDefault((op.Schema, op.Table), int.MaxValue))
            .ThenBy(op => op.Column) // Secondary sort by column name for consistency
            .ToList();
    }

    /// <summary>
    ///     Sorts foreign key operations to avoid referencing tables before their FKs are set up.
    ///     This is a best-effort sort; circular FKs will be added in original order.
    /// </summary>
    private static List<MigrationOperation> TopologicalSortForeignKeys(
        List<MigrationOperation> fkOps,
        List<MigrationOperation> sortedTables)
    {
        // Create a table order map
        var tableOrder = new Dictionary<(string Schema, string Table), int>();
        for (var i = 0; i < sortedTables.Count; i++)
        {
            var table = sortedTables[i];
            tableOrder[(table.Schema, table.Table)] = i;
        }

        // Sort FKs by:
        // 1. Referenced table order (so FKs to earlier tables come first)
        // 2. Source table order
        return fkOps
            .OrderBy(fk =>
            {
                var refKey = (fk.RefSchema ?? fk.Schema, fk.RefTable!);
                return tableOrder.GetValueOrDefault(refKey, int.MaxValue);
            })
            .ThenBy(fk =>
            {
                var key = (fk.Schema, fk.Table);
                return tableOrder.GetValueOrDefault(key, int.MaxValue);
            })
            .ToList();
    }
}