using ClimateAlert.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClimateAlert.Infrastructure.Simulation;

public sealed class SimulatedReadingHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<SimulatedReadingHostedService> logger) : BackgroundService
{
    private const int DefaultIntervalSeconds = 60;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int intervalSeconds = ReadInterval(configuration["SIMULATED_READING_INTERVAL_SECONDS"]);
        if (intervalSeconds <= 0)
        {
            logger.LogInformation("Automatic simulated readings are disabled.");
            return;
        }

        TimeSpan interval = TimeSpan.FromSeconds(intervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                ISimulatedReadingCycle cycle =
                    scope.ServiceProvider.GetRequiredService<ISimulatedReadingCycle>();
                await cycle.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The simulated-reading cycle failed.");
            }

            try
            {
                await Task.Delay(interval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public static int ReadInterval(string? configuredValue) =>
        int.TryParse(configuredValue, out int intervalSeconds)
            ? intervalSeconds
            : DefaultIntervalSeconds;
}
