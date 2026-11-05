using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Application.Exceptions;
using ClinicDesk.Api.Domain.Entities;
using ClinicDesk.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicDesk.Api.Application.Services;

public class DoctorService
{
    private readonly ClinicDeskDbContext _db;

    public DoctorService(ClinicDeskDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<DoctorDto>> ListAsync(Guid? specialtyId, PageQuery query, CancellationToken ct)
    {
        var baseQuery = _db.Doctors.Where(d => d.IsActive);
        if (specialtyId.HasValue)
        {
            baseQuery = baseQuery.Where(d => d.SpecialtyId == specialtyId.Value);
        }

        var totalCount = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderBy(d => d.User.FullName)
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .Select(d => new DoctorDto(d.Id, d.User.FullName, d.Specialty.Name, d.LicenseNumber, d.Bio))
            .ToListAsync(ct);

        return new PagedResult<DoctorDto>
        {
            Items = items,
            Page = query.SafePage,
            PageSize = query.SafePageSize,
            TotalCount = totalCount
        };
    }

    public async Task<DoctorDto> GetAsync(Guid doctorId, CancellationToken ct)
    {
        var doctor = await _db.Doctors
            .Where(d => d.Id == doctorId)
            .Select(d => new DoctorDto(d.Id, d.User.FullName, d.Specialty.Name, d.LicenseNumber, d.Bio))
            .FirstOrDefaultAsync(ct);

        return doctor ?? throw new NotFoundException("Doctor no encontrado.");
    }

    public async Task<List<ScheduleRuleDto>> GetActiveScheduleAsync(Guid doctorId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rules = await _db.DoctorSchedules
            .Where(s => s.DoctorId == doctorId && s.EffectiveFrom <= today && (s.EffectiveTo == null || s.EffectiveTo >= today))
            .OrderBy(s => s.DayOfWeek)
            .ToListAsync(ct);

        return rules.Select(r => new ScheduleRuleDto(r.Id, r.DayOfWeek, r.StartTime, r.EndTime, r.SlotDurationMinutes, r.EffectiveFrom, r.EffectiveTo)).ToList();
    }

    /// <summary>
    /// Reemplaza el horario semanal de un doctor sin borrar el historial: cierra las reglas
    /// vigentes (EffectiveTo = EffectiveFrom - 1 dia) e inserta las nuevas a partir de EffectiveFrom.
    /// Los slots ya generados conservan su DoctorScheduleId y no se ven afectados.
    /// </summary>
    public async Task<List<ScheduleRuleDto>> SetScheduleAsync(Guid doctorId, SetScheduleRequest request, CancellationToken ct)
    {
        var doctorExists = await _db.Doctors.AnyAsync(d => d.Id == doctorId, ct);
        if (!doctorExists)
        {
            throw new NotFoundException("Doctor no encontrado.");
        }

        if (request.Rules.Any(r => r.StartTime >= r.EndTime))
        {
            throw new ValidationException("La hora de inicio debe ser menor a la hora de fin en cada regla.");
        }

        var currentlyActive = await _db.DoctorSchedules
            .Where(s => s.DoctorId == doctorId && s.EffectiveTo == null)
            .ToListAsync(ct);

        foreach (var schedule in currentlyActive)
        {
            schedule.EffectiveTo = request.EffectiveFrom.AddDays(-1);
        }

        var newRules = request.Rules.Select(r => new DoctorSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            DayOfWeek = (DayOfWeek)r.DayOfWeek,
            StartTime = r.StartTime,
            EndTime = r.EndTime,
            SlotDurationMinutes = r.SlotDurationMinutes,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = null,
            CreatedAtUtc = DateTime.UtcNow
        }).ToList();

        _db.DoctorSchedules.AddRange(newRules);
        await _db.SaveChangesAsync(ct);

        return newRules.Select(r => new ScheduleRuleDto(r.Id, r.DayOfWeek, r.StartTime, r.EndTime, r.SlotDurationMinutes, r.EffectiveFrom, r.EffectiveTo)).ToList();
    }
}
