using Apolon.Core.Attributes;
using Apolon.Core.Migrations.Models;
using Apolon.Core.Sql;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Apolon.Core.Migrations.Utils;

internal static class MigrationUtils
{
    private static string ConvertOperationToSql(MigrationOperation operation)
    {
        return operation.Type switch
        {
            MigrationOperationType.CreateSchema => MigrationBuilderSql.BuildCreateSchema(operation.Schema),
            MigrationOperationType.CreateTable => MigrationBuilderSql.BuildCreateTableFromName(operation.Schema,
                operation.Table),
            MigrationOperationType.AddColumn => MigrationBuilderSql.BuildAddColumn(operation.Schema, operation.Table,
                operation.Column!, operation.GetSqlType()!, operation.IsNullable!.Value, operation.DefaultSql,
                operation.IsPrimaryKey ?? false, operation.IsIdentity ?? false, operation.IdentityGeneration),
            MigrationOperationType.DropTable => MigrationBuilderSql.BuildDropTableFromName(operation.Schema,
                operation.Table),
            MigrationOperationType.DropColumn => MigrationBuilderSql.BuildDropColumn(operation.Schema, operation.Table,
                operation.Column!),
            MigrationOperationType.AlterColumnType => MigrationBuilderSql.BuildAlterColumnType(operation.Schema,
                operation.Table, operation.Column!, operation.GetSqlType()!),
            MigrationOperationType.AlterNullability => MigrationBuilderSql.BuildAlterNullability(operation.Schema,
                operation.Table, operation.Column!, operation.IsNullable!.Value),
            MigrationOperationType.SetDefault => MigrationBuilderSql.BuildSetDefault(operation.Schema, operation.Table,
                operation.Column!, operation.DefaultSql!),
            MigrationOperationType.DropDefault => MigrationBuilderSql.BuildDropDefault(operation.Schema,
                operation.Table, operation.Column!),
            MigrationOperationType.AddUnique => MigrationBuilderSql.BuildAddUnique(operation.Schema, operation.Table,
                operation.Column!),
            MigrationOperationType.DropConstraint => MigrationBuilderSql.BuildDropConstraint(operation.Schema,
                operation.Table, operation.ConstraintName!),
            MigrationOperationType.AddForeignKey => MigrationBuilderSql.BuildAddForeignKey(operation.Schema,
                operation.Table, operation.Column!,
                operation.ConstraintName ?? $"{operation.Table}_{operation.Column}_fkey",
                operation.RefSchema ?? "public",
                operation.RefTable ?? throw new InvalidOperationException("Missing ref table"),
                operation.RefColumn ?? "id",
                OnDeleteBehaviorExtensions.ParseOrDefault(operation.OnDeleteRule)),
            _ => throw new InvalidOperationException($"Unsupported migration operation type: {operation.Type}")
        };
    }

    internal static List<string> ConvertOperationsToSql(IReadOnlyList<MigrationOperation> operations)
    {
        // Sort operations to respect dependencies (FK constraints, etc.)
        var sortedOperations = MigrationOperationSorter.Sort(operations);

        return sortedOperations.Select(ConvertOperationToSql).ToList();
    }

    public static IReadOnlyList<MigrationOperation> ExtractOperationsFromMigrationTypes(
        IEnumerable<Type> migrationTypes)
    {
        return migrationTypes
            .Select(ExtractOperationsFromMigration)
            .SelectMany(operations => operations)
            .ToList();
    }

    /// <summary>
    ///     Extracts MigrationOperations from a migration type by reading its source file and parsing the Up method.
    /// </summary>
    /// <param name="migrationType">The Type of the migration class.</param>
    /// <returns>List of operations performed in the Up method.</returns>
    private static IReadOnlyList<MigrationOperation> ExtractOperationsFromMigration(Type migrationType)
    {
        // Get the source file path from the type's assembly location
        var assemblyLocation = migrationType.Assembly.Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);

        if (string.IsNullOrEmpty(assemblyDirectory))
            throw new InvalidOperationException(
                $"Could not determine assembly directory for type {migrationType.FullName}");

