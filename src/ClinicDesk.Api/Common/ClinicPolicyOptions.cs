namespace ClinicDesk.Api.Common;

public class ClinicPolicyOptions
{
    public const string SectionName = "ClinicPolicy";

    /// <summary>Si se cancela con menos horas de anticipacion que esta, la cita se marca LateCancelled.</summary>
    public int FreeCancellationWindowHours { get; set; } = 24;

    /// <summary>Cuantos dias hacia adelante genera slots el servicio de fondo.</summary>
    public int SlotGenerationHorizonDays { get; set; } = 14;
}
