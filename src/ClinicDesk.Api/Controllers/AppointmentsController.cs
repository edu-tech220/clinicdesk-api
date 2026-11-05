using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Application.Exceptions;
using ClinicDesk.Api.Application.Services;
using ClinicDesk.Api.Common;
using ClinicDesk.Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClinicDesk.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly AppointmentService _appointmentService;

    public AppointmentsController(AppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    [HttpPost("appointments")]
    [Authorize(Roles = "Patient")]
    [EnableRateLimiting("booking")]
    public async Task<ActionResult<AppointmentDto>> Book(BookAppointmentRequest request, CancellationToken ct)
    {
        var patientId = RequirePatientId();
        var result = await _appointmentService.BookAsync(patientId, request, ct);
        return CreatedAtAction(nameof(Get), new { appointmentId = result.Id }, result);
    }

    [HttpPost("slots/{slotId:guid}/waitlist")]
    [Authorize(Roles = "Patient")]
    [EnableRateLimiting("booking")]
    public async Task<ActionResult<WaitlistEntryDto>> JoinWaitlist(Guid slotId, CancellationToken ct)
    {
        var patientId = RequirePatientId();
        var result = await _appointmentService.JoinWaitlistAsync(patientId, slotId, ct);
        return Ok(result);
    }

    [HttpGet("appointments/{appointmentId:guid}")]
    public async Task<ActionResult<AppointmentDto>> Get(Guid appointmentId, CancellationToken ct)
    {
        var appointment = await _appointmentService.GetAsync(appointmentId, ct);
        EnsureCanView(appointment);
        return Ok(appointment);
    }

    [HttpGet("appointments")]
    public async Task<ActionResult<PagedResult<AppointmentDto>>> List(
        [FromQuery] Guid? patientId,
        [FromQuery] Guid? doctorId,
        [FromQuery] AppointmentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (User.IsAdmin())
        {
            // el admin puede filtrar libremente
        }
        else if (User.GetPatientId() is { } ownPatientId)
        {
            patientId = ownPatientId;
            doctorId = null;
        }
        else if (User.GetDoctorId() is { } ownDoctorId)
        {
            doctorId = ownDoctorId;
            patientId = null;
        }
        else
        {
            throw new ForbiddenException("No tienes un perfil de paciente o doctor asociado.");
        }

        var result = await _appointmentService.ListAsync(patientId, doctorId, status, new PageQuery(page, pageSize), ct);
        return Ok(result);
    }

    [HttpPatch("appointments/{appointmentId:guid}/cancel")]
    public async Task<ActionResult<AppointmentDto>> Cancel(Guid appointmentId, CancelAppointmentRequest request, CancellationToken ct)
    {
        var isPrivileged = User.IsAdmin() || User.GetDoctorId() is not null;
        var patientId = User.GetPatientId();
        var result = await _appointmentService.CancelAsync(appointmentId, patientId, isPrivileged, request.Reason, ct);
        return Ok(result);
    }

    [HttpPatch("appointments/{appointmentId:guid}/complete")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<ActionResult<AppointmentDto>> Complete(Guid appointmentId, CancellationToken ct)
    {
        await EnsureDoctorOwnsAppointmentOrAdmin(appointmentId, ct);
        return Ok(await _appointmentService.CompleteAsync(appointmentId, ct));
    }

    [HttpPatch("appointments/{appointmentId:guid}/no-show")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<ActionResult<AppointmentDto>> NoShow(Guid appointmentId, CancellationToken ct)
    {
        await EnsureDoctorOwnsAppointmentOrAdmin(appointmentId, ct);
        return Ok(await _appointmentService.MarkNoShowAsync(appointmentId, ct));
    }

    private Guid RequirePatientId() =>
        User.GetPatientId() ?? throw new ForbiddenException("No tienes un perfil de paciente asociado a tu cuenta.");

    private void EnsureCanView(AppointmentDto appointment)
    {
        if (User.IsAdmin())
        {
            return;
        }

        var patientId = User.GetPatientId();
        if (patientId.HasValue && appointment.PatientId == patientId.Value)
        {
            return;
        }

        var doctorId = User.GetDoctorId();
        if (doctorId.HasValue && appointment.DoctorId == doctorId.Value)
        {
            return;
        }

        throw new ForbiddenException("No puedes ver esta cita.");
    }

    private async Task EnsureDoctorOwnsAppointmentOrAdmin(Guid appointmentId, CancellationToken ct)
    {
        if (User.IsAdmin())
        {
            return;
        }

        var appointment = await _appointmentService.GetAsync(appointmentId, ct);
        var doctorId = User.GetDoctorId();
        if (doctorId is null || appointment.DoctorId != doctorId.Value)
        {
            throw new ForbiddenException("No puedes modificar una cita de otro doctor.");
        }
    }
}
