using ClinicDesk.Api.Domain.Enums;

namespace ClinicDesk.Api.Domain.Entities;

/// <summary>
/// Cuando un paciente pide un slot ya ocupado entra aqui en orden FIFO (CreatedAtUtc).
/// Si el titular cancela, el primero en espera se promueve automaticamente a una cita confirmada.
/// </summary>
public class WaitlistEntry
{
    public Guid Id { get; set; }
    public Guid SlotId { get; set; }
    public Guid PatientId { get; set; }
    public WaitlistStatus Status { get; set; } = WaitlistStatus.Waiting;
    public DateTime CreatedAtUtc { get; set; }
    public Guid? PromotedAppointmentId { get; set; }

    public AppointmentSlot Slot { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
}
