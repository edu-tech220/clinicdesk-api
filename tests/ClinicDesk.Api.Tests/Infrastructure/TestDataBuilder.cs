using ClinicDesk.Api.Domain.Entities;
using ClinicDesk.Api.Domain.Enums;
using ClinicDesk.Api.Infrastructure;

namespace ClinicDesk.Api.Tests.Infrastructure;

/// <summary>Crea entidades validas con valores por defecto razonables para montar escenarios de prueba rapido.</summary>
public static class TestDataBuilder
{
    private static int _counter;

    public static Specialty Specialty(string name = "Medicina general") => new()
    {
        Id = Guid.NewGuid(),
        Name = $"{name} {_counter++}"
    };

    public static (User User, Doctor Doctor) Doctor(Guid specialtyId, string fullName = "Dra. Prueba")
    {
        var n = _counter++;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"doctor{n}@clinicdesk.test",
            PasswordHash = "x",
            PasswordSalt = "x",
            Role = UserRole.Doctor,
            FullName = fullName,
            CreatedAtUtc = DateTime.UtcNow
        };
        var doctor = new Domain.Entities.Doctor
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SpecialtyId = specialtyId,
            LicenseNumber = $"LIC-{n}",
            Bio = "Bio de prueba",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        return (user, doctor);
    }

    public static (User User, Patient Patient) Patient(string fullName = "Paciente Prueba")
    {
        var n = _counter++;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"patient{n}@clinicdesk.test",
            PasswordHash = "x",
            PasswordSalt = "x",
            Role = UserRole.Patient,
            FullName = fullName,
            CreatedAtUtc = DateTime.UtcNow
        };
        var patient = new Domain.Entities.Patient
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FullName = fullName,
            Phone = "5555555555",
            DateOfBirth = new DateOnly(1990, 1, 1),
            CreatedAtUtc = DateTime.UtcNow
        };
        return (user, patient);
    }

    public static AppointmentSlot Slot(Guid doctorId, DateTime startsAtUtc, int durationMinutes = 30, SlotStatus status = SlotStatus.Available) => new()
    {
        Id = Guid.NewGuid(),
        DoctorId = doctorId,
        StartsAtUtc = startsAtUtc,
        EndsAtUtc = startsAtUtc.AddMinutes(durationMinutes),
        Status = status
    };

    public static DoctorSchedule ScheduleRule(Guid doctorId, DayOfWeek dayOfWeek, DateOnly effectiveFrom, DateOnly? effectiveTo = null) => new()
    {
        Id = Guid.NewGuid(),
        DoctorId = doctorId,
        DayOfWeek = dayOfWeek,
        StartTime = TimeSpan.FromHours(9),
        EndTime = TimeSpan.FromHours(13),
        SlotDurationMinutes = 30,
        EffectiveFrom = effectiveFrom,
        EffectiveTo = effectiveTo,
        CreatedAtUtc = DateTime.UtcNow
    };

    public static async Task<(Guid SpecialtyId, Guid DoctorId, Guid PatientId)> SeedBasicScenarioAsync(ClinicDeskDbContext db)
    {
        var specialty = Specialty();
        var (doctorUser, doctor) = Doctor(specialty.Id);
        var (patientUser, patient) = Patient();

        db.Specialties.Add(specialty);
        db.Users.AddRange(doctorUser, patientUser);
        db.Doctors.Add(doctor);
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        return (specialty.Id, doctor.Id, patient.Id);
    }
}
