namespace Apolon.Core.Migrations.Models;

public sealed record SchemaSnapshot(
    IReadOnlyList<TableSnapshot> Tables
)
{
    public override string ToString()
    {
        var tablesText = Tables.Count == 0
            ? "(none)"
            : string.Join(Environment.NewLine, Tables.Select(t => t.ToString()));

        return $"Tables:{Environment.NewLine}{tablesText}";
    }

    public bool Equals(SchemaSnapshot? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        // Compare by content, not by list reference; also ignore ordering differences.
        var a = Tables.OrderBy(t => t.Schema).ThenBy(t => t.Table).ToArray();
        var b = other.Tables.OrderBy(t => t.Schema).ThenBy(t => t.Table).ToArray();

        return a.SequenceEqual(b);
    }

    public override int GetHashCode()
    {
        // Stable structural hash: order by key, then fold in element hashes.
        var hc = new HashCode();
        foreach (var t in Tables.OrderBy(t => t.Schema).ThenBy(t => t.Table))
            hc.Add(t);
        return hc.ToHashCode();
    }
    
    // TODO remove method
    public IReadOnlyList<string> Diff(SchemaSnapshot other)
    {
        var diffs = new List<string>();

        var leftTables = Tables.ToDictionary(t => (t.Schema, t.Table));
        var rightTables = other.Tables.ToDictionary(t => (t.Schema, t.Table));

        foreach (var key in leftTables.Keys.Except(rightTables.Keys).OrderBy(k => k.Schema).ThenBy(k => k.Table))
            diffs.Add($"Table missing in OTHER: {key.Schema}.{key.Table}");

        foreach (var key in rightTables.Keys.Except(leftTables.Keys).OrderBy(k => k.Schema).ThenBy(k => k.Table))
            diffs.Add($"Table missing in THIS: {key.Schema}.{key.Table}");

        foreach (var key in leftTables.Keys.Intersect(rightTables.Keys).OrderBy(k => k.Schema).ThenBy(k => k.Table))
        {
            var l = leftTables[key];
            var r = rightTables[key];

            var lCols = l.Columns.ToDictionary(c => c.ColumnName);
            var rCols = r.Columns.ToDictionary(c => c.ColumnName);

            foreach (var col in lCols.Keys.Except(rCols.Keys).OrderBy(x => x))
                diffs.Add($"{key.Schema}.{key.Table}: column missing in OTHER: {col}");

            foreach (var col in rCols.Keys.Except(lCols.Keys).OrderBy(x => x))
                diffs.Add($"{key.Schema}.{key.Table}: column missing in THIS: {col}");

            foreach (var col in lCols.Keys.Intersect(rCols.Keys).OrderBy(x => x))
            {
                var lc = lCols[col];
                var rc = rCols[col];

                // Compare all properties (records already have value equality per property),
                // but we want to report what changed, so list specific fields.
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.DataType), lc.DataType, rc.DataType);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.UdtName), lc.UdtName, rc.UdtName);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.CharacterMaximumLength), lc.CharacterMaximumLength, rc.CharacterMaximumLength);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.NumericPrecision), lc.NumericPrecision, rc.NumericPrecision);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.NumericScale), lc.NumericScale, rc.NumericScale);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.DateTimePrecision), lc.DateTimePrecision, rc.DateTimePrecision);

                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.IsNullable), lc.IsNullable, rc.IsNullable);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.ColumnDefault), lc.ColumnDefault, rc.ColumnDefault);

                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.IsIdentity), lc.IsIdentity, rc.IsIdentity);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.IdentityGeneration), lc.IdentityGeneration, rc.IdentityGeneration);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.IsGenerated), lc.IsGenerated, rc.IsGenerated);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.GenerationExpression), lc.GenerationExpression, rc.GenerationExpression);

                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.IsPrimaryKey), lc.IsPrimaryKey, rc.IsPrimaryKey);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.PkConstraintName), lc.PkConstraintName, rc.PkConstraintName);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.IsUnique), lc.IsUnique, rc.IsUnique);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.UniqueConstraintName), lc.UniqueConstraintName, rc.UniqueConstraintName);

                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.IsForeignKey), lc.IsForeignKey, rc.IsForeignKey);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.FkConstraintName), lc.FkConstraintName, rc.FkConstraintName);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.ReferencesSchema), lc.ReferencesSchema, rc.ReferencesSchema);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.ReferencesTable), lc.ReferencesTable, rc.ReferencesTable);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.ReferencesColumn), lc.ReferencesColumn, rc.ReferencesColumn);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.FkUpdateRule), lc.FkUpdateRule, rc.FkUpdateRule);
                AddIfDifferent(diffs, key, col, nameof(ColumnSnapshot.FkDeleteRule), lc.FkDeleteRule, rc.FkDeleteRule);
            }
        }

        return diffs;

        static void AddIfDifferent<T>(
            List<string> diffs,
            (string Schema, string Table) key,
            string column,
            string prop,
            T left,
            T right)
        {
            if (!EqualityComparer<T>.Default.Equals(left, right))
                diffs.Add($"{key.Schema}.{key.Table}.{column}: {prop} differs (this={left ?? (object)"<null>"}, other={right ?? (object)"<null>"})");
        }
    }
}

