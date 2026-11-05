namespace ClinicDesk.Api.Domain.Entities;

public class Specialty
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
}
