using System.Net;
using System.Text;
using System.Text.Json;
using MafiaRumoursService.Data;
using MafiaRumoursService.Exceptions;
using MafiaRumoursService.Models;
using MafiaRumoursService.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.Protected;

namespace MafiaRumoursService.Tests;

public class PostgresRumourServiceTests
{
    private readonly DbContextOptions<RumoursDbContext> _dbContextOptions = new DbContextOptionsBuilder<RumoursDbContext>()
        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
        .Options;

    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();
    private readonly HttpClient _httpClient;

    public PostgresRumourServiceTests()
    {
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
    }

    [Fact]
    public async Task CreateRumourAsync_ShouldThrowExceptionForUnknownType()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context, _httpClient);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;
        const long targetId = 2;
        const string rumourType = "role";

        await Assert.ThrowsAsync<RumourTypeNotFoundException>(() => service.CreateRumourAsync(lobbyId, ownerId, targetId, rumourType));
    }

    [Fact]
    public async Task GetRumoursByOwnerAsync_ShouldReturnRumoursForOwner()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context, _httpClient);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;

        var rumours = new List<Rumour>
        {
            new() { Id = 1, LobbyId = lobbyId, OwnerId = ownerId, TargetId = 2, Type = "role", Text = "Test rumour 1", CreatedAt = DateTime.UtcNow },
            new() { Id = 2, LobbyId = lobbyId, OwnerId = ownerId, TargetId = 3, Type = "activity", Text = "Test rumour 2", CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new() { Id = 3, LobbyId = lobbyId, OwnerId = 2, TargetId = 4, Type = "allegiance", Text = "Test rumour 3", CreatedAt = DateTime.UtcNow }
        };

        await context.Rumours.AddRangeAsync(rumours);
        await context.SaveChangesAsync();

        var result = (await service.GetRumoursByOwnerAsync(lobbyId, ownerId)).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(ownerId, r.OwnerId));
        Assert.True(result[0].CreatedAt > result[1].CreatedAt);
    }

    [Fact]
    public async Task GetRumoursByOwnerAsync_WhenNoRumoursExist_ShouldReturnEmptyList()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context, _httpClient);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;

        var result = await service.GetRumoursByOwnerAsync(lobbyId, ownerId);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateRumourAsync_WithSuccessfulActivityApiCall_ShouldGenerateActivityRumour()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context, _httpClient);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;
        const long targetId = 2;
        const string rumourType = "activity";

        var apiResponse = new ApiResponse<Dictionary<string, List<TaskDto>>>
        {
            Data = new Dictionary<string, List<TaskDto>>
            {
                { "tasks", [new TaskDto { Name = "Test Task", Description = "Test Description", Location = "Test Location" }] }
            }
        };
        var json = JsonSerializer.Serialize(apiResponse);
        var httpResponse = new HttpResponseMessage {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        Environment.SetEnvironmentVariable("TASK_SERVICE_URL", "http://localhost:8080");

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var result = await service.CreateRumourAsync(lobbyId, ownerId, targetId, rumourType);

        Assert.NotNull(result);
        Assert.Contains($"player {targetId}", result.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRumourAsync_WithFailedActivityApiCall_ShouldGenerateDefaultRumour()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context, _httpClient);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;
        const long targetId = 2;
        const string rumourType = "activity";

        var httpResponse = new HttpResponseMessage {
            StatusCode = HttpStatusCode.InternalServerError
        };

        Environment.SetEnvironmentVariable("TASK_SERVICE_URL", "http://localhost:8080");

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var result = await service.CreateRumourAsync(lobbyId, ownerId, targetId, rumourType);

        Assert.NotNull(result);
        Assert.Equal($"I heard player {targetId} is up to something, but I don't have the details.", result.Text);
    }

    [Fact]
    public async Task CreateRumourAsync_WithSuccessfulAppearanceApiCall_ShouldGenerateAppearanceRumour()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context, _httpClient);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;
        const long targetId = 2;
        const string rumourType = "appearance";

        var apiResponse = new ApiResponse<AppearanceDataDto>
        {
            Data = new AppearanceDataDto
            {
                Assets = new Dictionary<string, object>
                {
                    { "hat", "fedora" }
                }
            }
        };
        var json = JsonSerializer.Serialize(apiResponse);
        var httpResponse = new HttpResponseMessage {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        Environment.SetEnvironmentVariable("CHARACTER_SERVICE_URL", "http://localhost:8081");

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var result = await service.CreateRumourAsync(lobbyId, ownerId, targetId, rumourType);

        Assert.NotNull(result);
        Assert.Contains($"player {targetId}", result.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRumourAsync_WithFailedAppearanceApiCall_ShouldGenerateDefaultRumour()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context, _httpClient);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;
        const long targetId = 2;
        const string rumourType = "appearance";

        var httpResponse = new HttpResponseMessage {
            StatusCode = HttpStatusCode.InternalServerError
        };

        Environment.SetEnvironmentVariable("CHARACTER_SERVICE_URL", "http://localhost:8081");

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var result = await service.CreateRumourAsync(lobbyId, ownerId, targetId, rumourType);

        Assert.NotNull(result);
        Assert.Equal($"Player {targetId} is trying to blend in, but their disguise is impeccable.", result.Text);
    }

    [Fact]
    public async Task CreateRumourAsync_WithNoEnvVar_ShouldGenerateDefaultRumour()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context, _httpClient);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;
        const long targetId = 2;
        const string rumourType = "activity";

        Environment.SetEnvironmentVariable("TASK_SERVICE_URL", null);

        var result = await service.CreateRumourAsync(lobbyId, ownerId, targetId, rumourType);

        Assert.NotNull(result);
        Assert.Equal($"I heard player {targetId} is up to something, but I don't have the details.", result.Text);
    }

    [Fact]
    public async Task CreateRumourAsync_WithHttpException_ShouldGenerateDefaultRumour()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context, _httpClient);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;
        const long targetId = 2;
        const string rumourType = "activity";

        Environment.SetEnvironmentVariable("TASK_SERVICE_URL", "http://localhost:8080");

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var result = await service.CreateRumourAsync(lobbyId, ownerId, targetId, rumourType);

        Assert.NotNull(result);
        Assert.Equal($"I heard player {targetId} is up to something, but I don't have the details.", result.Text);
    }

    [Fact]
    public async Task CreateRumourAsync_WithEmptyTasks_ShouldGenerateDefaultRumour()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context, _httpClient);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;
        const long targetId = 2;
        const string rumourType = "activity";

        var apiResponse = new ApiResponse<Dictionary<string, List<TaskDto>>>
        {
            Data = new Dictionary<string, List<TaskDto>> { { "tasks", [] } }
        };
        var json = JsonSerializer.Serialize(apiResponse);
        var httpResponse = new HttpResponseMessage {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        Environment.SetEnvironmentVariable("TASK_SERVICE_URL", "http://localhost:8080");

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var result = await service.CreateRumourAsync(lobbyId, ownerId, targetId, rumourType);

        Assert.NotNull(result);
        Assert.Equal($"I heard player {targetId} is up to something, but I don't have the details.", result.Text);
    }

    [Fact]
    public async Task CreateRumourAsync_WithEmptyAssets_ShouldGenerateDefaultRumour()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context, _httpClient);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;
        const long targetId = 2;
        const string rumourType = "appearance";

        var apiResponse = new ApiResponse<AppearanceDataDto>
        {
            Data = new AppearanceDataDto { Assets = new Dictionary<string, object>() }
        };
        var json = JsonSerializer.Serialize(apiResponse);
        var httpResponse = new HttpResponseMessage {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        Environment.SetEnvironmentVariable("CHARACTER_SERVICE_URL", "http://localhost:8081");

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var result = await service.CreateRumourAsync(lobbyId, ownerId, targetId, rumourType);

        Assert.NotNull(result);
        Assert.Equal($"Player {targetId} is trying to blend in, but their disguise is impeccable.", result.Text);
    }

    [Fact]
    public async Task CreateRumourAsync_WithUnknownType_ShouldThrowRumourTypeNotFoundException()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context, _httpClient);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;
        const long targetId = 2;
        const string rumourType = "unknown-type"; // An unsupported type

        await Assert.ThrowsAsync<RumourTypeNotFoundException>(() => service.CreateRumourAsync(lobbyId, ownerId, targetId, rumourType));
    }

    [Fact]
    public async Task CreateRumourAsync_WithNoCharacterServiceUrl_ShouldGenerateDefaultRumour()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context, _httpClient);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;
        const long targetId = 2;
        const string rumourType = "appearance";

        Environment.SetEnvironmentVariable("CHARACTER_SERVICE_URL", null);

        var result = await service.CreateRumourAsync(lobbyId, ownerId, targetId, rumourType);

        Assert.NotNull(result);
        Assert.Equal($"Player {targetId} is trying to blend in, but their disguise is impeccable.", result.Text);
    }

    [Fact]
    public async Task CreateRumourAsync_WithCharacterServiceHttpException_ShouldGenerateDefaultRumour()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context, _httpClient);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;
        const long targetId = 2;
        const string rumourType = "appearance";

        Environment.SetEnvironmentVariable("CHARACTER_SERVICE_URL", "http://localhost:8081");

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var result = await service.CreateRumourAsync(lobbyId, ownerId, targetId, rumourType);

        Assert.NotNull(result);
        Assert.Equal($"Player {targetId} is trying to blend in, but their disguise is impeccable.", result.Text);
    }
}