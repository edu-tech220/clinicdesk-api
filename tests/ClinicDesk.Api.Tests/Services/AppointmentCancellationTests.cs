using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Application.Exceptions;
using ClinicDesk.Api.Application.Services;
using ClinicDesk.Api.Common;
using ClinicDesk.Api.Domain.Enums;
using ClinicDesk.Api.Tests.Infrastructure;
using Microsoft.Extensions.Options;

namespace ClinicDesk.Api.Tests.Services;

public class AppointmentCancellationTests : IClassFixture<SqliteInMemoryFixture>
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly ClinicPolicyOptions _policy = new() { FreeCancellationWindowHours = 24 };

    public AppointmentCancellationTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
    }

    private AppointmentService NewService(ClinicDesk.Api.Infrastructure.ClinicDeskDbContext db) =>
        new(db, Options.Create(_policy));

    [Fact]
    public async Task CancelAsync_MarksAsCancelled_WhenOutsideFreeWindow()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, patientId) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var slot = TestDataBuilder.Slot(doctorId, DateTime.UtcNow.AddHours(48));
        db.AppointmentSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = NewService(db);
        var appointment = await service.BookAsync(patientId, new BookAppointmentRequest(slot.Id, "Consulta"), default);

        var cancelled = await service.CancelAsync(appointment.Id, patientId, isPrivileged: false, "Ya no puedo ir", default);

        Assert.Equal("Cancelled", cancelled.Status);
    }

    [Fact]
    public async Task CancelAsync_MarksAsLateCancelled_WhenInsideFreeWindow()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, patientId) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var slot = TestDataBuilder.Slot(doctorId, DateTime.UtcNow.AddHours(2));
        db.AppointmentSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = NewService(db);
        var appointment = await service.BookAsync(patientId, new BookAppointmentRequest(slot.Id, "Consulta"), default);

        var cancelled = await service.CancelAsync(appointment.Id, patientId, isPrivileged: false, "Se me complico", default);

        Assert.Equal("LateCancelled", cancelled.Status);
    }

    [Fact]
    public async Task CancelAsync_FreesTheSlot_WhenThereIsNoWaitlist()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, patientId) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var slot = TestDataBuilder.Slot(doctorId, DateTime.UtcNow.AddHours(48));
        db.AppointmentSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = NewService(db);
        var appointment = await service.BookAsync(patientId, new BookAppointmentRequest(slot.Id, "Consulta"), default);
        await service.CancelAsync(appointment.Id, patientId, isPrivileged: false, null, default);

        var refreshedSlot = await db.AppointmentSlots.FindAsync(slot.Id);
        Assert.Equal(SlotStatus.Available, refreshedSlot!.Status);
    }

    [Fact]
    public async Task CancelAsync_PromotesFirstWaitlistedPatient_InsteadOfFreeingTheSlot()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, patientAId) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var (userB, patientB) = TestDataBuilder.Patient("Segundo en espera");
        var (userC, patientC) = TestDataBuilder.Patient("Tercero en espera");
        db.Users.AddRange(userB, userC);
        db.Patients.AddRange(patientB, patientC);
        var slot = TestDataBuilder.Slot(doctorId, DateTime.UtcNow.AddHours(48));
        db.AppointmentSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = NewService(db);
        var original = await service.BookAsync(patientAId, new BookAppointmentRequest(slot.Id, "Consulta"), default);

        var firstWaiting = await service.JoinWaitlistAsync(patientB.Id, slot.Id, default);
        var secondWaiting = await service.JoinWaitlistAsync(patientC.Id, slot.Id, default);
        Assert.Equal(1, firstWaiting.Position);
        Assert.Equal(2, secondWaiting.Position);

        await service.CancelAsync(original.Id, patientAId, isPrivileged: false, "Imprevisto", default);

        var slotAfter = await db.AppointmentSlots.FindAsync(slot.Id);
        Assert.Equal(SlotStatus.Booked, slotAfter!.Status);

        var appointments = await service.ListAsync(null, doctorId, null, new PageQuery(1, 20), default);
        var promoted = Assert.Single(appointments.Items, a => a.PatientId == patientB.Id);
        Assert.Equal("Confirmed", promoted.Status);

        var stillWaiting = await service.ListAsync(null, doctorId, null, new PageQuery(1, 20), default);
        Assert.DoesNotContain(stillWaiting.Items, a => a.PatientId == patientC.Id);
    }

    [Fact]
    public async Task CancelAsync_Throws_WhenAnotherPatientTriesToCancel()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, patientAId) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var (userB, patientB) = TestDataBuilder.Patient("Otro paciente");
        db.Users.Add(userB);
        db.Patients.Add(patientB);
        var slot = TestDataBuilder.Slot(doctorId, DateTime.UtcNow.AddHours(48));
        db.AppointmentSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = NewService(db);
        var appointment = await service.BookAsync(patientAId, new BookAppointmentRequest(slot.Id, "Consulta"), default);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CancelAsync(appointment.Id, patientB.Id, isPrivileged: false, null, default));
    }

    [Fact]
    public async Task CancelAsync_Throws_WhenAppointmentIsNotConfirmed()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, patientId) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var slot = TestDataBuilder.Slot(doctorId, DateTime.UtcNow.AddHours(48));
        db.AppointmentSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = NewService(db);
        var appointment = await service.BookAsync(patientId, new BookAppointmentRequest(slot.Id, "Consulta"), default);
        await service.CancelAsync(appointment.Id, patientId, isPrivileged: false, null, default);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CancelAsync(appointment.Id, patientId, isPrivileged: false, null, default));
    }
}
