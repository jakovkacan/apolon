// using Apolon.Core.DataAccess;
// using Apolon.Core.Mapping;
//
// namespace Apolon.Core.Migrations;
//
// public class SchemaGenerator
// {
//     public static string GenerateCreateTableSql(Type entityType)
//     {
//         var metadata = EntityMapper.GetMetadata(entityType);
//         var lines = new List<string>
//         {
//             $"CREATE TABLE {metadata.Schema}.{metadata.TableName} ("
//         };
//
//         // Add columns
//         foreach (var column in metadata.Columns)
//         {
//             var line = $"    {column.ColumnName} {column.DbType}";
//
//             // Constraints
//             if (column.ColumnName == metadata.PrimaryKey.ColumnName)
//             {
//                 line += metadata.PrimaryKey.AutoIncrement ? " PRIMARY KEY GENERATED ALWAYS AS IDENTITY" : " PRIMARY KEY";
//             }
//             else
//             {
//                 if (!column.IsNullable) line += " NOT NULL";
//                 if (column.DefaultValue != null) line += $" DEFAULT {FormatValue(column.DefaultValue)}";
//                 if (column.IsUnique) line += " UNIQUE";
//             }
//
//             lines.Add(line + ",");
//         }
//
//         // Add foreign keys
//         foreach (var fk in metadata.ForeignKeys)
//         {
//             var refMetadata = EntityMapper.GetMetadata(fk.ReferencedTable);
//             var line = $"    CONSTRAINT fk_{metadata.TableName}_{fk.ColumnName} " +
//                       $"FOREIGN KEY ({fk.ColumnName}) " +
//                       $"REFERENCES {refMetadata.Schema}.{refMetadata.TableName}({fk.ReferencedColumn}) " +
//                       $"ON DELETE {fk.OnDeleteBehavior}";
//             lines.Add(line + ",");
//         }
//
//         // Remove trailing comma from last line
//         if (lines.Count > 1)
//         {
//             lines[lines.Count - 1] = lines[lines.Count - 1].TrimEnd(',');
//         }
//
//         lines.Add(");");
//
//         return string.Join("\n", lines);
//     }
//
//     private static string FormatValue(object value)
//     {
//         return value switch
//         {
//             string s => $"'{s}'",
//             bool b => b ? "true" : "false",
//             DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
//             _ => value.ToString()
//         };
//     }
// }
//
// // Migrations/MigrationRunner.cs
// public class MigrationRunner
// {
//     private readonly IDbConnection _connection;
//     private readonly string _migrationsPath;
//
//     internal MigrationRunner(IDbConnection connection, string migrationsPath = "./Migrations")
//     {
//         _connection = connection;
//         _migrationsPath = migrationsPath;
//         EnsureMigrationHistoryTable();
//     }
//
//     public void RunPendingMigrations(params Type[] migrationTypes)
//     {
//         foreach (var migrationType in migrationTypes)
//         {
//             if (!IsMigrationApplied(migrationType.Name))
//             {
//                 var migration = (Migration)Activator.CreateInstance(migrationType);
//                 migration.Up();
//                 RecordMigration(migrationType.Name);
//             }
//         }
//     }
//
//     public void RollbackLastMigration()
//     {
//         var lastMigration = GetLastAppliedMigration();
//         if (lastMigration != null)
//         {
//             var migrationName = lastMigration;
//             var migrationType = Type.GetType($"ApolonORM.Migrations.{migrationName}");
//             if (migrationType != null)
//             {
//                 var migration = (Migration)Activator.CreateInstance(migrationType);
//                 migration.Down();
//                 RemoveMigration(migrationName);
//             }
//         }
//     }
//
//     private void EnsureMigrationHistoryTable()
//     {
//         var sql = @"
//             CREATE TABLE IF NOT EXISTS public.__EFMigrationsHistory (
//                 migration_id VARCHAR(150) PRIMARY KEY,
//                 product_version VARCHAR(32) NOT NULL,
//                 applied_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
//             );
//         ";
//         _connection.ExecuteNonQuery(_connection.CreateCommand(sql));
//     }
//
//     private bool IsMigrationApplied(string migrationId)
//     {
//         var sql = "SELECT 1 FROM public.__EFMigrationsHistory WHERE migration_id = @id LIMIT 1";
//         var command = _connection.CreateCommand(sql);
//         command.Parameters.AddWithValue("@id", migrationId);
//         return _connection.ExecuteScalar(command) != null;
//     }
//
//     private void RecordMigration(string migrationId)
//     {
//         var sql = "INSERT INTO public.__EFMigrationsHistory (migration_id, product_version) VALUES (@id, @version)";
//         var command = _connection.CreateCommand(sql);
//         command.Parameters.AddWithValue("@id", migrationId);
//         command.Parameters.AddWithValue("@version", "1.0");
//         _connection.ExecuteNonQuery(command);
//     }
//
//     private void RemoveMigration(string migrationId)
//     {
//         var sql = "DELETE FROM public.__EFMigrationsHistory WHERE migration_id = @id";
//         var command = _connection.CreateCommand(sql);
//         command.Parameters.AddWithValue("@id", migrationId);
//         _connection.ExecuteNonQuery(command);
//     }
//
//     private string GetLastAppliedMigration()
//     {
//         var sql = "SELECT migration_id FROM public.__EFMigrationsHistory ORDER BY applied_at DESC LIMIT 1";
//         return _connection.ExecuteScalar(_connection.CreateCommand(sql))?.ToString();
//     }
// }
//
// // Migrations/Migration001_InitialSchema.cs
// public class Migration001_InitialSchema : Migration
// {
//     public override void Up()
//     {
//         // Create Patient table
//         // ExecuteSql(SchemaGenerator.GenerateCreateTableSql(typeof(Patient)));
//         // ExecuteSql(SchemaGenerator.GenerateCreateTableSql(typeof(Checkup)));
//         // ExecuteSql(SchemaGenerator.GenerateCreateTableSql(typeof(Medication)));
//         // ExecuteSql(SchemaGenerator.GenerateCreateTableSql(typeof(Prescription)));
//         // ExecuteSql(SchemaGenerator.GenerateCreateTableSql(typeof(CheckupType)));
//     }
//
//     public override void Down()
//     {
//         ExecuteSql("DROP TABLE IF EXISTS public.prescription CASCADE;");
//         ExecuteSql("DROP TABLE IF EXISTS public.checkup CASCADE;");
//         ExecuteSql("DROP TABLE IF EXISTS public.medication CASCADE;");
//         ExecuteSql("DROP TABLE IF EXISTS public.patient CASCADE;");
//         ExecuteSql("DROP TABLE IF EXISTS public.checkup_type CASCADE;");
//     }
// }