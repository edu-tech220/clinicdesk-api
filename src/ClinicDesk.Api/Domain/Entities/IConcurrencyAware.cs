namespace ClinicDesk.Api.Domain.Entities;

/// <summary>
/// Entidades que usan un token de concurrencia optimista propio en vez de ROWVERSION
/// nativo de SQL Server, para que el mismo modelo funcione igual en SQLite (pruebas)
/// y en SQL Server (produccion). ClinicDeskDbContext regenera Version en cada SaveChanges.
/// </summary>
public interface IConcurrencyAware
{
    Guid Version { get; set; }
}
