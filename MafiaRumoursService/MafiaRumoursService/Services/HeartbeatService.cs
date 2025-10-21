namespace MafiaRumoursService.Services;

public class HeartbeatService(ServiceRegistryClient registryClient, ILogger<HeartbeatService> logger)
    : BackgroundService
{
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Heartbeat service starting.");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Heartbeat service stopped during initial delay.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await registryClient.SendHeartbeatAsync();
                await Task.Delay(_heartbeatInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unhandled error occurred in the heartbeat service loop.");
                
                try
                {
                    await Task.Delay(_heartbeatInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        logger.LogInformation("Heartbeat service stopping.");
    }
}