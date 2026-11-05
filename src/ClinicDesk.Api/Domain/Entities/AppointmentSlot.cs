using ClinicDesk.Api.Domain.Enums;

namespace ClinicDesk.Api.Domain.Entities;

public class AppointmentSlot : IConcurrencyAware
{
    public Guid Id { get; set; }
    public Guid DoctorId { get; set; }
    public Guid? DoctorScheduleId { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public SlotStatus Status { get; set; } = SlotStatus.Available;
    public Guid Version { get; set; } = Guid.NewGuid();

    public Doctor Doctor { get; set; } = null!;
    public DoctorSchedule? DoctorSchedule { get; set; }
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<WaitlistEntry> WaitlistEntries { get; set; } = new List<WaitlistEntry>();
}
