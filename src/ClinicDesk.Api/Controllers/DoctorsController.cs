using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Application.Exceptions;
using ClinicDesk.Api.Application.Services;
using ClinicDesk.Api.Common;
using ClinicDesk.Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicDesk.Api.Controllers;

[ApiController]
[Route("api/doctors")]
public class DoctorsController : ControllerBase
{
    private readonly DoctorService _doctorService;
    private readonly SlotQueryService _slotQueryService;
    private readonly SlotGenerationService _slotGenerationService;
    private readonly ClinicPolicyOptions _policy;

    public DoctorsController(
        DoctorService doctorService,
        SlotQueryService slotQueryService,
        SlotGenerationService slotGenerationService,
        Microsoft.Extensions.Options.IOptions<ClinicPolicyOptions> policy)
    {
        _doctorService = doctorService;
        _slotQueryService = slotQueryService;
        _slotGenerationService = slotGenerationService;
        _policy = policy.Value;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<DoctorDto>>> List([FromQuery] Guid? specialtyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await _doctorService.ListAsync(specialtyId, new PageQuery(page, pageSize), ct));
    }

    [HttpGet("{doctorId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<DoctorDto>> Get(Guid doctorId, CancellationToken ct)
    {
        return Ok(await _doctorService.GetAsync(doctorId, ct));
    }

    [HttpGet("{doctorId:guid}/schedule")]
    [AllowAnonymous]
    public async Task<ActionResult<List<ScheduleRuleDto>>> GetSchedule(Guid doctorId, CancellationToken ct)
    {
        return Ok(await _doctorService.GetActiveScheduleAsync(doctorId, ct));
    }

    [HttpPut("{doctorId:guid}/schedule")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<ActionResult<List<ScheduleRuleDto>>> SetSchedule(Guid doctorId, SetScheduleRequest request, CancellationToken ct)
    {
        EnsureDoctorOwnerOrAdmin(doctorId);
        var result = await _doctorService.SetScheduleAsync(doctorId, request, ct);
        return Ok(result);
    }

    [HttpPost("{doctorId:guid}/generate-slots")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<ActionResult<object>> GenerateSlots(Guid doctorId, CancellationToken ct)
    {
        EnsureDoctorOwnerOrAdmin(doctorId);
        var created = await _slotGenerationService.GenerateForDoctorAsync(doctorId, _policy.SlotGenerationHorizonDays, ct);
        return Ok(new { created });
    }

    [HttpGet("{doctorId:guid}/slots")]
    [Authorize]
    public async Task<ActionResult<PagedResult<SlotDto>>> GetAgenda(
        Guid doctorId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] SlotStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        return Ok(await _slotQueryService.GetAgendaAsync(doctorId, from, to, status, new PageQuery(page, pageSize), ct));
    }

    private void EnsureDoctorOwnerOrAdmin(Guid doctorId)
    {
        if (User.IsAdmin())
        {
            return;
        }

        var ownDoctorId = User.GetDoctorId();
        if (ownDoctorId != doctorId)
        {
            throw new ForbiddenException("No puedes administrar la agenda de otro doctor.");
        }
    }
}
