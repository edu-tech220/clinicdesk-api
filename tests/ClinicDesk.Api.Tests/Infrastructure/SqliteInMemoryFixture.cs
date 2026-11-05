using ClinicDesk.Api.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ClinicDesk.Api.Tests.Infrastructure;

/// <summary>
/// Mantiene una conexion SQLite ":memory:" abierta para que varios ClinicDeskDbContext
/// independientes vean el mismo esquema y los mismos datos, lo que permite simular
/// peticiones concurrentes de distintos "usuarios" sin levantar un servidor real.
/// </summary>
public sealed class SqliteInMemoryFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteInMemoryFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public ClinicDeskDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ClinicDeskDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new ClinicDeskDbContext(options);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
