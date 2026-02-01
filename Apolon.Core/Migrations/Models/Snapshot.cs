namespace Apolon.Core.Migrations.Models;

/// <summary>
/// Internal snapshot model used for schema diffing. Use DatabaseSchema for public APIs.
/// </summary>
internal sealed record SchemaSnapshot(
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
        var a = Tables.OrderBy(t => t.Schema).ThenBy(t => t.Name).ToArray();
        var b = other.Tables.OrderBy(t => t.Schema).ThenBy(t => t.Name).ToArray();

        return a.SequenceEqual(b);
    }

    public override int GetHashCode()
    {
        // Stable structural hash: order by key, then fold in element hashes.
        var hc = new HashCode();
        foreach (var t in Tables.OrderBy(t => t.Schema).ThenBy(t => t.Name))
            hc.Add(t);
        return hc.ToHashCode();
    }
}

/// <summary>
/// Internal snapshot model used for schema diffing. Use TableSchema for public APIs.
/// </summary>
internal sealed record TableSnapshot(
    string Schema,
    string Name,
    IReadOnlyList<ColumnSnapshot> Columns
)
{
    public bool Equals(TableSnapshot? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (Schema != other.Schema || Name != other.Name)
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
        hc.Add(Name);
        foreach (var c in Columns.OrderBy(c => c.ColumnName))
            hc.Add(c);
        return hc.ToHashCode();
    }

    public override string ToString()
    {
        var columnsText = Columns.Count == 0
            ? "  (no columns)"
            : string.Join(Environment.NewLine, Columns.Select(c => "  - " + c));

        return $"{Schema}.{Name}{Environment.NewLine}{columnsText}";
    }
}

/// <summary>
/// Internal snapshot model used for schema diffing. Use ColumnSchema for public APIs.
/// </summary>
internal sealed record ColumnSnapshot(
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