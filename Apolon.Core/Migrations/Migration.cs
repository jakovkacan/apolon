using Apolon.Core.DataAccess;

namespace Apolon.Core.Migrations;

public abstract class Migration
{
    internal IDbConnection Connection { get; set; }

    public abstract void Up();
    public abstract void Down();

    protected void ExecuteSql(string sql)
    {
        var command = Connection.CreateCommand(sql);
        Connection.ExecuteNonQuery(command);
    }
}