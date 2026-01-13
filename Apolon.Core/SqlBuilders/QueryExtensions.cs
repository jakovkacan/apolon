using Apolon.Core.DbSet;
namespace Apolon.Core.SqlBuilders;

public static class QueryExtensions
{
    public static List<T> ToList<T>(this QueryBuilder<T> qb, DbSet<T> set) where T : class
    {
        return set.ExecuteQuery(qb);
    }
}