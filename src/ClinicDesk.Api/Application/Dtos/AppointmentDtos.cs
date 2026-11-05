using System.ComponentModel.DataAnnotations;

namespace ClinicDesk.Api.Application.Dtos;

public record SlotDto(Guid Id, Guid DoctorId, string DoctorName, DateTime StartsAtUtc, DateTime EndsAtUtc, string Status);

public record BookAppointmentRequest(
    [Required] Guid SlotId,
    [Required, MaxLength(500)] string Reason);

public record AppointmentDto(
    Guid Id,
    Guid SlotId,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string Status,
    string Reason,
    DateTime CreatedAtUtc,
    DateTime? CancelledAtUtc,
    string? CancellationReason);

public record CancelAppointmentRequest(string? Reason);

public record WaitlistEntryDto(Guid Id, Guid SlotId, Guid PatientId, string PatientName, string Status, DateTime CreatedAtUtc, int Position);
