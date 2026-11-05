using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Application.Services;
using ClinicDesk.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicDesk.Api.Tests.Services;

public class ScheduleVersioningTests : IClassFixture<SqliteInMemoryFixture>
{
    private readonly SqliteInMemoryFixture _fixture;

    public ScheduleVersioningTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SetScheduleAsync_ClosesPreviousVersion_InsteadOfDeletingIt()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, _) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var service = new DoctorService(db);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await service.SetScheduleAsync(doctorId, new SetScheduleRequest(today, new List<ScheduleRuleRequest>
        {
            new((int)DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(13), 30)
        }), default);

        var nextWeek = today.AddDays(7);
        await service.SetScheduleAsync(doctorId, new SetScheduleRequest(nextWeek, new List<ScheduleRuleRequest>
        {
            new((int)DayOfWeek.Tuesday, TimeSpan.FromHours(10), TimeSpan.FromHours(14), 30)
        }), default);

        var allRules = await db.DoctorSchedules.Where(s => s.DoctorId == doctorId).OrderBy(s => s.EffectiveFrom).ToListAsync();

        Assert.Equal(2, allRules.Count);
        Assert.Equal(nextWeek.AddDays(-1), allRules[0].EffectiveTo);
        Assert.Equal(DayOfWeek.Monday, allRules[0].DayOfWeek);
        Assert.Null(allRules[1].EffectiveTo);
        Assert.Equal(DayOfWeek.Tuesday, allRules[1].DayOfWeek);
    }

    [Fact]
    public async Task GetActiveScheduleAsync_StillReturnsFirstVersion_WhenSecondVersionIsInTheFuture()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, _) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var service = new DoctorService(db);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await service.SetScheduleAsync(doctorId, new SetScheduleRequest(today, new List<ScheduleRuleRequest>
        {
            new((int)DayOfWeek.Wednesday, TimeSpan.FromHours(9), TimeSpan.FromHours(13), 30)
        }), default);

        await service.SetScheduleAsync(doctorId, new SetScheduleRequest(today.AddDays(30), new List<ScheduleRuleRequest>
        {
            new((int)DayOfWeek.Friday, TimeSpan.FromHours(8), TimeSpan.FromHours(12), 20)
        }), default);

        var active = await service.GetActiveScheduleAsync(doctorId, default);

        var rule = Assert.Single(active);
        Assert.Equal(DayOfWeek.Wednesday, rule.DayOfWeek);
    }

    [Fact]
    public async Task SetScheduleAsync_Throws_WhenStartTimeIsNotBeforeEndTime()
    {
        using var db = _fixture.CreateContext();
        var (_, doctorId, _) = await TestDataBuilder.SeedBasicScenarioAsync(db);
        var service = new DoctorService(db);

        var request = new SetScheduleRequest(DateOnly.FromDateTime(DateTime.UtcNow), new List<ScheduleRuleRequest>
        {
            new((int)DayOfWeek.Monday, TimeSpan.FromHours(14), TimeSpan.FromHours(10), 30)
        });

        await Assert.ThrowsAsync<ClinicDesk.Api.Application.Exceptions.ValidationException>(() =>
            service.SetScheduleAsync(doctorId, request, default));
    }
}
