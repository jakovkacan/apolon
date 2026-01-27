using Apolon.Core.Attributes;
using Apolon.Core.Migrations.Models;
using Apolon.Core.Sql;

namespace Apolon.Core.Migrations.Utils;

internal static class MigrationUtils
{
    internal static OnDeleteBehavior ParseOnDeleteRule(string? rule)
    {
        return rule?.ToUpperInvariant() switch
        {
            "CASCADE" => OnDeleteBehavior.Cascade,
            "RESTRICT" => OnDeleteBehavior.Restrict,
            "SET NULL" => OnDeleteBehavior.SetNull,
            "SET DEFAULT" => OnDeleteBehavior.SetDefault,
            _ => OnDeleteBehavior.NoAction
        };
    }

    internal static List<string> ConvertOperationsToSql(IReadOnlyList<MigrationOperation> operations)
    {
        var sql = new List<string>();
        foreach (var op in operations)
        {
            switch (op.Type)
            {
                case MigrationOperationType.CreateSchema:
                    sql.Add(MigrationBuilderSql.BuildCreateSchema(op.Schema));
                    break;
                case MigrationOperationType.CreateTable:
                    sql.Add(MigrationBuilderSql.BuildCreateTableFromName(op.Schema, op.Table));
                    break;
                case MigrationOperationType.AddColumn:
                    sql.Add(MigrationBuilderSql.BuildAddColumn(
                        op.Schema, op.Table, op.Column!, op.GetSqlType()!,
                        op.IsNullable!.Value, op.DefaultSql,
                        op.IsPrimaryKey ?? false, op.IsIdentity ?? false, op.IdentityGeneration));
                    break;
                case MigrationOperationType.DropTable:
                    sql.Add(MigrationBuilderSql.BuildDropTableFromName(op.Schema, op.Table));
                    break;
                case MigrationOperationType.DropColumn:
                    sql.Add(MigrationBuilderSql.BuildDropColumn(op.Schema, op.Table, op.Column!));
                    break;
                case MigrationOperationType.AlterColumnType:
                    sql.Add(MigrationBuilderSql.BuildAlterColumnType(op.Schema, op.Table, op.Column!,
                        op.GetSqlType()!));
                    break;
                case MigrationOperationType.AlterNullability:
                    sql.Add(MigrationBuilderSql.BuildAlterNullability(op.Schema, op.Table, op.Column!,
                        op.IsNullable!.Value));
                    break;
                case MigrationOperationType.SetDefault:
                    sql.Add(MigrationBuilderSql.BuildSetDefault(op.Schema, op.Table, op.Column!, op.DefaultSql!));
                    break;
                case MigrationOperationType.DropDefault:
                    sql.Add(MigrationBuilderSql.BuildDropDefault(op.Schema, op.Table, op.Column!));
                    break;
                case MigrationOperationType.AddUnique:
                    sql.Add(MigrationBuilderSql.BuildAddUnique(op.Schema, op.Table, op.Column!));
                    break;
                case MigrationOperationType.DropConstraint:
                    sql.Add(MigrationBuilderSql.BuildDropConstraint(op.Schema, op.Table, op.ConstraintName!));
                    break;
                case MigrationOperationType.AddForeignKey:
                    sql.Add(MigrationBuilderSql.BuildAddForeignKey(
                        schema: op.Schema,
                        table: op.Table,
                        column: op.Column!,
                        constraintName: op.ConstraintName ?? $"{op.Table}_{op.Column}_fkey",
                        refSchema: op.RefSchema ?? "public",
                        refTable: op.RefTable ?? throw new InvalidOperationException("Missing ref table"),
                        refColumn: op.RefColumn ?? "id",
                        onDelete: ParseOnDeleteRule(op.OnDeleteRule)
                    ));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported migration operation type: {op.Type}");
            }
        }

        return sql;
    }
}

internal class MigrationTypeWrapper
{
    public required Type Type { get; set; }
    public required string Name { get; set; }
    public required string Timestamp { get; set; }
}