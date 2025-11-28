using Grpc.Core;
using MafiaRumoursService.Protos;
using System.Net;

namespace MafiaRumoursService.Services;

public class ServiceRegistryClient
{
    private readonly ILogger<ServiceRegistryClient> _logger;
    private readonly RegistrationService.RegistrationServiceClient? _grpcClient;
    
    private readonly string _serviceId;
    private readonly string _serviceHost;
    private readonly int _restPort;
    private readonly int _rpcPort;
    private readonly string _interestedTopic;
    private readonly string _subscribedTopics;

    public string? InstanceId { get; private set; }

    public ServiceRegistryClient(ILogger<ServiceRegistryClient> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _grpcClient = serviceProvider.GetService<RegistrationService.RegistrationServiceClient>();

        _serviceId = Environment.GetEnvironmentVariable("SERVICE_ID") ?? "mafia-rumours-service";
        _interestedTopic = Environment.GetEnvironmentVariable("SERVICE_TOPIC") ?? "rumours-topic";
        _subscribedTopics = Environment.GetEnvironmentVariable("SUBSCRIBED_TOPICS") ?? "rumours-events";

        _serviceHost = "localhost";
        var hostnameFromEnv = Environment.GetEnvironmentVariable("HOSTNAME");

        if (!string.IsNullOrEmpty(hostnameFromEnv))
        {
            _serviceHost = hostnameFromEnv;
             _logger.LogInformation("Resolved hostname from HOSTNAME: {Hostname}", _serviceHost);
        }
        else
        {
            try
            {
                _serviceHost = Dns.GetHostName();
            }
            catch
            {
                // Ignore
            }
        }

        var portStr = Environment.GetEnvironmentVariable("SERVICE_PORT");
        if (!int.TryParse(portStr, out _restPort)) _restPort = 8080;

        var rpcPortStr = Environment.GetEnvironmentVariable("RPC_PORT");
        if (!int.TryParse(rpcPortStr, out _rpcPort)) _rpcPort = 6000;
        
        _logger.LogInformation("ServiceRegistryClient initialized. ServiceId: {Id}", _serviceId);
    }

    public virtual async Task RegisterAsync()
    {
        if (_grpcClient == null)
        {
            _logger.LogWarning("DISCOVERY_SERVICE_GRPC_URL not set or client null. Skipping registration.");
            return;
        }

        try
        {
            var request = new RegisterRequest
            {
                ServiceId = _serviceId,
                Host = _serviceHost,
                RestPort = _restPort,
                RpcPort = _rpcPort,
                TopicName = _interestedTopic
            };

            request.Metadata.Add("language", "csharp");

            if (!string.IsNullOrEmpty(_subscribedTopics))
            {
                request.Metadata.Add("subscribedTopics", _subscribedTopics);
            }

            _logger.LogInformation("Sending gRPC Registration with metadata (Topics: {Topics})...", _subscribedTopics);
            var response = await _grpcClient.RegisterAsync(request);

            InstanceId = response.InstanceId;
            _logger.LogInformation("Service registered with discovery. Instance ID: {InstanceId}", InstanceId);
        }
        catch (RpcException ex)
        {
            InstanceId = null;
            _logger.LogError(ex, "gRPC Error during service registration: {Status}", ex.Status);
        }
        catch (Exception ex)
        {
            InstanceId = null;
            _logger.LogError(ex, "Error occurred during service registration via gRPC.");
        }
    }

    public virtual async Task DeregisterAsync()
    {
        if (string.IsNullOrEmpty(InstanceId) || _grpcClient == null) return;

        try
        {
            var response = await _grpcClient.DeregisterAsync(new DeregisterRequest { InstanceId = InstanceId });
            _logger.LogInformation("Service deregistered. Status: {Status}", response.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during service deregistration.");
        }
    }
}