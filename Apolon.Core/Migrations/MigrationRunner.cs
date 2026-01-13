using Apolon.Core.DataAccess;
using Apolon.Core.SqlBuilders;

namespace Apolon.Core.Migrations;

public class MigrationRunner
{
    private readonly IDbConnection _connection;
    private readonly string _migrationsPath;
    private readonly EntityExecutor _executor;

    internal MigrationRunner(IDbConnection connection, string migrationsPath = "./Migrations")
    {
        _connection = connection;
        _migrationsPath = migrationsPath;
        _executor = new EntityExecutor(connection);
        EnsureMigrationHistoryTable();
    }

    public void RunPendingMigrations(params Type[] migrationTypes)
    {
        foreach (var migrationType in migrationTypes)
        {
            if (!IsMigrationApplied(migrationType.Name))
            {
                var migration = (Migration)Activator.CreateInstance(migrationType);
                migration.Up();
                RecordMigration(migrationType.Name);
            }
        }
    }

    public void RollbackLastMigration()
    {
        var lastMigration = GetLastAppliedMigration();
        if (lastMigration != null)
        {
            var migrationName = lastMigration;
            var migrationType = Type.GetType($"ApolonORM.Migrations.{migrationName}");
            if (migrationType != null)
            {
                var migration = (Migration)Activator.CreateInstance(migrationType);
                migration.Down();
                RemoveMigration(migrationName);
            }
        }
    }

    private void EnsureMigrationHistoryTable()
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS public.__EFMigrationsHistory (
                migration_id VARCHAR(150) PRIMARY KEY,
                product_version VARCHAR(32) NOT NULL,
                applied_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
        ";
        _connection.ExecuteNonQuery(_connection.CreateCommand(sql));
    }

    private bool IsMigrationApplied(string migrationId)
    {
        var qb = new QueryBuilder<MigrationHistory>()
            .Where(m => m.MigrationId == migrationId);

        var command = _connection.CreateCommand(qb.Build());
        foreach (var param in qb.GetParameters())
        {
            _connection.AddParameter(command, param.Name, param.Value);
        }

        return _connection.ExecuteScalar(command) != null;

        // var sql = "SELECT 1 FROM public.__EFMigrationsHistory WHERE migration_id = @id LIMIT 1";
        // var command = _connection.CreateCommand(sql);
        // command.Parameters.AddWithValue("@id", migrationId);
        // return _connection.ExecuteScalar(command) != null;
    }

    private void RecordMigration(string migrationId)
    {
        _executor.Insert(new MigrationHistory { MigrationId = migrationId, ProductVersion = "1.0" });
        // var sql = "INSERT INTO public.__EFMigrationsHistory (migration_id, product_version) VALUES (@id, @version)";
        // var command = _connection.CreateCommand(sql);
        // command.Parameters.AddWithValue("@id", migrationId);
        // command.Parameters.AddWithValue("@version", "1.0");
        // _connection.ExecuteNonQuery(command);
    }

    private void RemoveMigration(string migrationId)
    {
        _executor.Delete(new MigrationHistory { MigrationId = migrationId });
        // var sql = "DELETE FROM public.__EFMigrationsHistory WHERE migration_id = @id";
        // var command = _connection.CreateCommand(sql);
        // command.Parameters.AddWithValue("@id", migrationId);
        // _connection.ExecuteNonQuery(command);
    }

    private string? GetLastAppliedMigration()
    {
        var qb = new QueryBuilder<MigrationHistory>()
            .OrderByDescending(m => m.AppliedAt)
            .Take(1); // We'll need a "Descending" or just handle via Build()

        return _connection.ExecuteScalar(_connection.CreateCommand(qb.Build()))?.ToString();
    }
}