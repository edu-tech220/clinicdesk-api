using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Application.Exceptions;
using ClinicDesk.Api.Application.Services;
using ClinicDesk.Api.Common;
using ClinicDesk.Api.Domain.Enums;
using ClinicDesk.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClinicDesk.Api.Tests.Services;

public class SlotConcurrencyTests : IClassFixture<SqliteInMemoryFixture>
{
    private readonly SqliteInMemoryFixture _fixture;

    public SlotConcurrencyTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
    }

    private static AppointmentService NewService(ClinicDesk.Api.Infrastructure.ClinicDeskDbContext db) =>
        new(db, Options.Create(new ClinicPolicyOptions()));

    [Fact]
    public async Task SaveChanges_Throws_WhenTwoContextsUpdateTheSameSlotFromTheSameOriginalVersion()
    {
        // Esta prueba va directo a EF Core para demostrar que el token de concurrencia
        // (Version, IsConcurrencyToken) realmente protege al slot, sin pasar por el
        // atajo del AppointmentService que ya detecta el conflicto por Status.
        using var setupDb = _fixture.CreateContext();
        var (_, doctorId, _) = await TestDataBuilder.SeedBasicScenarioAsync(setupDb);
        var slot = TestDataBuilder.Slot(doctorId, DateTime.UtcNow.AddDays(3));
        setupDb.AppointmentSlots.Add(slot);
        await setupDb.SaveChangesAsync();

        using var contextA = _fixture.CreateContext();
        using var contextB = _fixture.CreateContext();

        var slotA = await contextA.AppointmentSlots.FindAsync(slot.Id);
        var slotB = await contextB.AppointmentSlots.FindAsync(slot.Id);

        slotA!.Status = SlotStatus.Booked;
        await contextA.SaveChangesAsync();

        slotB!.Status = SlotStatus.Booked;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync());
    }

    [Fact]
    public async Task BookAsync_SecondPatientLosesTheRace_AndGetsConflict()
    {
        using var setupDb = _fixture.CreateContext();
        var (_, doctorId, patientAId) = await TestDataBuilder.SeedBasicScenarioAsync(setupDb);
        var (userB, patientB) = TestDataBuilder.Patient("Paciente Dos");
        setupDb.Users.Add(userB);
        setupDb.Patients.Add(patientB);
        var slot = TestDataBuilder.Slot(doctorId, DateTime.UtcNow.AddDays(3));
        setupDb.AppointmentSlots.Add(slot);
        await setupDb.SaveChangesAsync();

        using var dbA = _fixture.CreateContext();
        using var dbB = _fixture.CreateContext();
        var serviceA = NewService(dbA);
        var serviceB = NewService(dbB);

        var request = new BookAppointmentRequest(slot.Id, "Consulta general");

        var confirmed = await serviceA.BookAsync(patientAId, request, default);
        Assert.Equal("Confirmed", confirmed.Status);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => serviceB.BookAsync(patientB.Id, request, default));
        Assert.Contains("disponible", ex.Message);
    }

    [Fact]
    public async Task BookAsync_Throws_WhenSlotIsInThePast()
    {
        using var setupDb = _fixture.CreateContext();
        var (_, doctorId, patientId) = await TestDataBuilder.SeedBasicScenarioAsync(setupDb);
        var pastSlot = TestDataBuilder.Slot(doctorId, DateTime.UtcNow.AddHours(-1));
        setupDb.AppointmentSlots.Add(pastSlot);
        await setupDb.SaveChangesAsync();

        using var db = _fixture.CreateContext();
        var service = NewService(db);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.BookAsync(patientId, new BookAppointmentRequest(pastSlot.Id, "Consulta"), default));
    }

    [Fact]
    public async Task BookAsync_Throws_WhenSlotDoesNotExist()
    {
        using var setupDb = _fixture.CreateContext();
        var (_, _, patientId) = await TestDataBuilder.SeedBasicScenarioAsync(setupDb);

        using var db = _fixture.CreateContext();
        var service = NewService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.BookAsync(patientId, new BookAppointmentRequest(Guid.NewGuid(), "Consulta"), default));
    }
}
