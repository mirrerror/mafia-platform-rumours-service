using MafiaRumoursService.Models;

namespace MafiaRumoursService.Middleware;

public class RequestThrottlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _requestTimeoutMilliseconds;
    private readonly ILogger<RequestThrottlingMiddleware> _logger;

    public RequestThrottlingMiddleware(RequestDelegate next, SemaphoreSlim semaphore, ILogger<RequestThrottlingMiddleware> logger)
    {
        _next = next;
        _semaphore = semaphore;
        _logger = logger;

        var timeoutSecondsStr = Environment.GetEnvironmentVariable("REQUEST_TIMEOUT_SECONDS") ?? "30";
        if (!int.TryParse(timeoutSecondsStr, out var timeoutSeconds))
        {
            timeoutSeconds = 30;
        }
        _requestTimeoutMilliseconds = timeoutSeconds * 1000;
        _logger.LogInformation("RequestThrottlingMiddleware initialized. Max Concurrent: {MaxConcurrentRequests}, Timeout: {Timeout}s", 
            semaphore.CurrentCount, timeoutSeconds);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        _logger.LogTrace("Request received for: {Path}. Waiting for semaphore...", path);

        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(1)))
        {
            _logger.LogWarning("Concurrency limit reached. Returning 503 for: {Path}. Current semaphore count: {CurrentCount}", path, _semaphore.CurrentCount);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new ErrorResponse("CONCURRENCY_LIMIT_REACHED", "The service is temporarily overloaded. Please try again later."));
            return;
        }

        _logger.LogTrace("Semaphore acquired for: {Path}. Current count: {CurrentCount}", path, _semaphore.CurrentCount);

        try
        {
            using var timeoutCts = new CancellationTokenSource(_requestTimeoutMilliseconds);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, context.RequestAborted);
                
            context.RequestAborted = linkedCts.Token;

            await _next(context);
        }
        catch (OperationCanceledException)
        {
            if (!context.Response.HasStarted)
            {
                _logger.LogWarning("Request timed out after {Timeout}ms for: {Path}", _requestTimeoutMilliseconds, path);
                context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
                await context.Response.WriteAsJsonAsync(new ErrorResponse("REQUEST_TIMEOUT", "The request took too long to process."));
            }
            else
            {
                _logger.LogWarning("Request timed out for {Path}, but response had already started.", path);
            }
        }
        finally
        {
            _semaphore.Release();
            _logger.LogTrace("Semaphore released for: {Path}. Current count: {CurrentCount}", path, _semaphore.CurrentCount);
        }
    }
}