        // Try to find the source file
        // Look in typical migration folders: Migrations, Data/Migrations, etc.
        var possibleDirectories = new[]
        {
            Path.Combine(assemblyDirectory, "..", "..", "..", "Migrations"),
            Path.Combine(assemblyDirectory, "..", "..", "..", "Data", "Migrations"),
            Path.Combine(assemblyDirectory, "Migrations")
        };

        string? sourceFilePath = null;
        foreach (var directory in possibleDirectories)
        {
            var normalizedDirectory = Path.GetFullPath(directory);
            if (!Directory.Exists(normalizedDirectory)) continue;

            // Search for files matching pattern: *_{ClassName}.cs to handle timestamp prefixes
            var matchingFiles = Directory.GetFiles(normalizedDirectory, $"*_{migrationType.Name}.cs");
            if (matchingFiles.Length > 0)
            {
                sourceFilePath = matchingFiles[0]; // Take the first match
                break;
            }
        }

        if (sourceFilePath == null)
            throw new FileNotFoundException($"Could not find source file for migration type {migrationType.Name}");

        var sourceCode = File.ReadAllText(sourceFilePath);
        return ExtractOperationsFromMigrationSource(sourceCode);
    }

    /// <summary>
    ///     Extracts MigrationOperations from generated migration file source code by parsing the Up method.
    /// </summary>
    /// <param name="migrationFileContent">The C# source code of the migration file.</param>
    /// <returns>List of operations performed in the Up method.</returns>
    private static IReadOnlyList<MigrationOperation> ExtractOperationsFromMigrationSource(string migrationFileContent)
    {
        // Parse the syntax tree
        var tree = CSharpSyntaxTree.ParseText(migrationFileContent);
        var root = tree.GetRoot();

        // Find the Migration class
        var classDeclaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.BaseList?.Types.Any(t => t.ToString().Contains("Migration")) == true);

        if (classDeclaration == null)
            throw new InvalidOperationException("Could not find Migration class in the provided source code.");

        // Find the Up method
        var upMethod = classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "Up");

        if (upMethod == null)
            throw new InvalidOperationException("Could not find Up method in the Migration class.");

        // Create a MigrationBuilder and execute the method logic by parsing the syntax
        var migrationBuilder = new MigrationBuilder();
        ParseUpMethodBody(upMethod, migrationBuilder);

        return migrationBuilder.Operations;
    }

    private static void ParseUpMethodBody(MethodDeclarationSyntax upMethod, MigrationBuilder builder)
    {
        if (upMethod.Body == null)
            return;

        foreach (var statement in upMethod.Body.Statements)
        {
            if (statement is not ExpressionStatementSyntax expressionStatement)
                continue;

            var invocation = expressionStatement.Expression as InvocationExpressionSyntax;
            if (invocation == null)
                continue;

            ParseInvocation(invocation, builder);
        }
    }

    private static void ParseInvocation(InvocationExpressionSyntax invocation, MigrationBuilder builder)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            memberAccess.Expression.ToString() != "migrationBuilder")
            return;

        var methodName = memberAccess.Name.Identifier.Text;
        var arguments = invocation.ArgumentList.Arguments;

        var schemaName = GetStringArgument(arguments, "schema") ?? GetStringArgument(arguments, 0) ?? "public";
        var tableName = GetStringArgument(arguments, "table") ?? GetStringArgument(arguments, 1);
        var columnName = GetStringArgument(arguments, "column") ?? GetStringArgument(arguments, 2);

        switch (methodName)
        {
            case "CreateSchema":
                builder.CreateSchema(schemaName);
                break;

            case "CreateTable":
                ParseCreateTable(invocation, builder);
                break;

            case "DropTable":
                builder.DropTable(schemaName, tableName ?? throw new InvalidOperationException("Missing table name"));
                break;

            case "AddColumn":
                var sqlTypeAddColumn = GetStringArgument(arguments, "sqlType") ?? GetStringArgument(arguments, 3);
                builder.AddColumn(
                    schemaName,
                    tableName ?? throw new InvalidOperationException("Missing table name"),
                    columnName ?? throw new InvalidOperationException("Missing column name"),
                    sqlTypeAddColumn ?? throw new InvalidOperationException("Missing column SQL type"),
                    GetBoolArgument(arguments, "isNullable") ?? GetBoolArgument(arguments, 4) ?? false,
                    GetStringArgument(arguments, "defaultSql"),
                    GetBoolArgument(arguments, "isPrimaryKey") ?? false,
                    GetBoolArgument(arguments, "isIdentity") ?? false,
                    GetStringArgument(arguments, "identityGeneration"));
                break;

            case "DropColumn":
                builder.DropColumn(
                    schemaName,
                    tableName ?? throw new InvalidOperationException("Missing table name"),
                    columnName ?? throw new InvalidOperationException("Missing column name"));
                break;

            case "AlterColumnType":
                var sqlTypeAlterColumnType = GetStringArgument(arguments, "sqlType") ?? GetStringArgument(arguments, 3);
                builder.AlterColumnType(
                    schemaName,
                    tableName ?? throw new InvalidOperationException("Missing table name"),
                    columnName ?? throw new InvalidOperationException("Missing column name"),
                    sqlTypeAlterColumnType ?? throw new InvalidOperationException("Missing column SQL type"));
                break;

            case "AlterNullability":
                builder.AlterNullability(
                    schemaName,
                    tableName ?? throw new InvalidOperationException("Missing table name"),
                    columnName ?? throw new InvalidOperationException("Missing column name"),
                    GetBoolArgument(arguments, "isNullable") ?? GetBoolArgument(arguments, 3) ?? false);
                break;

            case "SetDefault":
                var defaultSqlSetDefault =
                    GetStringArgument(arguments, "defaultSql") ?? GetStringArgument(arguments, 3);
                builder.SetDefault(
                    schemaName,
                    tableName ?? throw new InvalidOperationException("Missing table name"),
                    columnName ?? throw new InvalidOperationException("Missing column name"),
                    defaultSqlSetDefault ?? throw new InvalidOperationException("Missing default SQL"));
                break;

            case "DropDefault":
                builder.DropDefault(
                    schemaName,
                    tableName ?? throw new InvalidOperationException("Missing table name"),
                    columnName ?? throw new InvalidOperationException("Missing column name"));
                break;

            case "AddUnique":
                builder.AddUnique(
                    schemaName,
                    tableName ?? throw new InvalidOperationException("Missing table name"),
                    columnName ?? throw new InvalidOperationException("Missing column name"));
                break;

            case "DropConstraint":
                var constraintNameDropConstraint =
                    GetStringArgument(arguments, "constraintName") ?? GetStringArgument(arguments, 2);
                builder.DropConstraint(
                    schemaName,
                    tableName ?? throw new InvalidOperationException("Missing table name"),
                    constraintNameDropConstraint ?? throw new InvalidOperationException("Missing constraint name"));
                break;

            case "AddForeignKey":
                var constraintNameAddForeignKey =
                    GetStringArgument(arguments, "constraintName") ?? GetStringArgument(arguments, 3);
                var refSchemaAddForeignKey =
                    GetStringArgument(arguments, "refSchema") ?? GetStringArgument(arguments, 4);
                var refTableAddForeignKey = GetStringArgument(arguments, "refTable") ?? GetStringArgument(arguments, 5);
                var refColumnAddForeignKey =
                    GetStringArgument(arguments, "refColumn") ?? GetStringArgument(arguments, 6);
                var onDeleteRuleAddForeignKey =
                    GetStringArgument(arguments, "onDeleteRule") ?? GetStringArgument(arguments, 7);
                builder.AddForeignKey(
                    schemaName,
                    tableName ?? throw new InvalidOperationException("Missing table name"),
                    columnName ?? throw new InvalidOperationException("Missing column name"),
                    constraintNameAddForeignKey ?? throw new InvalidOperationException("Missing constraint name"),
                    refSchemaAddForeignKey ?? throw new InvalidOperationException("Missing reference schema"),
                    refTableAddForeignKey ?? throw new InvalidOperationException("Missing reference table"),
                    refColumnAddForeignKey ?? throw new InvalidOperationException("Missing reference column"),
                    onDeleteRuleAddForeignKey ?? throw new InvalidOperationException("Missing on delete rule"));
                break;
        }
    }

    private static void ParseCreateTable(InvocationExpressionSyntax invocation, MigrationBuilder builder)
    {
        var arguments = invocation.ArgumentList.Arguments;

        var tableName = GetStringArgument(arguments, "name") ?? GetStringArgument(arguments, 0);
        var schema = GetStringArgument(arguments, "schema") ?? "public";

        if (tableName == null)
            return;

        // Create schema and table operations
        builder.CreateSchema(schema);
        builder.CreateTable(schema, tableName);

        // Parse columns lambda
        var columnsLambda = arguments.FirstOrDefault(a =>
            a.NameColon?.Name.Identifier.Text == "columns" || arguments.IndexOf(a) == 1);
        if (columnsLambda?.Expression is SimpleLambdaExpressionSyntax columnsSimpleLambda)
            if (columnsSimpleLambda.ExpressionBody is AnonymousObjectCreationExpressionSyntax columnsAnonymousObject)
                ParseColumnDefinitions(columnsAnonymousObject, schema, tableName, builder);

        // Parse constraints lambda
        var constraintsLambda = arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == "constraints");
        if (constraintsLambda?.Expression is not SimpleLambdaExpressionSyntax constraintsSimpleLambda) return;

        if (constraintsSimpleLambda.Body is BlockSyntax constraintsBlock)
            ParseConstraints(constraintsBlock, schema, tableName, builder);
    }

    private static void ParseColumnDefinitions(
        AnonymousObjectCreationExpressionSyntax columnsObject,
        string schema,
        string tableName,
        MigrationBuilder builder)
    {
        foreach (var initializer in columnsObject.Initializers)
        {
            if (initializer.NameEquals == null)
                continue;

            var columnName = initializer.NameEquals.Name.Identifier.Text;

            // Parse the Column<T>() invocation chain
            var columnExpression = initializer.Expression;
            var columnInfo = ParseColumnExpression(columnExpression);

            builder.AddColumn(
                schema,
                tableName,
                columnName,
                columnInfo.SqlType ?? "TEXT",
                columnInfo.IsNullable,
                columnInfo.DefaultSql,
                columnInfo.IsPrimaryKey,
                columnInfo.IsIdentity,
                columnInfo.IdentityGeneration);

            if (columnInfo.IsUnique) builder.AddUnique(schema, tableName, columnName);
        }
    }

    private static ColumnInfo ParseColumnExpression(ExpressionSyntax expression)
    {
        var info = new ColumnInfo { IsNullable = true };

        // Start from the innermost Column<T>() call
        var current = expression;

        while (current != null)
        {
            // Check if this is table.Column<T>()
            if (current is InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax memberAccess
                } invocation)
            {
                var methodName = memberAccess.Name.Identifier.Text;

                if (methodName == "Column")
                {
                    // Parse Column<T>() arguments
                    ParseColumnArguments(invocation.ArgumentList.Arguments, info);
                    // Move to the parent expression (the chain continues upward)
                }
                else
                {
                    // Parse fluent method calls (HasPrecision, HasMaxLength, etc.)
                    ParseFluentMethod(methodName, invocation.ArgumentList.Arguments, info);

                    // Move to the expression on the left side
                    current = memberAccess.Expression;
                    continue;
                }
            }

            break;
        }

        return info;
    }

    private static void ParseColumnArguments(SeparatedSyntaxList<ArgumentSyntax> arguments, ColumnInfo info)
    {
        info.SqlType = GetStringArgument(arguments, "type");

        var nullable = GetBoolArgument(arguments, "nullable");
        if (nullable.HasValue)
            info.IsNullable = nullable.Value;
    }

    private static void ParseFluentMethod(string methodName, SeparatedSyntaxList<ArgumentSyntax> arguments,
        ColumnInfo info)
    {
        switch (methodName)
        {
            case "Annotation":
                var annotationName = GetStringArgument(arguments, 0);
                var annotationValue = GetStringArgument(arguments, 1);
                if (annotationName == "Postgres:Identity")
                {
                    info.IsIdentity = true;
                    info.IdentityGeneration = annotationValue;
                }

                break;

            case "HasDefaultValueSql":
                info.DefaultSql = GetStringArgument(arguments, 0);
                break;

            case "HasMaxLength":
                // We don't need to extract this as it's already in the type parameter
                break;

            case "HasPrecision":
                // Already in type parameter
                break;

            case "IsUnique":
                info.IsUnique = true;
                break;
        }
    }

    private static void ParseConstraints(BlockSyntax constraintsBlock, string schema, string tableName,
        MigrationBuilder builder)
    {
        foreach (var statement in constraintsBlock.Statements)
        {
            if (statement is not ExpressionStatementSyntax expressionStatement)
                continue;

            if (expressionStatement.Expression is not InvocationExpressionSyntax invocation)
                continue;

            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                continue;

            var methodName = memberAccess.Name.Identifier.Text;
            var arguments = invocation.ArgumentList.Arguments;

            switch (methodName)
            {
                case "PrimaryKey":
                    // Primary key is already marked in AddColumn
                    break;

                case "UniqueConstraint":
                    // Unique constraints are already added via IsUnique
                    break;

                case "ForeignKey":
                    ParseForeignKeyConstraint(arguments, schema, tableName, builder);
                    break;
            }
        }
    }

    private static void ParseForeignKeyConstraint(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        string schema,
        string tableName,
        MigrationBuilder builder)
    {
        var constraintName = GetStringArgument(arguments, 0);
        var column = ExtractColumnFromLambda(arguments, 1);
        var refTable = GetStringArgument(arguments, 2);
        var refColumn = GetStringArgument(arguments, 3);
        var principalSchema = GetStringArgument(arguments, "principalSchema") ?? "public";
        var onDelete = GetStringArgument(arguments, "onDelete");

        if (constraintName != null && column != null && refTable != null && refColumn != null)
            builder.AddForeignKey(
                schema,
                tableName,
                column,
                constraintName,
                principalSchema,
                refTable,
                refColumn,
                onDelete);
    }

    private static string? ExtractColumnFromLambda(SeparatedSyntaxList<ArgumentSyntax> arguments, int index)
    {
        if (index >= arguments.Count)
            return null;

        var argument = arguments[index];
        if (argument.Expression is SimpleLambdaExpressionSyntax lambda)
            if (lambda.ExpressionBody is MemberAccessExpressionSyntax memberAccess)
                return memberAccess.Name.Identifier.Text;

        return null;
    }

    private static string? GetStringArgument(SeparatedSyntaxList<ArgumentSyntax> arguments, string name)
    {
        var argument = arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == name);
        return GetStringValue(argument?.Expression);
    }

    private static string? GetStringArgument(SeparatedSyntaxList<ArgumentSyntax> arguments, int position)
    {
        if (position >= arguments.Count)
            return null;

        return GetStringValue(arguments[position].Expression);
    }

    private static string? GetStringValue(ExpressionSyntax? expression)
    {
        if (expression is LiteralExpressionSyntax literal && literal.Token.Value is string stringValue)
            return stringValue;

        return null;
    }

    private static bool? GetBoolArgument(SeparatedSyntaxList<ArgumentSyntax> arguments, string name)
    {
        var argument = arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == name);
        return GetBoolValue(argument?.Expression);
    }

    private static bool? GetBoolArgument(SeparatedSyntaxList<ArgumentSyntax> arguments, int position)
    {
        if (position >= arguments.Count)
            return null;

        return GetBoolValue(arguments[position].Expression);
    }

    private static bool? GetBoolValue(ExpressionSyntax? expression)
    {
        if (expression is LiteralExpressionSyntax literal)
        {
            if (literal.Kind() == SyntaxKind.TrueLiteralExpression)
                return true;
            if (literal.Kind() == SyntaxKind.FalseLiteralExpression)
                return false;
        }

        return null;
    }

    private class ColumnInfo
    {
        public string? SqlType { get; set; }
        public bool IsNullable { get; set; }
        public string? DefaultSql { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsIdentity { get; set; }
        public string? IdentityGeneration { get; set; }
        public bool IsUnique { get; set; }
    }
}

internal class MigrationTypeWrapper
{
    public required Type Type { get; init; }
    public required string Name { get; init; }
    public required string Timestamp { get; set; }
}