public sealed record TableSnapshot(
    string Schema,
    string Table,
    IReadOnlyList<ColumnSnapshot> Columns
)
{
    public bool Equals(TableSnapshot? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (Schema != other.Schema || Table != other.Table)
            return false;

        // Compare by content, not list reference; ignore ordering differences.
        var a = Columns.OrderBy(c => c.ColumnName).ToArray();
        var b = other.Columns.OrderBy(c => c.ColumnName).ToArray();

        return a.SequenceEqual(b);
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Schema);
        hc.Add(Table);
        foreach (var c in Columns.OrderBy(c => c.ColumnName))
            hc.Add(c);
        return hc.ToHashCode();
    }

    public override string ToString()
    {
        var columnsText = Columns.Count == 0
            ? "  (no columns)"
            : string.Join(Environment.NewLine, Columns.Select(c => "  - " + c));

        return $"{Schema}.{Table}{Environment.NewLine}{columnsText}";
    }
}

public sealed record ColumnSnapshot(
    // identity
    string ColumnName,

    // type (high-level + detailed)
    string DataType,
    string UdtName,
    int? CharacterMaximumLength,
    int? NumericPrecision,
    int? NumericScale,
    int? DateTimePrecision,

    // nullability/default
    bool IsNullable,
    string? ColumnDefault,

    // identity / generated columns
    bool IsIdentity,
    string? IdentityGeneration,
    bool IsGenerated,
    string? GenerationExpression,

    // pk / unique
    bool IsPrimaryKey,
    string? PkConstraintName,
    bool IsUnique,
    string? UniqueConstraintName,

    // fk
    bool IsForeignKey,
    string? FkConstraintName,
    string? ReferencesSchema,
    string? ReferencesTable,
    string? ReferencesColumn,
    string? FkUpdateRule,
    string? FkDeleteRule
)
{
    public override string ToString()
    {
        var defaultText = ColumnDefault is null ? "" : $" DEFAULT {ColumnDefault}";

        var typeDetails = new List<string>();
        if (CharacterMaximumLength is not null) typeDetails.Add($"len={CharacterMaximumLength}");
        if (NumericPrecision is not null) typeDetails.Add($"p={NumericPrecision}");
        if (NumericScale is not null) typeDetails.Add($"s={NumericScale}");
        if (DateTimePrecision is not null) typeDetails.Add($"dtp={DateTimePrecision}");

        var typeText = typeDetails.Count == 0
            ? $"{DataType} ({UdtName})"
            : $"{DataType} ({UdtName}; {string.Join(", ", typeDetails)})";

        var pkText = IsPrimaryKey ? $" PK({PkConstraintName ?? "?"})" : "";
        var uqText = IsUnique ? $" UNIQUE({UniqueConstraintName ?? "?"})" : "";

        var identityText = IsIdentity ? $" IDENTITY({IdentityGeneration ?? "?"})" : "";
        var genText = IsGenerated ? $" GENERATED({GenerationExpression ?? "?"})" : "";

        var fkText = IsForeignKey
            ? $" FK({FkConstraintName ?? "?"}) -> {(ReferencesSchema ?? "?")}.{(ReferencesTable ?? "?")}.{(ReferencesColumn ?? "?")} " +
              $"ON UPDATE {(FkUpdateRule ?? "?")} ON DELETE {(FkDeleteRule ?? "?")}"
            : "";

        return
            $"{ColumnName} {typeText}{(IsNullable ? " NULL" : " NOT NULL")}{defaultText}{identityText}{genText}{pkText}{uqText}{fkText}";
    }

    public bool Equals(ColumnSnapshot? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return ColumnName == other.ColumnName && DataType == other.DataType && UdtName == other.UdtName &&
               CharacterMaximumLength == other.CharacterMaximumLength && NumericPrecision == other.NumericPrecision &&
               NumericScale == other.NumericScale && DateTimePrecision == other.DateTimePrecision &&
               IsNullable == other.IsNullable && ColumnDefault == other.ColumnDefault &&
               IsIdentity == other.IsIdentity && IdentityGeneration == other.IdentityGeneration &&
               IsGenerated == other.IsGenerated && GenerationExpression == other.GenerationExpression &&
               IsPrimaryKey == other.IsPrimaryKey && PkConstraintName == other.PkConstraintName &&
               IsUnique == other.IsUnique && UniqueConstraintName == other.UniqueConstraintName &&
               IsForeignKey == other.IsForeignKey && FkConstraintName == other.FkConstraintName &&
               ReferencesSchema == other.ReferencesSchema && ReferencesTable == other.ReferencesTable &&
               ReferencesColumn == other.ReferencesColumn && FkUpdateRule == other.FkUpdateRule &&
               FkDeleteRule == other.FkDeleteRule;
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(ColumnName);
        hashCode.Add(DataType);
        hashCode.Add(UdtName);
        hashCode.Add(CharacterMaximumLength);
        hashCode.Add(NumericPrecision);
        hashCode.Add(NumericScale);
        hashCode.Add(DateTimePrecision);
        hashCode.Add(IsNullable);
        hashCode.Add(ColumnDefault);
        hashCode.Add(IsIdentity);
        hashCode.Add(IdentityGeneration);
        hashCode.Add(IsGenerated);
        hashCode.Add(GenerationExpression);
        hashCode.Add(IsPrimaryKey);
        hashCode.Add(PkConstraintName);
        hashCode.Add(IsUnique);
        hashCode.Add(UniqueConstraintName);
        hashCode.Add(IsForeignKey);
        hashCode.Add(FkConstraintName);
        hashCode.Add(ReferencesSchema);
        hashCode.Add(ReferencesTable);
        hashCode.Add(ReferencesColumn);
        hashCode.Add(FkUpdateRule);
        hashCode.Add(FkDeleteRule);
        return hashCode.ToHashCode();
    }
}