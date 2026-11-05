using ClinicDesk.Api;
using ClinicDesk.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClinicDesk.Api.Tests.Infrastructure;

/// <summary>
/// Levanta la API completa (pipeline, auth, controllers) contra SQLite en memoria en vez de
/// SQL Server, para que las pruebas de integracion corran con 'dotnet test' sin dependencias
/// externas. El entorno "Testing" hace que Program.cs omita el registro de SqlServer, la
/// migracion automatica y el hosted service de generacion de slots.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            _connection.Open();
            services.AddDbContext<ClinicDeskDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicDeskDbContext>();
        db.Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
