using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Application.Exceptions;
using ClinicDesk.Api.Application.Services;
using ClinicDesk.Api.Common;
using ClinicDesk.Api.Tests.Infrastructure;
using Microsoft.Extensions.Options;

namespace ClinicDesk.Api.Tests.Services;

public class AppointmentLifecycleTests : IClassFixture<SqliteInMemoryFixture>
{
    private readonly SqliteInMemoryFixture _fixture;

    public AppointmentLifecycleTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
    }

    private static AppointmentService NewService(ClinicDesk.Api.Infrastructure.ClinicDeskDbContext db) =>
        new(db, Options.Create(new ClinicPolicyOptions()));

    [Fact]
    public async Task CompleteAsync_Throws_WhenAppointmentHasNotStartedYet()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, patientId) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var slot = TestDataBuilder.Slot(doctorId, DateTime.UtcNow.AddHours(5));
        db.AppointmentSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = NewService(db);
        var appointment = await service.BookAsync(patientId, new BookAppointmentRequest(slot.Id, "Consulta"), default);

        await Assert.ThrowsAsync<ValidationException>(() => service.CompleteAsync(appointment.Id, default));
    }

    [Fact]
    public async Task CompleteAsync_Succeeds_AfterSlotHasStarted()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, patientId) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var slot = TestDataBuilder.Slot(doctorId, DateTime.UtcNow.AddMinutes(10));
        db.AppointmentSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = NewService(db);
        var appointment = await service.BookAsync(patientId, new BookAppointmentRequest(slot.Id, "Consulta"), default);

        // Simula que ya paso la hora de la cita (no se puede reservar un slot ya pasado,
        // pero si completar/marcar inasistencia una vez que su horario quedo atras).
        slot.StartsAtUtc = DateTime.UtcNow.AddMinutes(-10);
        await db.SaveChangesAsync();

        var completed = await service.CompleteAsync(appointment.Id, default);

        Assert.Equal("Completed", completed.Status);
    }

    [Fact]
    public async Task MarkNoShowAsync_Succeeds_AfterSlotHasStarted()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, patientId) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var slot = TestDataBuilder.Slot(doctorId, DateTime.UtcNow.AddMinutes(10));
        db.AppointmentSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = NewService(db);
        var appointment = await service.BookAsync(patientId, new BookAppointmentRequest(slot.Id, "Consulta"), default);

        slot.StartsAtUtc = DateTime.UtcNow.AddMinutes(-10);
        await db.SaveChangesAsync();

        var noShow = await service.MarkNoShowAsync(appointment.Id, default);

        Assert.Equal("NoShow", noShow.Status);
    }

    [Fact]
    public async Task JoinWaitlistAsync_Throws_WhenSlotIsStillAvailable()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, patientId) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var slot = TestDataBuilder.Slot(doctorId, DateTime.UtcNow.AddDays(1));
        db.AppointmentSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = NewService(db);

        await Assert.ThrowsAsync<ValidationException>(() => service.JoinWaitlistAsync(patientId, slot.Id, default));
    }

    [Fact]
    public async Task JoinWaitlistAsync_Throws_WhenPatientAlreadyWaiting()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, patientAId) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var (userB, patientB) = TestDataBuilder.Patient();
        db.Users.Add(userB);
        db.Patients.Add(patientB);
        var slot = TestDataBuilder.Slot(doctorId, DateTime.UtcNow.AddDays(1));
        db.AppointmentSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = NewService(db);
        await service.BookAsync(patientAId, new BookAppointmentRequest(slot.Id, "Consulta"), default);
        await service.JoinWaitlistAsync(patientB.Id, slot.Id, default);

        await Assert.ThrowsAsync<ConflictException>(() => service.JoinWaitlistAsync(patientB.Id, slot.Id, default));
    }
}
