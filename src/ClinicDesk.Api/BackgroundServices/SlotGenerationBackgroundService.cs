using ClinicDesk.Api.Application.Services;
using ClinicDesk.Api.Common;
using ClinicDesk.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace ClinicDesk.Api.BackgroundServices;

/// <summary>
/// Cada 6 horas revisa los horarios activos de todos los doctores y genera los
/// AppointmentSlots que falten dentro del horizonte configurado. Es seguro ejecutarlo
/// repetidamente porque GenerateForDoctorAsync es idempotente.
/// </summary>
public class SlotGenerationBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ClinicPolicyOptions _policy;
    private readonly ILogger<SlotGenerationBackgroundService> _logger;

    public SlotGenerationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<ClinicPolicyOptions> policy,
        ILogger<SlotGenerationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _policy = policy.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var generator = scope.ServiceProvider.GetRequiredService<SlotGenerationService>();
                var created = await generator.GenerateForAllDoctorsAsync(_policy.SlotGenerationHorizonDays, stoppingToken);
                if (created > 0)
                {
                    _logger.LogInformation("Generados {Count} horarios nuevos", created);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error generando horarios en segundo plano");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
