using Apolon.Core.DbSet;

namespace Apolon.Core.Sql;

public static class QueryExtensions
{
    public static List<T> ToList<T>(this QueryBuilder<T> qb, DbSet<T> set) where T : class
    {
        return set.ExecuteQuery(qb);
    }
}