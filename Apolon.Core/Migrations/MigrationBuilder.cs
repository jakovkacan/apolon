// csharp

using Apolon.Core.Migrations.Models;

namespace Apolon.Core.Migrations;

public sealed class MigrationBuilder
{
    private readonly List<MigrationOperation> _ops = [];

    public IReadOnlyList<MigrationOperation> Operations => _ops;

    public void CreateSchema(string schema)
    {
        _ops.Add(new MigrationOperation(MigrationOperationType.CreateSchema, schema, ""));
    }

    public void CreateTable(string schema, string table)
    {
        _ops.Add(new MigrationOperation(MigrationOperationType.CreateTable, schema, table));
    }

    public void AddColumn(
        string schema, string table, string column, string sqlType,
        bool isNullable, string? defaultSql = null,
        bool isPrimaryKey = false, bool isIdentity = false, string? identityGeneration = null)
    {
        _ops.Add(new MigrationOperation(
            MigrationOperationType.AddColumn, schema, table,
            column, sqlType, IsNullable: isNullable,
            DefaultSql: defaultSql, IsPrimaryKey: isPrimaryKey,
            IsIdentity: isIdentity, IdentityGeneration: identityGeneration));
    }

    public void DropTable(string schema, string table)
    {
        _ops.Add(new MigrationOperation(MigrationOperationType.DropTable, schema, table));
    }

    public void DropColumn(string schema, string table, string column)
    {
        _ops.Add(new MigrationOperation(MigrationOperationType.DropColumn, schema, table, column));
    }

    public void AlterColumnType(string schema, string table, string column, string sqlType)
    {
        _ops.Add(new MigrationOperation(MigrationOperationType.AlterColumnType, schema, table, column,
            sqlType));
    }

    public void AlterNullability(string schema, string table, string column, bool isNullable)
    {
        _ops.Add(new MigrationOperation(MigrationOperationType.AlterNullability, schema, table, column,
            IsNullable: isNullable));
    }

    public void SetDefault(string schema, string table, string column, string defaultSql)
    {
        _ops.Add(new MigrationOperation(MigrationOperationType.SetDefault, schema, table, column,
            DefaultSql: defaultSql));
    }

    public void DropDefault(string schema, string table, string column)
    {
        _ops.Add(new MigrationOperation(MigrationOperationType.DropDefault, schema, table, column));
    }

    public void AddUnique(string schema, string table, string column)
    {
        _ops.Add(new MigrationOperation(MigrationOperationType.AddUnique, schema, table, column));
    }

    public void DropConstraint(string schema, string table, string constraintName)
    {
        _ops.Add(new MigrationOperation(MigrationOperationType.DropConstraint, schema, table,
            ConstraintName: constraintName));
    }

    public void AddForeignKey(
        string schema,
        string table,
        string column,
        string constraintName,
        string refSchema,
        string refTable,
        string refColumn,
        string? onDeleteRule = null)
    {
        _ops.Add(new MigrationOperation(
            MigrationOperationType.AddForeignKey,
            schema,
            table,
            column,
            ConstraintName: constraintName,
            RefSchema: refSchema,
            RefTable: refTable,
            RefColumn: refColumn,
            OnDeleteRule: onDeleteRule));
    }
}