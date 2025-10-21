using MafiaRumoursService.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace MafiaRumoursService.Tests;

public class HeartbeatServiceTests : IDisposable
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<ServiceRegistryClient> _mockRegistryClient;
    private readonly Mock<ILogger<HeartbeatService>> _mockLogger;
    private readonly Mock<ILogger<ServiceRegistryClient>> _mockRegistryLogger;
    private readonly HeartbeatService _service;

    private const string TestDiscoveryUrl = "http://test-discovery.com";

    public HeartbeatServiceTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        _mockRegistryLogger = new Mock<ILogger<ServiceRegistryClient>>();

        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);

        _mockRegistryClient = new Mock<ServiceRegistryClient>(_mockHttpClientFactory.Object, _mockRegistryLogger.Object) { CallBase = true };

        _mockRegistryClient.Setup(c => c.RegisterAsync()).Returns(Task.CompletedTask);
        _mockRegistryClient.Object.RegisterAsync().GetAwaiter().GetResult();

        _mockLogger = new Mock<ILogger<HeartbeatService>>();
        _service = new HeartbeatService(_mockRegistryClient.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", null);
    }

    [Fact]
    public async Task ExecuteAsync_LogsStart_AndCallsHeartbeat_AndStops()
    {
        using var cts = new CancellationTokenSource();
        _mockRegistryClient.Setup(c => c.SendHeartbeatAsync()).Returns(Task.CompletedTask);

        var startTask = _service.StartAsync(cts.Token);
         try { await Task.Delay(TimeSpan.FromSeconds(10.5), cts.Token); } catch (TaskCanceledException){}
        await cts.CancelAsync();
        try { await startTask; }
        catch (OperationCanceledException) { }
        catch (AggregateException ae) when (ae.InnerExceptions.Any(e => e is TaskCanceledException or OperationCanceledException)) { }

        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Heartbeat service starting.")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        _mockRegistryClient.Verify(c => c.SendHeartbeatAsync(), Times.AtLeastOnce);
        
        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Heartbeat service stopping.")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_LogsError_WhenHeartbeatFails_AndStops()
    {
        using var cts = new CancellationTokenSource();
        var testException = new HttpRequestException("Heartbeat network failed");
        _mockRegistryClient.Setup(c => c.SendHeartbeatAsync()).ThrowsAsync(testException);

        var startTask = _service.StartAsync(cts.Token);
        try { await Task.Delay(TimeSpan.FromSeconds(10.5), cts.Token); } catch(TaskCanceledException){}
        await cts.CancelAsync();
        try { await startTask; }
        catch (OperationCanceledException) { }
        catch (AggregateException ae) when (ae.InnerExceptions.Any(e => e is TaskCanceledException or OperationCanceledException)) { }

        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("An unhandled error occurred in the heartbeat service loop.")),
                It.Is<Exception>(ex => ex == testException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
            
        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Heartbeat service stopping.")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
            
         _mockRegistryLogger.Verify(
             log => log.Log(
                 LogLevel.Error,
                 It.IsAny<EventId>(),
                 It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error occurred while sending heartbeat.")),
                 It.IsAny<Exception>(),
                 It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
             Times.Never);
    }


    [Fact]
    public async Task ExecuteAsync_StopsImmediately_WhenTokenIsCancelled()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var logger = new Mock<ILogger<HeartbeatService>>();
        var serviceClientMock = new Mock<ServiceRegistryClient>( Mock.Of<IHttpClientFactory>(), Mock.Of<ILogger<ServiceRegistryClient>>());

        var service = new HeartbeatService(serviceClientMock.Object, logger.Object);

        await service.StartAsync(cancellationTokenSource.Token);

        serviceClientMock.Verify(c => c.SendHeartbeatAsync(), Times.Never);
        
        logger.Verify(
            log => log.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Heartbeat service stopped during initial delay.")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
            
        logger.Verify(
            log => log.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Heartbeat service stopping.")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);

        logger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}