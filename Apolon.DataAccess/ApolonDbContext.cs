using Apolon.Core.Context;
using Apolon.Core.DbSet;
using Apolon.Models;

namespace Apolon.DataAccess;

public class ApolonDbContext(string connectionString, bool openConnection = true)
    : DbContext(connectionString, openConnection)
{
    // Domain-specific DbSets exposed as properties
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Checkup> Checkups => Set<Checkup>();
    public DbSet<Medication> Medications => Set<Medication>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<CheckupType> CheckupTypes => Set<CheckupType>();
}