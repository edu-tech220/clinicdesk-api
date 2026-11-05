using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Application.Services;
using ClinicDesk.Api.Common;
using ClinicDesk.Api.Tests.Infrastructure;
using Microsoft.Extensions.Options;

namespace ClinicDesk.Api.Tests.Services;

public class PaginationTests : IClassFixture<SqliteInMemoryFixture>
{
    private readonly SqliteInMemoryFixture _fixture;

    public PaginationTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListAsync_ReturnsCorrectSlice_AndTotalPages()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, patientId) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var appointmentService = new AppointmentService(db, Options.Create(new ClinicPolicyOptions()));

        for (var i = 0; i < 5; i++)
        {
            var slot = TestDataBuilder.Slot(doctorId, DateTime.UtcNow.AddDays(i + 1));
            db.AppointmentSlots.Add(slot);
            await db.SaveChangesAsync();
            await appointmentService.BookAsync(patientId, new BookAppointmentRequest(slot.Id, $"Consulta {i}"), default);
        }

        var page1 = await appointmentService.ListAsync(patientId, null, null, new PageQuery(1, 2), default);
        var page2 = await appointmentService.ListAsync(patientId, null, null, new PageQuery(2, 2), default);
        var page3 = await appointmentService.ListAsync(patientId, null, null, new PageQuery(3, 2), default);

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.Single(page3.Items);

        var allIds = page1.Items.Concat(page2.Items).Concat(page3.Items).Select(a => a.Id).Distinct().ToList();
        Assert.Equal(5, allIds.Count);
    }

    [Fact]
    public async Task ListDoctors_FiltersBySpecialty()
    {
        using var db = _fixture.CreateContext();
        var specialtyA = TestDataBuilder.Specialty("Cardiologia");
        var specialtyB = TestDataBuilder.Specialty("Dermatologia");
        var (userA, doctorA) = TestDataBuilder.Doctor(specialtyA.Id, "Doctor Cardio");
        var (userB, doctorB) = TestDataBuilder.Doctor(specialtyB.Id, "Doctor Derma");
        db.Specialties.AddRange(specialtyA, specialtyB);
        db.Users.AddRange(userA, userB);
        db.Doctors.AddRange(doctorA, doctorB);
        await db.SaveChangesAsync();

        var service = new DoctorService(db);
        var result = await service.ListAsync(specialtyA.Id, new PageQuery(1, 10), default);

        var doctor = Assert.Single(result.Items);
        Assert.Equal("Doctor Cardio", doctor.FullName);
    }
}
