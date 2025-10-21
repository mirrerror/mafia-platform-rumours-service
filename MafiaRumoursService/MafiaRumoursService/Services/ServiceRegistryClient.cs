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
        _serviceHost = Environment.GetEnvironmentVariable("SERVICE_HOST") ?? "localhost";
            
        var portStr = Environment.GetEnvironmentVariable("SERVICE_PORT");
        if (int.TryParse(portStr, out _servicePort)) return;
        _servicePort = 5000; 
        _logger.LogWarning("SERVICE_PORT not found or invalid in .env. Defaulting to 5000.");
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
            return;
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.DeleteAsync($"{_discoveryUrl}/api/discovery/deregister/{InstanceId}");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Service deregistered from discovery.");
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

    public virtual async Task SendHeartbeatAsync()
    {
        if (string.IsNullOrEmpty(InstanceId) || string.IsNullOrEmpty(_discoveryUrl))
        {
            _logger.LogWarning("Skipping heartbeat. Service not registered or discovery URL not set.");
            return; 
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsync($"{_discoveryUrl}/api/discovery/heartbeat/{InstanceId}", null);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Heartbeat sent successfully.");
            }
            else
            {
                _logger.LogWarning("Heartbeat failed. Status code: {ResponseStatusCode}. Attempting to re-register...", response.StatusCode);
                await RegisterAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while sending heartbeat.");
        }
    }
}