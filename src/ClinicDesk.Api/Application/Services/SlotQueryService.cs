using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Domain.Enums;
using ClinicDesk.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicDesk.Api.Application.Services;

public class SlotQueryService
{
    private readonly ClinicDeskDbContext _db;

    public SlotQueryService(ClinicDeskDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<SlotDto>> GetAgendaAsync(Guid doctorId, DateTime? from, DateTime? to, SlotStatus? status, PageQuery query, CancellationToken ct)
    {
        var baseQuery = _db.AppointmentSlots.Where(s => s.DoctorId == doctorId);

        if (from.HasValue)
        {
            baseQuery = baseQuery.Where(s => s.StartsAtUtc >= from.Value);
        }

        if (to.HasValue)
        {
            baseQuery = baseQuery.Where(s => s.StartsAtUtc <= to.Value);
        }

        if (status.HasValue)
        {
            baseQuery = baseQuery.Where(s => s.Status == status.Value);
        }

        var totalCount = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderBy(s => s.StartsAtUtc)
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .Select(s => new SlotDto(s.Id, s.DoctorId, s.Doctor.User.FullName, s.StartsAtUtc, s.EndsAtUtc, s.Status.ToString()))
            .ToListAsync(ct);

        return new PagedResult<SlotDto>
        {
            Items = items,
            Page = query.SafePage,
            PageSize = query.SafePageSize,
            TotalCount = totalCount
        };
    }
}
