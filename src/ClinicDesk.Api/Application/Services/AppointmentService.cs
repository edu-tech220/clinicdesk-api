using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Application.Exceptions;
using ClinicDesk.Api.Common;
using ClinicDesk.Api.Domain.Entities;
using ClinicDesk.Api.Domain.Enums;
using ClinicDesk.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;

namespace ClinicDesk.Api.Application.Services;

public class AppointmentService
{
    private readonly ClinicDeskDbContext _db;
    private readonly ClinicPolicyOptions _policy;

    public AppointmentService(ClinicDeskDbContext db, IOptions<ClinicPolicyOptions> policy)
    {
        _db = db;
        _policy = policy.Value;
    }

    /// <summary>
    /// Reserva un slot disponible. La concurrencia se protege con el token optimista del slot:
    /// si dos pacientes reservan el mismo slot casi al mismo tiempo, el segundo SaveChanges
    /// falla con DbUpdateConcurrencyException y el llamador recibe un 409 para reintentar o
    /// unirse a la lista de espera.
    /// </summary>
    public async Task<AppointmentDto> BookAsync(Guid patientId, BookAppointmentRequest request, CancellationToken ct)
    {
        var slot = await _db.AppointmentSlots
            .Include(s => s.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(s => s.Id == request.SlotId, ct)
            ?? throw new NotFoundException("El horario indicado no existe.");

        if (slot.StartsAtUtc <= DateTime.UtcNow)
        {
            throw new ValidationException("No se puede reservar un horario que ya paso.");
        }

        if (slot.Status != SlotStatus.Available)
        {
            throw new ConflictException("El horario ya no esta disponible. Puedes unirte a la lista de espera.");
        }

        slot.Status = SlotStatus.Booked;

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            SlotId = slot.Id,
            PatientId = patientId,
            DoctorId = slot.DoctorId,
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = request.Reason.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Appointments.Add(appointment);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("El horario fue reservado por otra persona justo antes. Intenta con otro horario.");
        }

        return await GetAsync(appointment.Id, ct);
    }

    public async Task<WaitlistEntryDto> JoinWaitlistAsync(Guid patientId, Guid slotId, CancellationToken ct)
    {
        var slot = await _db.AppointmentSlots.FirstOrDefaultAsync(s => s.Id == slotId, ct)
            ?? throw new NotFoundException("El horario indicado no existe.");

        if (slot.Status != SlotStatus.Booked)
        {
            throw new ValidationException("Solo puedes unirte a la lista de espera de un horario ocupado.");
        }

        var alreadyWaiting = await _db.WaitlistEntries
            .AnyAsync(w => w.SlotId == slotId && w.PatientId == patientId && w.Status == WaitlistStatus.Waiting, ct);
        if (alreadyWaiting)
        {
            throw new ConflictException("Ya estas en la lista de espera de este horario.");
        }

        var entry = new WaitlistEntry
        {
            Id = Guid.NewGuid(),
            SlotId = slotId,
            PatientId = patientId,
            Status = WaitlistStatus.Waiting,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.WaitlistEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        var position = await _db.WaitlistEntries
            .CountAsync(w => w.SlotId == slotId && w.Status == WaitlistStatus.Waiting && w.CreatedAtUtc <= entry.CreatedAtUtc, ct);

        var patientName = await _db.Patients.Where(p => p.Id == patientId).Select(p => p.FullName).FirstAsync(ct);
        return new WaitlistEntryDto(entry.Id, entry.SlotId, entry.PatientId, patientName, entry.Status.ToString(), entry.CreatedAtUtc, position);
    }

    /// <summary>
    /// Cancela una cita confirmada. Si se cancela dentro de la ventana de gracia configurada
    /// queda como LateCancelled (cuenta contra el paciente); si no, como Cancelled. En ambos
    /// casos, si hay alguien en la lista de espera del slot, se promueve FIFO automaticamente
    /// en la misma transaccion en vez de liberar el horario.
    /// </summary>
    public async Task<AppointmentDto> CancelAsync(Guid appointmentId, Guid? actingPatientId, bool isPrivileged, string? reason, CancellationToken ct)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Slot)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw new NotFoundException("La cita indicada no existe.");

