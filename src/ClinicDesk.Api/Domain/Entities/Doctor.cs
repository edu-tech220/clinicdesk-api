namespace ClinicDesk.Api.Domain.Entities;

public class Doctor
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SpecialtyId { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Specialty Specialty { get; set; } = null!;
    public ICollection<DoctorSchedule> Schedules { get; set; } = new List<DoctorSchedule>();
    public ICollection<AppointmentSlot> Slots { get; set; } = new List<AppointmentSlot>();
}
