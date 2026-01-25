using System.Text;
using Apolon.Core.DataAccess;
using Apolon.Core.Migrations;
using Apolon.Core.Migrations.Models;
using Apolon.Core.Sql;

namespace Apolon.Core.Context;

public class DatabaseFacade
{
    private readonly IDbConnection _connection;
    private readonly MigrationRunner _migrationRunner;
    
    internal DatabaseFacade(IDbConnection connection)
    {
        _connection = connection;
        _migrationRunner = new MigrationRunner(connection);
    }
    
    // Connection
    public void OpenConnection() => _connection.OpenConnection();

    public Task OpenConnectionAsync() => _connection.OpenConnectionAsync();

    public void CloseConnection() => _connection.CloseConnection();

    public string GetConnectionString() => _connection.ConnectionString;
    
    public bool CanConnect() => _connection.State == System.Data.ConnectionState.Open;

    // Creation & Deletion
    public void EnsureCreated()
    {
        Console.WriteLine("Database ensured to be created.");
    }
    
    public void EnsureDeleted()
    {
        Console.WriteLine("Database ensured to be deleted.");
    }

    // Migrations
    public void Migrate()
    {
        Console.WriteLine("Database migrations applied.");
    }
    
    public void MigrateAsync()
    {
        Console.WriteLine("Database migrations applied asynchronously.");
    }
    
    public void GetPendingMigrations()
    {
        Console.WriteLine("Retrieved pending migrations.");
    }

    public void GetAppliedMigrations()
    {
        Console.WriteLine("Retrieved applied migrations.");
    }
    
    // Transactions
    public void BeginTransaction() => _connection.BeginTransaction();
    public void CommitTransaction() => _connection.CommitTransaction();
    public void RollbackTransaction() => _connection.RollbackTransaction();
    
    public static string DumpModelSql(Type[] modelTypes)
    {
        StringBuilder sb = new();

        foreach (var modelType in modelTypes)
        {
            sb.Append(MigrationBuilder.BuildCreateTable(modelType));
        }
        
        return sb.ToString();
    }

    public static SchemaSnapshot DumpModelSchema(Type[] modelTypes)
    {
        var modelSnapshot = ModelSnapshotBuilder.BuildFromModel(modelTypes);
        
        return modelSnapshot;
    }

    public async Task<SchemaSnapshot> DumpDbSchema()
    {
        var schemaSnapshot = await SnapshotReader.ReadAsync(_connection);

        return schemaSnapshot;
    }
    
    public static IReadOnlyList<MigrationOperation> DiffSchema(SchemaSnapshot expected, SchemaSnapshot actual)
    {
        var ops = SchemaDiffer.Diff(expected, actual);
        return ops;
    }
    
    public Task<IReadOnlyList<string>> SyncSchemaAsync(params Type[] entityTypes)
    {
        return _migrationRunner.SyncSchemaAsync(entityTypes: entityTypes);
    }
}