using System.Net;
using MafiaRumoursService.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace MafiaRumoursService.Tests;

public class HeartbeatServiceTests : IDisposable
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly ServiceRegistryClient _registryClient;
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

        _registryClient = new ServiceRegistryClient(_mockHttpClientFactory.Object, _mockRegistryLogger.Object);

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains("register")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        
        _registryClient.RegisterAsync().GetAwaiter().GetResult();

        _mockLogger = new Mock<ILogger<HeartbeatService>>();
        _service = new HeartbeatService(_registryClient, _mockLogger.Object);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", null);
    }

    [Fact]
    public async Task ExecuteAsync_LogsStart_AndCallsHeartbeat()
    {
        using var cts = new CancellationTokenSource();

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains("heartbeat")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var executeTask = _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(10.5), cts.Token); 
        await cts.CancelAsync();
        
        try { await executeTask; }
        catch (OperationCanceledException) { }

        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Heartbeat service starting.")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains("heartbeat")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_LogsError_WhenHeartbeatFails()
    {
        using var cts = new CancellationTokenSource();
        var testException = new Exception("Heartbeat network failed");

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains("heartbeat")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(testException);

        var executeTask = _service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(10.5), cts.Token);
        await cts.CancelAsync();
        
        try { await executeTask; }
        catch (OperationCanceledException) { }

        _mockRegistryLogger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error occurred while sending heartbeat.")),
                testException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
        
        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("An unhandled error occurred in the heartbeat service loop.")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}