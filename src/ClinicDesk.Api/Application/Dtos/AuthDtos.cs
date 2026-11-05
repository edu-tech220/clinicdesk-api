using System.ComponentModel.DataAnnotations;

namespace ClinicDesk.Api.Application.Dtos;

public record RegisterPatientRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string FullName,
    [Required] string Phone,
    [Required] DateOnly DateOfBirth);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record AuthResponse(string Token, string Role, string FullName, Guid UserId);

public record DoctorAccountCreatedResponse(Guid DoctorId, Guid UserId, string Email, string FullName);

public record CreateDoctorRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string FullName,
    [Required] Guid SpecialtyId,
    [Required] string LicenseNumber,
    string Bio = "");
