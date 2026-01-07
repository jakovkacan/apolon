using System.Data.Common;
using Apolon.Core.DbSet;
using Apolon.Models;

namespace Apolon.WebApp.Context;

public class ApolonDbContext : IUnitOfWork
{
    private readonly DbConnection _connection;
    private DbSet<Patient> _patients;
    private DbSet<Checkup> _checkups;
    private DbSet<Medication> _medications;
    private DbSet<Prescription> _prescriptions;

    public DbSet<Patient> Patients => _patients ??= new DbSet<Patient>(_connection);
    public DbSet<Checkup> Checkups => _checkups ??= new DbSet<Checkup>(_connection);
    public DbSet<Medication> Medications => _medications ??= new DbSet<Medication>(_connection);
    public DbSet<Prescription> Prescriptions => _prescriptions ??= new DbSet<Prescription>(_connection);

    public ApolonDbContext(string connectionString)
    {
        _connection = new DbConnection(connectionString);
        _connection.Open();
    }

    public int SaveChanges()
    {
        int total = 0;
        total += Patients.SaveChanges();
        total += Checkups.SaveChanges();
        total += Medications.SaveChanges();
        total += Prescriptions.SaveChanges();
        return total;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await Task.FromResult(SaveChanges());
    }

    public void BeginTransaction()
    {
        _connection.BeginTransaction();
    }

    public void CommitTransaction()
    {
        _connection.CommitTransaction();
    }

    public void RollbackTransaction()
    {
        _connection.RollbackTransaction();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}