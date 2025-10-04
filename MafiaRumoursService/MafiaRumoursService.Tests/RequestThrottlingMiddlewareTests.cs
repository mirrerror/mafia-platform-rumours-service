using System.Reflection;
using MafiaRumoursService.Middleware;
using Microsoft.AspNetCore.Http;

namespace MafiaRumoursService.Tests;

public class RequestThrottlingMiddlewareTests
{
    public RequestThrottlingMiddlewareTests()
    {
        Environment.SetEnvironmentVariable("REQUEST_TIMEOUT_SECONDS", "1");
    }

    [Fact]
    public void Constructor_WhenEnvVarIsNotSet_UsesDefaultTimeout()
    {
        Environment.SetEnvironmentVariable("REQUEST_TIMEOUT_SECONDS", null);
        var semaphore = new SemaphoreSlim(1, 1);
        var nextDelegate = new RequestDelegate(_ => Task.CompletedTask);

        var middleware = new RequestThrottlingMiddleware(nextDelegate, semaphore);

        var timeoutField = typeof(RequestThrottlingMiddleware).GetField("_requestTimeoutMilliseconds", BindingFlags.NonPublic | BindingFlags.Instance);
        var timeoutValue = (int)timeoutField!.GetValue(middleware)!;
        Assert.Equal(30000, timeoutValue);
    }

    [Fact]
    public void Constructor_WhenEnvVarIsInvalid_UsesDefaultTimeout()
    {
        Environment.SetEnvironmentVariable("REQUEST_TIMEOUT_SECONDS", "invalid-value");
        var semaphore = new SemaphoreSlim(1, 1);
        var nextDelegate = new RequestDelegate(_ => Task.CompletedTask);

        var middleware = new RequestThrottlingMiddleware(nextDelegate, semaphore);

        var timeoutField = typeof(RequestThrottlingMiddleware).GetField("_requestTimeoutMilliseconds", BindingFlags.NonPublic | BindingFlags.Instance);
        var timeoutValue = (int)timeoutField!.GetValue(middleware)!;
        Assert.Equal(30000, timeoutValue);
    }

    [Fact]
    public async Task InvokeAsync_WhenConcurrencySlotIsAvailable_CallsNext()
    {
        Environment.SetEnvironmentVariable("REQUEST_TIMEOUT_SECONDS", "1");
        var semaphore = new SemaphoreSlim(1, 1);
        var middleware = new RequestThrottlingMiddleware(next: _ => Task.CompletedTask, semaphore: semaphore);
        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(200, httpContext.Response.StatusCode);
        Assert.Equal(1, semaphore.CurrentCount);
    }
    
    [Fact]
    public async Task InvokeAsync_WhenConcurrencyLimitIsReached_Returns503ServiceUnavailable()
    {
        var semaphore = new SemaphoreSlim(1, 1);
        await semaphore.WaitAsync();

        var middleware = new RequestThrottlingMiddleware(next: _ => Task.CompletedTask, semaphore: semaphore);
        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, httpContext.Response.StatusCode);
        
        semaphore.Release();
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestTimesOut_Returns408RequestTimeout()
    {
        var semaphore = new SemaphoreSlim(1, 1);
        var middleware = new RequestThrottlingMiddleware(
            next: async innerHttpContext =>
            {
                await Task.Delay(2000, innerHttpContext.RequestAborted);
            },
            semaphore: semaphore
        );
        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status408RequestTimeout, httpContext.Response.StatusCode);
        Assert.Equal(1, semaphore.CurrentCount);
    }
    
    [Fact]
    public async Task InvokeAsync_WhenSemaphoreIsReleased_OnExceptionInNextMiddleware()
    {
        var semaphore = new SemaphoreSlim(1, 1);
        var middleware = new RequestThrottlingMiddleware(
            next: _ => throw new InvalidOperationException("Test Exception"),
            semaphore: semaphore
        );
        var httpContext = new DefaultHttpContext();
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(httpContext));
        Assert.Equal(1, semaphore.CurrentCount);
    }
}