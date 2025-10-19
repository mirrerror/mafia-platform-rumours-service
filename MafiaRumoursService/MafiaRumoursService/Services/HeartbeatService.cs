namespace MafiaRumoursService.Services;

public class HeartbeatService(ServiceRegistryClient registryClient, ILogger<HeartbeatService> logger)
    : BackgroundService
{
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Heartbeat service starting.");

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await registryClient.SendHeartbeatAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unhandled error occurred in the heartbeat service loop.");
            }

            await Task.Delay(_heartbeatInterval, stoppingToken);
        }

        logger.LogInformation("Heartbeat service stopping.");
    }
}