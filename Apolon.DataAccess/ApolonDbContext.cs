using Apolon.Core.Context;
using Apolon.Core.DbSet;
using Apolon.Models;

namespace Apolon.DataAccess;

public class ApolonDbContext(string connectionString) : DbContext(connectionString) // Inherits generic DbContext
{
    // Domain-specific DbSets exposed as properties
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Checkup> Checkups => Set<Checkup>();
    public DbSet<Medication> Medications => Set<Medication>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<CheckupType> CheckupTypes => Set<CheckupType>();
}