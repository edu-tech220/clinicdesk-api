using System.Security.Claims;

namespace ClinicDesk.Api.Common;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetPatientId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClinicClaimTypes.PatientId);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static Guid? GetDoctorId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClinicClaimTypes.DoctorId);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole("Admin");
}
