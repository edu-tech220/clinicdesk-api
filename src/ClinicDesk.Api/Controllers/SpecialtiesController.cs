using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicDesk.Api.Controllers;

[ApiController]
[Route("api/specialties")]
public class SpecialtiesController : ControllerBase
{
    private readonly SpecialtyService _specialtyService;

    public SpecialtiesController(SpecialtyService specialtyService)
    {
        _specialtyService = specialtyService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<SpecialtyDto>>> List(CancellationToken ct)
    {
        return Ok(await _specialtyService.ListAsync(ct));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SpecialtyDto>> Create(CreateSpecialtyRequest request, CancellationToken ct)
    {
        var result = await _specialtyService.CreateAsync(request, ct);
        return Ok(result);
    }
}
