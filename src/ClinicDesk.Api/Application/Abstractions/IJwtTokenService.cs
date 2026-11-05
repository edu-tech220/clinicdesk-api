using ClinicDesk.Api.Domain.Entities;

namespace ClinicDesk.Api.Application.Abstractions;

public interface IJwtTokenService
{
    string GenerateToken(User user, Guid? patientId = null, Guid? doctorId = null);
}