        if (!isPrivileged && appointment.PatientId != actingPatientId)
        {
            throw new ForbiddenException("No puedes cancelar la cita de otro paciente.");
        }

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            throw new ConflictException("Solo se pueden cancelar citas confirmadas.");
        }

        var hoursUntilStart = (appointment.Slot.StartsAtUtc - DateTime.UtcNow).TotalHours;
        var isLate = hoursUntilStart < _policy.FreeCancellationWindowHours;

        appointment.Status = isLate ? AppointmentStatus.LateCancelled : AppointmentStatus.Cancelled;
        appointment.CancelledAtUtc = DateTime.UtcNow;
        appointment.CancellationReason = reason?.Trim();

        var nextWaiting = await _db.WaitlistEntries
            .Where(w => w.SlotId == appointment.SlotId && w.Status == WaitlistStatus.Waiting)
            .OrderBy(w => w.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (nextWaiting is not null)
        {
            var promoted = new Appointment
            {
                Id = Guid.NewGuid(),
                SlotId = appointment.SlotId,
                PatientId = nextWaiting.PatientId,
                DoctorId = appointment.DoctorId,
                Status = AppointmentStatus.Confirmed,
                ReasonForVisit = "Promovido automaticamente desde la lista de espera.",
                CreatedAtUtc = DateTime.UtcNow
            };
            nextWaiting.Status = WaitlistStatus.Promoted;
            nextWaiting.PromotedAppointmentId = promoted.Id;
            _db.Appointments.Add(promoted);
        }
        else
        {
            appointment.Slot.Status = SlotStatus.Available;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("La cita cambio de estado mientras se procesaba la cancelacion. Intenta de nuevo.");
        }

        return await GetAsync(appointment.Id, ct);
    }

    public async Task<AppointmentDto> CompleteAsync(Guid appointmentId, CancellationToken ct)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Slot)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw new NotFoundException("La cita indicada no existe.");

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            throw new ConflictException("Solo se pueden completar citas confirmadas.");
        }

        if (appointment.Slot.StartsAtUtc > DateTime.UtcNow)
        {
            throw new ValidationException("No se puede completar una cita que aun no ha iniciado.");
        }

        appointment.Status = AppointmentStatus.Completed;
        await SaveWithConcurrencyCheckAsync(ct);
        return await GetAsync(appointment.Id, ct);
    }

    public async Task<AppointmentDto> MarkNoShowAsync(Guid appointmentId, CancellationToken ct)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Slot)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw new NotFoundException("La cita indicada no existe.");

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            throw new ConflictException("Solo se pueden marcar como inasistencia citas confirmadas.");
        }

        if (appointment.Slot.StartsAtUtc > DateTime.UtcNow)
        {
            throw new ValidationException("No se puede marcar inasistencia antes de la hora de la cita.");
        }

        appointment.Status = AppointmentStatus.NoShow;
        await SaveWithConcurrencyCheckAsync(ct);
        return await GetAsync(appointment.Id, ct);
    }

    public async Task<AppointmentDto> GetAsync(Guid appointmentId, CancellationToken ct)
    {
        var dto = await _db.Appointments
            .Where(a => a.Id == appointmentId)
            .Select(ProjectionExpression)
            .FirstOrDefaultAsync(ct);

        return dto ?? throw new NotFoundException("La cita indicada no existe.");
    }

    public async Task<PagedResult<AppointmentDto>> ListAsync(Guid? patientId, Guid? doctorId, AppointmentStatus? status, PageQuery query, CancellationToken ct)
    {
        var baseQuery = _db.Appointments.AsQueryable();

        if (patientId.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.PatientId == patientId.Value);
        }

        if (doctorId.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.DoctorId == doctorId.Value);
        }

        if (status.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.Status == status.Value);
        }

        var totalCount = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .Select(ProjectionExpression)
            .ToListAsync(ct);

        return new PagedResult<AppointmentDto>
        {
            Items = items,
            Page = query.SafePage,
            PageSize = query.SafePageSize,
            TotalCount = totalCount
        };
    }

    private static readonly Expression<Func<Appointment, AppointmentDto>> ProjectionExpression = a => new AppointmentDto(
        a.Id, a.SlotId, a.PatientId, a.Patient.FullName, a.DoctorId, a.Doctor.User.FullName,
        a.Slot.StartsAtUtc, a.Slot.EndsAtUtc, a.Status.ToString(), a.ReasonForVisit, a.CreatedAtUtc,
        a.CancelledAtUtc, a.CancellationReason);

    private async Task SaveWithConcurrencyCheckAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("La cita cambio de estado mientras se procesaba la solicitud. Intenta de nuevo.");
        }
    }
}
