using Apolon.DataAccess;
using Apolon.Models;

const string connectionString =
    "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=apolon;";

var context = new ApolonDbContext(connectionString, false);
try
{
    Console.WriteLine("Connecting to database...");
    
    await context.Database.OpenConnectionAsync();
    
    Console.WriteLine("Connected to database.");
    
    Console.WriteLine("PATIENTS");
    var patients = context.Patients.ToList();
    patients.ForEach(Console.WriteLine);
    
    Console.WriteLine("CHECKUPS");
    var checkups = context.Checkups.Include(c => c.CheckupType, c => c.Patient).ToList();
    checkups.ForEach(checkup => {
        Console.WriteLine($"{checkup} for {checkup.Patient}");
    });
    
    Console.WriteLine("MEDICATIONS");
    var medications = context.Medications.ToList();
    medications.ForEach(Console.WriteLine);
    
    Console.WriteLine("PRESCRIPTIONS");
    var prescriptions = context.Prescriptions.ToList();
    prescriptions.ForEach(Console.WriteLine);
    
    Console.WriteLine("DONE");
    
    // context.Database.EnsureCreated();

    // var checkups = context.Checkups.Include(c => c.CheckupType, c => c.Patient).ToList();
    // foreach (var checkup in checkups)
    // {
    //     Console.WriteLine(
    //         $"Checkup: Patient Name: {checkup.Patient.FirstName} {checkup.Patient.LastName}, Checkup Type: {checkup.CheckupType.TypeCode}, CheckupDate: {checkup.CheckupDate}, Notes: {checkup.Notes}, Results: {checkup.Results}");
    // }

    // var checkup2 = new Checkup { PatientId = 1, CheckupTypeId = 1, CheckupDate = DateTime.Now.Subtract(TimeSpan.FromDays(30)), Notes = "Regular checkup", Results = "It's over guys" };
    // context.Checkups.Add(checkup2);
    // context.SaveChanges();
    //
    // var patients = context.Patients.Include(p => p.Checkups).ToList();
    // foreach (var patient in patients)
    // {
    //     Console.WriteLine(
    //         $"Patient: {patient.FirstName} {patient.LastName}, Email: {patient.Email}, Phone: {patient.PhoneNumber}, Gender: {patient.Gender}, Address: {patient.Address}, Checkups: {patient.Checkups.Count}");
    //     foreach (var checkup in patient.Checkups)
    //     {
    //         Console.WriteLine($"Checkup: {checkup.CheckupDate}, Notes: {checkup.Notes}, Results: {checkup.Results}");
    //     }
    // }

    // var patient = new Patient { FirstName = "John", LastName = "Doe", Email = "john.doe@mail.com" };
    // context.Patients.Add(patient);
    // context.SaveChanges();

    // var type = new CheckupType { TypeCode = "X-RAY", Description = "X-Ray" };
    //
    // context.CheckupTypes.Add(type);
    // context.SaveChanges();

    // var query = new QueryBuilder<Medication>().Where(x => x.Name.Contains("Lupocet"));

    // var medication = context.Medications
    //     .ExecuteQuery(query)
    //     .FirstOrDefault();


    // var medications = context.Medications.ToList();
    // foreach (var med in medications)
    // {
    //     Console.WriteLine($"Medication: {med.Name}, Generic Name: {med.GenericName}, Dosage Form: {med.DosageForm}");
    // }

    // if (lupocet != null)
    // {
    //     lupocet.GenericName = "Forte";
    //     context.Medications.Update(lupocet);
    //     context.SaveChanges();
    //     
    //     Console.WriteLine("Updated medication.");
    // }

    // var medication = new Medication
    // {
    //     Name = "Lupocet",
    //     GenericName = "Lupocet",
    //     DosageForm = "Tablet"
    // };
    //
    // context.Medications.Add(medication);
    // context.SaveChanges();

    // Console.WriteLine("Added medication.");

    // context.Medications.Query();

    // var patient = new Patient
    // {
    //     FirstName = "John",
    //     LastName = "Doe",
    //     Email = "john@example.com",
    //     PhoneNumber = "123456789",
    //     DateOfBirth = DateTime.Now.AddYears(-20),
    //     Address = "123 Main St",
    //     Gender = "Male",
    //     CreatedAt = DateTime.Now,
    //     UpdatedAt = DateTime.Now,
    // };
    //
    // context.Patients.Add(patient);
    // context.SaveChanges();
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
    Console.WriteLine(ex.StackTrace);
}
finally
{
    context.Dispose();
}