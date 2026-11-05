using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ClinicDesk.Api.Common;
using ClinicDesk.Api.Domain.Entities;
using ClinicDesk.Api.Domain.Enums;
using ClinicDesk.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace ClinicDesk.Api.Tests.Services;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _service = new(Options.Create(new JwtOptions
    {
        Issuer = "ClinicDesk.Tests",
        Audience = "ClinicDesk.Tests.Clients",
        SigningKey = "unit-test-signing-key-at-least-32-bytes-long",
        ExpirationMinutes = 30
    }));

    [Fact]
    public void GenerateToken_IncludesRoleAndPatientClaims()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "p@test.com", FullName = "Paciente Uno", Role = UserRole.Patient };
        var patientId = Guid.NewGuid();

        var token = _service.GenerateToken(user, patientId: patientId);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("Patient", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(patientId.ToString(), jwt.Claims.First(c => c.Type == ClinicClaimTypes.PatientId).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == ClinicClaimTypes.DoctorId);
    }

    [Fact]
    public void GenerateToken_IncludesDoctorClaim_WhenDoctorIdProvided()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "d@test.com", FullName = "Doctor Uno", Role = UserRole.Doctor };
        var doctorId = Guid.NewGuid();

        var token = _service.GenerateToken(user, doctorId: doctorId);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(doctorId.ToString(), jwt.Claims.First(c => c.Type == ClinicClaimTypes.DoctorId).Value);
    }
}
