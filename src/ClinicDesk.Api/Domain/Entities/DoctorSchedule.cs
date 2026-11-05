namespace ClinicDesk.Api.Domain.Entities;

/// <summary>
/// Una franja semanal recurrente de un doctor. Cuando el doctor cambia su horario no se
/// sobrescribe esta fila: se cierra (EffectiveToUtc) y se crean filas nuevas, para que los
/// slots ya generados con la version anterior conserven su referencia historica.
/// </summary>
public class DoctorSchedule
{
    public Guid Id { get; set; }
    public Guid DoctorId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int SlotDurationMinutes { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Doctor Doctor { get; set; } = null!;

    public bool IsActiveOn(DateOnly date) =>
        date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo);
}
