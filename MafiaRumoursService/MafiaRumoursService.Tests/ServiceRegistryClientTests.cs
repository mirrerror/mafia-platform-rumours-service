using Grpc.Core;
using MafiaRumoursService.Protos;
using MafiaRumoursService.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace MafiaRumoursService.Tests;

public class ServiceRegistryClientTests : IDisposable
{
    private readonly Mock<ILogger<ServiceRegistryClient>> _mockLogger;
    private readonly Mock<RegistrationService.RegistrationServiceClient> _mockGrpcClient;
    private readonly Mock<IServiceProvider> _mockServiceProvider;

    public ServiceRegistryClientTests()
    {
        _mockLogger = new Mock<ILogger<ServiceRegistryClient>>();
        _mockGrpcClient = new Mock<RegistrationService.RegistrationServiceClient>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        _mockServiceProvider
            .Setup(x => x.GetService(typeof(RegistrationService.RegistrationServiceClient)))
            .Returns(_mockGrpcClient.Object);

        ClearEnvVars();
    }

    public void Dispose()
    {
        ClearEnvVars();
        GC.SuppressFinalize(this);
    }

    private static void ClearEnvVars()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_GRPC_URL", null);
        Environment.SetEnvironmentVariable("SERVICE_ID", null);
        Environment.SetEnvironmentVariable("HOSTNAME", null);
        Environment.SetEnvironmentVariable("SERVICE_PORT", null);
        Environment.SetEnvironmentVariable("RPC_PORT", null);
        Environment.SetEnvironmentVariable("SUBSCRIBED_TOPICS", null);
    }

    private ServiceRegistryClient CreateClient()
    {
        return new ServiceRegistryClient(_mockLogger.Object, _mockServiceProvider.Object);
    }

    [Fact]
    public async Task RegisterAsync_Skips_WhenClientIsNull()
    {
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(RegistrationService.RegistrationServiceClient)))
            .Returns(null!);

        var client = CreateClient();

        await client.RegisterAsync();

        Assert.Null(client.InstanceId);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Skipping registration")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_Succeeds_WhenGrpcCallIsSuccessful()
    {
        var client = CreateClient();
        const string expectedInstanceId = "rumour-instance-1";

        var registerResponse = new RegisterResponse
        {
            InstanceId = expectedInstanceId,
            Status = "OK"
        };

        var mockCall = CreateAsyncUnaryCall(registerResponse);

        _mockGrpcClient
            .Setup(x => x.RegisterAsync(It.IsAny<RegisterRequest>(), null, null, CancellationToken.None))
            .Returns(mockCall);

        await client.RegisterAsync();

        Assert.Equal(expectedInstanceId, client.InstanceId);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Service registered")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_SendsCorrectMetadata()
    {
        Environment.SetEnvironmentVariable("SUBSCRIBED_TOPICS", "rumours-events");
        var client = CreateClient();

        var registerResponse = new RegisterResponse { InstanceId = "id", Status = "OK" };
        var mockCall = CreateAsyncUnaryCall(registerResponse);

        _mockGrpcClient
            .Setup(x => x.RegisterAsync(It.IsAny<RegisterRequest>(), null, null, CancellationToken.None))
            .Returns(mockCall)
            .Verifiable();

        await client.RegisterAsync();

        _mockGrpcClient.Verify(x => x.RegisterAsync(It.Is<RegisterRequest>(req => 
            req.Metadata.ContainsKey("language") && 
            req.Metadata["language"] == "csharp" &&
            req.Metadata.ContainsKey("subscribedTopics") && 
            req.Metadata["subscribedTopics"] == "rumours-events"
        ), null, null, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_Handles_RpcException()
    {
        var client = CreateClient();
        var rpcException = new RpcException(new Status(StatusCode.Unavailable, "Discovery Down"));

        _mockGrpcClient
            .Setup(x => x.RegisterAsync(It.IsAny<RegisterRequest>(), null, null, CancellationToken.None))
            .Throws(rpcException);

        await client.RegisterAsync();

        Assert.Null(client.InstanceId);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("gRPC Error during service registration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeregisterAsync_Succeeds()
    {
        var client = CreateClient();
        
        var registerResponse = new RegisterResponse { InstanceId = "inst-1", Status = "OK" };
        _mockGrpcClient.Setup(x => x.RegisterAsync(It.IsAny<RegisterRequest>(), null, null, CancellationToken.None))
            .Returns(CreateAsyncUnaryCall(registerResponse));
        await client.RegisterAsync();

        var deregisterResponse = new DeregisterResponse { Status = "OK" };
        _mockGrpcClient.Setup(x => x.DeregisterAsync(It.IsAny<DeregisterRequest>(), null, null, CancellationToken.None))
            .Returns(CreateAsyncUnaryCall(deregisterResponse));

        await client.DeregisterAsync();

        _mockGrpcClient.Verify(x => x.DeregisterAsync(It.Is<DeregisterRequest>(r => r.InstanceId == "inst-1"), null, null, CancellationToken.None), Times.Once);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Service deregistered")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WhenHostnameEnvVarMissing_ShouldNotThrow()
    {
        Environment.SetEnvironmentVariable("HOSTNAME", null);
        var client = CreateClient();
        Assert.NotNull(client); 
    }

    [Fact]
    public async Task DeregisterAsync_WhenInstanceIdIsNull_ShouldReturnImmediately()
    {
        var client = CreateClient();
        await client.DeregisterAsync();
        _mockGrpcClient.Verify(x => x.DeregisterAsync(It.IsAny<DeregisterRequest>(), null, null, CancellationToken.None), Times.Never);
    }

    private static AsyncUnaryCall<T> CreateAsyncUnaryCall<T>(T response)
    {
        return new AsyncUnaryCall<T>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => [],
            () => { });
    }
}