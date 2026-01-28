namespace Apolon.Core.Attributes;

public enum OnDeleteBehavior
{
    NoAction = 0,
    Restrict = 1,
    Cascade = 2,
    SetNull = 3,
    SetDefault = 4
}

public static class OnDeleteBehaviorExtensions
{
    public static string ToSql(this OnDeleteBehavior behavior) => behavior switch
    {
        OnDeleteBehavior.Cascade => "CASCADE",
        OnDeleteBehavior.Restrict => "RESTRICT",
        OnDeleteBehavior.SetNull => "SET NULL",
        OnDeleteBehavior.SetDefault => "SET DEFAULT",
        _ => "NO ACTION"
    };

    public static bool TryParse(string? rule, out OnDeleteBehavior behavior)
    {
        if (string.IsNullOrWhiteSpace(rule))
        {
            behavior = OnDeleteBehavior.NoAction;
            return false;
        }

        switch (rule.Trim().ToUpperInvariant())
        {
            case "CASCADE":
                behavior = OnDeleteBehavior.Cascade;
                return true;
            case "RESTRICT":
                behavior = OnDeleteBehavior.Restrict;
                return true;
            case "SET NULL":
                behavior = OnDeleteBehavior.SetNull;
                return true;
            case "SET DEFAULT":
                behavior = OnDeleteBehavior.SetDefault;
                return true;
            case "NO ACTION":
                behavior = OnDeleteBehavior.NoAction;
                return true;
            default:
                behavior = OnDeleteBehavior.NoAction;
                return false;
        }
    }

    public static OnDeleteBehavior ParseOrDefault(string? rule)
        => TryParse(rule, out var result) ? result : OnDeleteBehavior.NoAction;
}