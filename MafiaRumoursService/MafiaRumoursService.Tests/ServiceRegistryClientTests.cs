using System.Net;
using MafiaRumoursService.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace MafiaRumoursService.Tests;

public class ServiceRegistryClientTests : IDisposable
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<ServiceRegistryClient>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;

    private const string TestDiscoveryUrl = "http://test-discovery-service.com";

    public ServiceRegistryClientTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<ServiceRegistryClient>>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", null);
        Environment.SetEnvironmentVariable("SERVICE_ID", null);
        Environment.SetEnvironmentVariable("SERVICE_HOST", null);
        Environment.SetEnvironmentVariable("SERVICE_PORT", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", null);
        Environment.SetEnvironmentVariable("SERVICE_ID", null);
        Environment.SetEnvironmentVariable("SERVICE_HOST", null);
        Environment.SetEnvironmentVariable("SERVICE_PORT", null);
    }

    private ServiceRegistryClient CreateClient()
    {
        return new ServiceRegistryClient(_mockHttpClientFactory.Object, _mockLogger.Object);
    }

    private async Task RegisterClientAsync(ServiceRegistryClient client)
    {
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains("register")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        
        await client.RegisterAsync();
        _mockLogger.Invocations.Clear();
    }

    [Fact]
    public void Constructor_UsesDefaults_WhenEnvVarsNotSet()
    {
        var client = CreateClient();

        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SERVICE_PORT not found or invalid")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_ReadsEnvVars_Correctly()
    {
        Environment.SetEnvironmentVariable("SERVICE_PORT", "8080");
        var client = CreateClient();

        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SERVICE_PORT not found or invalid")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_Skips_WhenDiscoveryUrlNotSet()
    {
        var client = CreateClient();

        await client.RegisterAsync();

        Assert.Null(client.InstanceId);
        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DISCOVERY_SERVICE_URL is not set")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_Succeeds_WhenApiCallIsSuccessful()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        await client.RegisterAsync();

        Assert.NotNull(client.InstanceId);
        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Service registered with discovery")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    [Fact]
    public async Task RegisterAsync_Fails_WhenApiCallFails()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Bad registration")
            });

        await client.RegisterAsync();

        Assert.Null(client.InstanceId);
        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to register service")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_Handles_Exception()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();
        var testException = new HttpRequestException("Network error");
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(testException);

        await client.RegisterAsync();

        Assert.Null(client.InstanceId);
        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error occurred during service registration")),
                testException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeregisterAsync_Skips_WhenNotRegistered()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();

        await client.DeregisterAsync();

        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task DeregisterAsync_Succeeds_WhenApiCallIsSuccessful()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();
        await RegisterClientAsync(client);
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Delete),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        await client.DeregisterAsync();

        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Service deregistered from discovery")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeregisterAsync_Fails_WhenApiCallFails()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();
        await RegisterClientAsync(client);

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Delete),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest));

        await client.DeregisterAsync();

        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to deregister service")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeregisterAsync_Handles_Exception()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();
        await RegisterClientAsync(client);
        var testException = new HttpRequestException("Network error");

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Delete),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(testException);

        await client.DeregisterAsync();

        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error occurred during service deregistration")),
                testException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendHeartbeatAsync_Skips_WhenNotRegistered()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();
        
        await client.SendHeartbeatAsync();
        
        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Skipping heartbeat. Service not registered")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendHeartbeatAsync_Succeeds_WhenApiCallIsSuccessful()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();
        await RegisterClientAsync(client);

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains("heartbeat")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        await client.SendHeartbeatAsync();

        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Heartbeat sent successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Attempting to re-register")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task SendHeartbeatAsync_AttemptsReRegistration_WhenApiCallFails()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => 
                    m.Method == HttpMethod.Post && 
                    m.RequestUri!.ToString().Contains("register")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        
        await client.RegisterAsync();
        _mockLogger.Verify(
            log => log.Log(LogLevel.Information, It.IsAny<EventId>(), It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Service registered")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => 
                    m.Method == HttpMethod.Post && 
                    m.RequestUri!.ToString().Contains("heartbeat")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => 
                    m.Method == HttpMethod.Post && 
                    m.RequestUri!.ToString().Contains("register")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        await client.SendHeartbeatAsync();
        
        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Heartbeat failed. Status code: NotFound")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Service registered")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SendHeartbeatAsync_Handles_Exception()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();
        await RegisterClientAsync(client);
        var testException = new HttpRequestException("Network error");

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains("heartbeat")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(testException);

        await client.SendHeartbeatAsync();

        _mockLogger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error occurred while sending heartbeat")),
                testException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}