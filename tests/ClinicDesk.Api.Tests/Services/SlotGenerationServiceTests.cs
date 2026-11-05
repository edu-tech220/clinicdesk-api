using ClinicDesk.Api.Application.Services;
using ClinicDesk.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicDesk.Api.Tests.Services;

public class SlotGenerationServiceTests : IClassFixture<SqliteInMemoryFixture>
{
    private readonly SqliteInMemoryFixture _fixture;

    public SlotGenerationServiceTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GenerateForDoctorAsync_CreatesOneSlotPerHour_ForEveryDayInHorizon_WhenAllWeekdaysAreScheduled()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, _) = await TestDataBuilder.SeedBasicScenarioAsync(db);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            db.DoctorSchedules.Add(TestDataBuilder.ScheduleRule(doctorId, day, today));
        }
        // TestDataBuilder.ScheduleRule uses 9:00-13:00 / 30 min by default -> 8 slots/day.
        await db.SaveChangesAsync();

        var service = new SlotGenerationService(db);
        var horizonDays = 6; // today + 6 dias = 7 dias en total
        var created = await service.GenerateForDoctorAsync(doctorId, horizonDays, default);

        Assert.Equal(7 * 8, created);

        var totalInDb = await db.AppointmentSlots.CountAsync(s => s.DoctorId == doctorId);
        Assert.Equal(7 * 8, totalInDb);
    }

    [Fact]
    public async Task GenerateForDoctorAsync_IsIdempotent_WhenCalledTwiceForTheSameRange()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, _) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.DoctorSchedules.Add(TestDataBuilder.ScheduleRule(doctorId, today.DayOfWeek, today));
        await db.SaveChangesAsync();

        var service = new SlotGenerationService(db);
        var firstRun = await service.GenerateForDoctorAsync(doctorId, 10, default);
        var secondRun = await service.GenerateForDoctorAsync(doctorId, 10, default);

        Assert.True(firstRun > 0);
        Assert.Equal(0, secondRun);
    }

    [Fact]
    public async Task GenerateForDoctorAsync_SkipsExpiredScheduleVersions()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, _) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expiredRule = TestDataBuilder.ScheduleRule(doctorId, today.DayOfWeek, today.AddDays(-30), today.AddDays(-1));
        db.DoctorSchedules.Add(expiredRule);
        await db.SaveChangesAsync();

        var service = new SlotGenerationService(db);
        var created = await service.GenerateForDoctorAsync(doctorId, 10, default);

        Assert.Equal(0, created);
    }

    [Fact]
    public async Task GenerateForDoctorAsync_ReturnsZero_WhenDoctorHasNoSchedule()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, _) = await TestDataBuilder.SeedBasicScenarioAsync(db);

        var service = new SlotGenerationService(db);
        var created = await service.GenerateForDoctorAsync(doctorId, 14, default);

        Assert.Equal(0, created);
    }

    [Fact]
    public async Task GenerateForAllDoctorsAsync_SkipsInactiveDoctors()
    {
        using var db = _fixture.CreateContext();
        var (specialtyId, doctorId, _) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.DoctorSchedules.Add(TestDataBuilder.ScheduleRule(doctorId, today.DayOfWeek, today));

        var (inactiveUser, inactiveDoctor) = TestDataBuilder.Doctor(specialtyId, "Doctor Inactivo");
        inactiveDoctor.IsActive = false;
        db.Users.Add(inactiveUser);
        db.Doctors.Add(inactiveDoctor);
        db.DoctorSchedules.Add(TestDataBuilder.ScheduleRule(inactiveDoctor.Id, today.DayOfWeek, today));
        await db.SaveChangesAsync();

        var service = new SlotGenerationService(db);
        await service.GenerateForAllDoctorsAsync(5, default);

        var inactiveDoctorSlots = await db.AppointmentSlots.CountAsync(s => s.DoctorId == inactiveDoctor.Id);
        Assert.Equal(0, inactiveDoctorSlots);

        var activeDoctorSlots = await db.AppointmentSlots.CountAsync(s => s.DoctorId == doctorId);
        Assert.True(activeDoctorSlots > 0);
    }
}
