using MafiaRumoursService.Models;

namespace MafiaRumoursService.Middleware;

public class RequestThrottlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _requestTimeoutMilliseconds;

    public RequestThrottlingMiddleware(RequestDelegate next, SemaphoreSlim semaphore)
    {
        _next = next;
        _semaphore = semaphore;

        var timeoutSecondsStr = Environment.GetEnvironmentVariable("REQUEST_TIMEOUT_SECONDS") ?? "30";
        if (!int.TryParse(timeoutSecondsStr, out var timeoutSeconds))
        {
            timeoutSeconds = 30;
        }
        _requestTimeoutMilliseconds = timeoutSeconds * 1000;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(1)))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new ErrorResponse("CONCURRENCY_LIMIT_REACHED", "The service is temporarily overloaded. Please try again later."));
            return;
        }

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
                context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
                await context.Response.WriteAsJsonAsync(new ErrorResponse("REQUEST_TIMEOUT", "The request took too long to process."));
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}