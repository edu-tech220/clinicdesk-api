using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClinicDesk.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterPatientRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterPatientAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("doctors")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DoctorAccountCreatedResponse>> CreateDoctor(CreateDoctorRequest request, CancellationToken ct)
    {
        var result = await _authService.CreateDoctorAsync(request, ct);
        return Ok(result);
    }
}
