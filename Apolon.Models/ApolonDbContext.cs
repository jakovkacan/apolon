using Apolon.Core.Context;
using Apolon.Core.DbSet;

namespace Apolon.Models;

public class ApolonDbContext : DbContext
{
    private ApolonDbContext(string connectionString) : base(connectionString)
    {
    }

    public static Task<ApolonDbContext> CreateAsync(string connectionString)
    {
        return CreateAsync<ApolonDbContext>(connectionString);
    }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Checkup> Checkups => Set<Checkup>();
    public DbSet<Medication> Medications => Set<Medication>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<CheckupType> CheckupTypes => Set<CheckupType>();
}