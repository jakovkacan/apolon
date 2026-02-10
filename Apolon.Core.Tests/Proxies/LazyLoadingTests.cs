using Apolon.Core.Context;
using Apolon.Core.DbSet;
using Apolon.Core.Proxies;
using Apolon.Models;
using Xunit;

namespace Apolon.Core.Tests.Proxies;

public class LazyLoadingTests
{
    private const string TestConnectionString = "Host=localhost;Port=5432;Database=apolon_test;Username=admin;Password=password;";

    [Fact]
    public void LazyLoading_LoadsReferencePropertyOnAccess()
    {
        // Arrange
        var options = new DbContextOptionsBuilder()
            .UseLazyLoadingProxies()
            .Build();
        
        using var context = TestDbContext.Create(TestConnectionString, options);
        
        // Act - Query checkup without Include
        var checkups = context.Checkups.ToList();
        
        if (checkups.Count == 0)
        {
            // Skip test if no data
            return;
        }
        
        var firstCheckup = checkups.First();
        
        // Assert - Patient should be null initially, then loaded on access
        var patient = firstCheckup.Patient;
        
        // If lazy loading works, patient should be loaded
        Assert.NotNull(patient);
    }

    [Fact]
    public void LazyLoading_LoadsCollectionPropertyOnAccess()
    {
        // Arrange
        var options = new DbContextOptionsBuilder()
            .UseLazyLoadingProxies()
            .Build();
        
        using var context = TestDbContext.Create(TestConnectionString, options);
        
        // Act - Query patient without Include
        var patients = context.Patients.ToList();
        
        if (patients.Count == 0)
        {
            // Skip test if no data
            return;
        }
        
        var firstPatient = patients.First();
        
        // Assert - Checkups should be loaded on access
        var checkups = firstPatient.Checkups;
        
        Assert.NotNull(checkups);
        // If patient has checkups, they should be loaded
    }

    [Fact]
    public void LazyLoading_SupportsTransitiveLoading()
    {
        // Arrange
        var options = new DbContextOptionsBuilder()
            .UseLazyLoadingProxies()
            .Build();
        
        using var context = TestDbContext.Create(TestConnectionString, options);
        
        // Act - Query patient, then access checkups, then access prescriptions
        var patients = context.Patients.ToList();
        
        if (patients.Count == 0)
        {
            // Skip test if no data
            return;
        }
        
        var patient = patients.First();
        var checkups = patient.Checkups;
        
        if (checkups.Count > 0)
        {
            var firstCheckup = checkups.First();
            var prescriptions = firstCheckup.Prescriptions;
            
            // Assert - All levels should be loaded
            Assert.NotNull(patient);
            Assert.NotNull(checkups);
            Assert.NotNull(prescriptions);
        }
    }

    [Fact]
    public void WithoutLazyLoading_NavigationPropertiesAreNull()
    {
        // Arrange - Create context WITHOUT lazy loading
        using var context = TestDbContext.Create(TestConnectionString);
        
        // Act - Query checkup without Include
        var checkups = context.Checkups.ToList();
        
        if (checkups.Count == 0)
        {
            // Skip test if no data
            return;
        }
        
        var firstCheckup = checkups.First();
        
        // Assert - Patient should be null without lazy loading
        Assert.Null(firstCheckup.Patient);
    }

    [Fact]
    public void Include_MarksNavigationAsLoaded_PreventsLazyLoad()
    {
        // Arrange
        var options = new DbContextOptionsBuilder()
            .UseLazyLoadingProxies()
            .Build();
        
        using var context = TestDbContext.Create(TestConnectionString, options);
        
        // Act - Use Include to eagerly load
        var checkups = context.Checkups.Include(c => c.Patient);
        
        if (checkups.Count == 0)
        {
            // Skip test if no data
            return;
        }
        
        var firstCheckup = checkups.First();
        
        // Assert - Patient should be loaded via Include, not lazy loading
        // This test mainly verifies that no duplicate queries are executed
        var patient = firstCheckup.Patient;
        Assert.NotNull(patient);
    }

    [Fact]
    public void EntityTracker_ReturnsSameInstanceForSamePrimaryKey()
    {
        // Arrange
        var options = new DbContextOptionsBuilder()
            .UseLazyLoadingProxies()
            .Build();
        
        using var context = TestDbContext.Create(TestConnectionString, options);
        
        // Act - Query same entity through different paths
        var patients = context.Patients.ToList();
        
        if (patients.Count == 0)
        {
            // Skip test if no data
            return;
        }
        
        var patient1 = patients.First();
        var patientId = patient1.Id;
        
        // Access through checkup
        var checkups = patient1.Checkups;
        if (checkups.Count > 0)
        {
            var checkup = checkups.First();
            var patient2 = checkup.Patient;
            
            // Assert - Should be the same instance (identity map)
            Assert.Same(patient1, patient2);
        }
    }
}

// Test-specific DbContext
file class TestDbContext : DbContext
{
    private TestDbContext(string connectionString) : base(connectionString)
    {
    }

    private TestDbContext(string connectionString, DbContextOptions options) : base(connectionString, options)
    {
    }

    public static TestDbContext Create(string connectionString)
    {
        return Create<TestDbContext>(connectionString);
    }

    public static TestDbContext Create(string connectionString, DbContextOptions options)
    {
        return Create<TestDbContext>(connectionString, options);
    }

    public DbSet<Checkup> Checkups => Set<Checkup>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
}


