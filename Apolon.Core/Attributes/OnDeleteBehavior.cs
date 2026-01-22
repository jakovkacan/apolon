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
}