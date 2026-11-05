using System.ComponentModel.DataAnnotations;

namespace ClinicDesk.Api.Application.Dtos;

public record SpecialtyDto(Guid Id, string Name);

public record CreateSpecialtyRequest([Required] string Name);

public record DoctorDto(Guid Id, string FullName, string SpecialtyName, string LicenseNumber, string Bio);

public record ScheduleRuleRequest(
    [Range(0, 6)] int DayOfWeek,
    [Required] TimeSpan StartTime,
    [Required] TimeSpan EndTime,
    [Range(5, 240)] int SlotDurationMinutes);

public record SetScheduleRequest(
    [Required] DateOnly EffectiveFrom,
    [Required, MinLength(1)] List<ScheduleRuleRequest> Rules);

public record ScheduleRuleDto(Guid Id, DayOfWeek DayOfWeek, TimeSpan StartTime, TimeSpan EndTime, int SlotDurationMinutes, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
