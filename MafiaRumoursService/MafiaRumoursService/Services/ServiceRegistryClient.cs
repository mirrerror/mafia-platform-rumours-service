using System.Net;

namespace MafiaRumoursService.Services;

public class ServiceRegistryClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ServiceRegistryClient> _logger;
    private readonly string? _discoveryUrl;
    private readonly string _serviceId;
    private readonly string _serviceHost;
    private readonly int _servicePort;

    public string? InstanceId { get; private set; }

    public ServiceRegistryClient(IHttpClientFactory httpClientFactory, ILogger<ServiceRegistryClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _discoveryUrl = Environment.GetEnvironmentVariable("DISCOVERY_SERVICE_URL");
        _serviceId = Environment.GetEnvironmentVariable("SERVICE_ID") ?? "mafia-rumours-service";
        
        _serviceHost = "localhost";
        var hostnameFromEnv = Environment.GetEnvironmentVariable("HOSTNAME");

        if (!string.IsNullOrEmpty(hostnameFromEnv))
        {
            _serviceHost = hostnameFromEnv;
             _logger.LogInformation("Resolved hostname from HOSTNAME environment variable: {Hostname}", _serviceHost);
        }
        else
        {
            try
            {
                _serviceHost = Dns.GetHostName();
                _logger.LogInformation("Resolved hostname using DNS: {Hostname}", _serviceHost);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve hostname using DNS. Defaulting to 'localhost'.");
            }
        }

        var portStr = Environment.GetEnvironmentVariable("SERVICE_PORT");
        if (!int.TryParse(portStr, out _servicePort))
        {
            _servicePort = 8080;
            _logger.LogWarning("SERVICE_PORT not found or invalid in environment variables. Defaulting to {DefaultPort}.", _servicePort);
        }

        _logger.LogInformation("ServiceRegistryClient configured with ServiceId: {ServiceId}, Host: {ServiceHost}, Port: {ServicePort}", _serviceId, _serviceHost, _servicePort);
    }

    public virtual async Task RegisterAsync()
    {
        if (string.IsNullOrEmpty(_discoveryUrl))
        {
            _logger.LogWarning("DISCOVERY_SERVICE_URL is not set. Skipping service registration.");
            return;
        }

        InstanceId = Guid.NewGuid().ToString();
        var payload = new
        {
            serviceId = _serviceId,
            instanceId = InstanceId,
            host = _serviceHost,
            port = _servicePort
        };

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsJsonAsync($"{_discoveryUrl}/api/discovery/register", payload);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Service registered with discovery. Instance ID: {InstanceId}", InstanceId);
            }
            else
            {
                InstanceId = null;
                _logger.LogError("Failed to register service. Status: {ResponseStatusCode}. Body: {ReadAsStringAsync}", response.StatusCode, await response.Content.ReadAsStringAsync());
            }
        }
        catch (Exception ex)
        {
            InstanceId = null;
            _logger.LogError(ex, "Error occurred during service registration.");
        }
    }

    public virtual async Task DeregisterAsync()
    {
        if (string.IsNullOrEmpty(InstanceId) || string.IsNullOrEmpty(_discoveryUrl))
        {
            _logger.LogDebug("Skipping deregistration. Service not registered or discovery URL not set.");
            return;
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.DeleteAsync($"{_discoveryUrl}/api/discovery/deregister/{InstanceId}");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Service deregistered from discovery.");
                InstanceId = null;
            }
            else
            {
                _logger.LogError("Failed to deregister service. Status code: {ResponseStatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during service deregistration.");
        }
    }
}