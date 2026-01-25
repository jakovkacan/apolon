namespace Apolon.Core.Migrations.Models;

//TODO Make internal
public enum MigrationOperationType
{
    CreateSchema,
    CreateTable,
    DropTable,
    AddColumn,
    DropColumn,
    AlterColumnType,
    AlterNullability,
    SetDefault,
    DropDefault,
    AddUnique,
    DropConstraint,
    AddForeignKey
}

//TODO Make internal
public sealed record MigrationOperation(
    MigrationOperationType Type,
    string Schema,
    string Table,
    string? Column = null,
    string? SqlType = null,
    int? CharacterMaximumLength = null,
    int? NumericPrecision = null,
    int? NumericScale = null,
    int? DateTimePrecision = null,
    bool? IsPrimaryKey = null,
    bool? IsIdentity = null,
    string? IdentityGeneration = null,
    bool? IsNullable = null,
    string? DefaultSql = null,
    string? ConstraintName = null,
    string? RefSchema = null,
    string? RefTable = null,
    string? RefColumn = null,
    string? OnDeleteRule = null
)
{
    public string? GetSqlType()
        => BuildSqlType(SqlType, CharacterMaximumLength, NumericPrecision, NumericScale, DateTimePrecision);

    public static string? BuildSqlType(
        string? baseType,
        int? characterMaximumLength,
        int? numericPrecision,
        int? numericScale,
        int? dateTimePrecision)
    {
        if (string.IsNullOrWhiteSpace(baseType))
            return null;

        var normalized = baseType.Trim().ToLowerInvariant();
        var upperType = normalized.ToUpperInvariant();

        if (characterMaximumLength is not null && IsLengthType(normalized))
            return $"{upperType}({characterMaximumLength})";

        if (numericPrecision is not null && IsNumericType(normalized))
        {
            return numericScale is not null
                ? $"{upperType}({numericPrecision},{numericScale})"
                : $"{upperType}({numericPrecision})";
        }

        if (dateTimePrecision is not null && IsDateTimeType(normalized))
            return $"{upperType}({dateTimePrecision})";

        return upperType;
    }

    public override string ToString()
    {
        var parts = new List<string>
        {
            $"{nameof(Type)}: {Type}",
            $"{nameof(Schema)}: {Schema}",
            $"{nameof(Table)}: {Table}"
        };

        if (Column is not null) parts.Add($"{nameof(Column)}: {Column}");
        if (SqlType is not null) parts.Add($"{nameof(SqlType)}: {SqlType}");
        if (CharacterMaximumLength is not null) parts.Add($"{nameof(CharacterMaximumLength)}: {CharacterMaximumLength}");
        if (NumericPrecision is not null) parts.Add($"{nameof(NumericPrecision)}: {NumericPrecision}");
        if (NumericScale is not null) parts.Add($"{nameof(NumericScale)}: {NumericScale}");
        if (DateTimePrecision is not null) parts.Add($"{nameof(DateTimePrecision)}: {DateTimePrecision}");
        if (IsPrimaryKey.HasValue) parts.Add($"{nameof(IsPrimaryKey)}: {IsPrimaryKey.Value}");
        if (IsIdentity.HasValue) parts.Add($"{nameof(IsIdentity)}: {IsIdentity.Value}");
        if (IdentityGeneration is not null) parts.Add($"{nameof(IdentityGeneration)}: {IdentityGeneration}");
        if (IsNullable.HasValue) parts.Add($"{nameof(IsNullable)}: {IsNullable.Value}");
        if (DefaultSql is not null) parts.Add($"{nameof(DefaultSql)}: {DefaultSql}");
        if (ConstraintName is not null) parts.Add($"{nameof(ConstraintName)}: {ConstraintName}");
        if (RefSchema is not null) parts.Add($"{nameof(RefSchema)}: {RefSchema}");
        if (RefTable is not null) parts.Add($"{nameof(RefTable)}: {RefTable}");
        if (RefColumn is not null) parts.Add($"{nameof(RefColumn)}: {RefColumn}");
        if (OnDeleteRule is not null) parts.Add($"{nameof(OnDeleteRule)}: {OnDeleteRule}");

        return string.Join(", ", parts);
    }

    private static bool IsLengthType(string normalizedType)
        => normalizedType is "varchar" or "char" or "character" or "character varying" or "varbit" or "bit varying" or "bit";

    private static bool IsNumericType(string normalizedType)
        => normalizedType is "numeric" or "decimal";

    private static bool IsDateTimeType(string normalizedType)
        => normalizedType is "timestamp" or "timestamptz" or "time" or "timetz";
}
