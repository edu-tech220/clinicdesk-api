using ClinicDesk.Api.Domain.Enums;

namespace ClinicDesk.Api.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public Patient? Patient { get; set; }
    public Doctor? Doctor { get; set; }
}
