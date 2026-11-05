using ClinicDesk.Api.Domain.Enums;

namespace ClinicDesk.Api.Domain.Entities;

public class Appointment : IConcurrencyAware
{
    public Guid Id { get; set; }
    public Guid SlotId { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;
    public string ReasonForVisit { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();

    public AppointmentSlot Slot { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public Doctor Doctor { get; set; } = null!;
}
