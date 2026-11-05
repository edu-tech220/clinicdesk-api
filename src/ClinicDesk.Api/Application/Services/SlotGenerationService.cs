using ClinicDesk.Api.Domain.Entities;
using ClinicDesk.Api.Domain.Enums;
using ClinicDesk.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicDesk.Api.Application.Services;

/// <summary>
/// Genera AppointmentSlots a partir del horario activo de cada doctor. Es idempotente: antes de
/// insertar, trae en una sola consulta los StartsAtUtc que ya existen para el rango y los salta,
/// en vez de consultar slot por slot (evita N+1 al generar varias semanas de una vez).
/// </summary>
public class SlotGenerationService
{
    private readonly ClinicDeskDbContext _db;

    public SlotGenerationService(ClinicDeskDbContext db)
    {
        _db = db;
    }

    public async Task<int> GenerateForAllDoctorsAsync(int horizonDays, CancellationToken ct)
    {
        var doctorIds = await _db.Doctors.Where(d => d.IsActive).Select(d => d.Id).ToListAsync(ct);
        var total = 0;
        foreach (var doctorId in doctorIds)
        {
            total += await GenerateForDoctorAsync(doctorId, horizonDays, ct);
        }

        return total;
    }

    public async Task<int> GenerateForDoctorAsync(Guid doctorId, int horizonDays, CancellationToken ct)
    {
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var endDate = startDate.AddDays(horizonDays);

        var schedules = await _db.DoctorSchedules
            .Where(s => s.DoctorId == doctorId && s.EffectiveFrom <= endDate && (s.EffectiveTo == null || s.EffectiveTo >= startDate))
            .ToListAsync(ct);

        if (schedules.Count == 0)
        {
            return 0;
        }

        var rangeStartUtc = DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var rangeEndUtc = DateTime.SpecifyKind(endDate.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        var existingStarts = (await _db.AppointmentSlots
            .Where(s => s.DoctorId == doctorId && s.StartsAtUtc >= rangeStartUtc && s.StartsAtUtc < rangeEndUtc)
            .Select(s => s.StartsAtUtc)
            .ToListAsync(ct))
            .ToHashSet();

        var newSlots = new List<AppointmentSlot>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            foreach (var schedule in schedules.Where(s => s.DayOfWeek == date.DayOfWeek && s.IsActiveOn(date)))
            {
                var duration = TimeSpan.FromMinutes(schedule.SlotDurationMinutes);
                var cursor = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.FromTimeSpan(schedule.StartTime)), DateTimeKind.Utc);
                var dayEnd = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.FromTimeSpan(schedule.EndTime)), DateTimeKind.Utc);

                while (cursor + duration <= dayEnd)
                {
                    if (existingStarts.Add(cursor))
                    {
                        newSlots.Add(new AppointmentSlot
                        {
                            Id = Guid.NewGuid(),
                            DoctorId = doctorId,
                            DoctorScheduleId = schedule.Id,
                            StartsAtUtc = cursor,
                            EndsAtUtc = cursor + duration,
                            Status = SlotStatus.Available
                        });
                    }

                    cursor += duration;
                }
            }
        }

        if (newSlots.Count > 0)
        {
            _db.AppointmentSlots.AddRange(newSlots);
            await _db.SaveChangesAsync(ct);
        }

        return newSlots.Count;
    }
}
