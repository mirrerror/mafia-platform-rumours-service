using MafiaRumoursService.Data;
using MafiaRumoursService.Models;
using MafiaRumoursService.Services;
using Microsoft.EntityFrameworkCore;

namespace MafiaRumoursService.Tests;

public class PostgresRumourServiceTests
{
    private readonly DbContextOptions<RumoursDbContext> _dbContextOptions = new DbContextOptionsBuilder<RumoursDbContext>()
        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
        .Options;

    [Fact]
    public async Task CreateRumourAsync_ShouldCreateAndReturnRumour()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;
        const long targetId = 2;
        const string rumourType = "role";
        
        var result = await service.CreateRumourAsync(lobbyId, ownerId, targetId, rumourType);
        
        Assert.NotNull(result);
        Assert.Equal(lobbyId, result.LobbyId);
        Assert.Equal(ownerId, result.OwnerId);
        Assert.Equal(targetId, result.TargetId);
        Assert.Equal(rumourType, result.Type);
        Assert.Equal($"I heard a whisper that player {targetId} might be the... well, you know.", result.Text);

        var rumourInDb = await context.Rumours.FindAsync(result.Id);
        Assert.NotNull(rumourInDb);
        Assert.Equal(result.Id, rumourInDb.Id);
    }

    [Fact]
    public async Task GetRumoursByOwnerAsync_ShouldReturnRumoursForOwner()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;

        var rumours = new List<Rumour>
        {
            new() { LobbyId = lobbyId, OwnerId = ownerId, TargetId = 2, Type = "role", Text = "Test rumour 1", CreatedAt = DateTime.UtcNow },
            new() { LobbyId = lobbyId, OwnerId = ownerId, TargetId = 3, Type = "activity", Text = "Test rumour 2", CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new() { LobbyId = lobbyId, OwnerId = 2, TargetId = 4, Type = "allegiance", Text = "Test rumour 3", CreatedAt = DateTime.UtcNow }
        };

        await context.Rumours.AddRangeAsync(rumours);
        await context.SaveChangesAsync();
        
        var result = (await service.GetRumoursByOwnerAsync(lobbyId, ownerId)).ToList();
        
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(ownerId, r.OwnerId));
        Assert.True(result[0].CreatedAt > result[1].CreatedAt);
    }

    [Theory]
    [InlineData("role", 1, "I heard a whisper that player 1 might be the... well, you know.")]
    [InlineData("activity", 2, "Someone saw player 2 sneaking around last night.")]
    [InlineData("allegiance", 3, "Player 3 seems unusually friendly with the wrong crowd.")]
    [InlineData("unknown", 4, "There's a strange rumour going around about player 4.")]
    public async Task CreateRumourAsync_ShouldGenerateCorrectRumourText(string rumourType, long targetId, string expectedText)
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;

        var result = await service.CreateRumourAsync(lobbyId, ownerId, targetId, rumourType);

        Assert.Equal(expectedText, result.Text);
    }

    [Fact]
    public async Task GetRumoursByOwnerAsync_WhenNoRumoursExist_ShouldReturnEmptyList()
    {
        await using var context = new RumoursDbContext(_dbContextOptions);
        var service = new PostgresRumourService(context);
        const string lobbyId = "test-lobby";
        const long ownerId = 1;

        var result = await service.GetRumoursByOwnerAsync(lobbyId, ownerId);

        Assert.NotNull(result);
        Assert.Empty(result);
    }
}