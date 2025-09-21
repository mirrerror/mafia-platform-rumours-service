using MafiaRumoursService.Data;
using MafiaRumoursService.Models;
using Microsoft.EntityFrameworkCore;

namespace MafiaRumoursService.Tests;

public class RumoursDbContextTests
{
    [Fact]
    public void RumoursDbContext_ShouldHaveRumoursDbSet()
    {
        var options = new DbContextOptionsBuilder<RumoursDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new RumoursDbContext(options);
        
        Assert.NotNull(context.Rumours);
    }

    [Fact]
    public async Task RumoursDbContext_ShouldPersistRumours()
    {
        var options = new DbContextOptionsBuilder<RumoursDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new RumoursDbContext(options);
        
        var rumour = new Rumour
        {
            LobbyId = "test-lobby",
            Type = "role",
            OwnerId = 1,
            TargetId = 2,
            Text = "Test rumour",
            CreatedAt = DateTime.UtcNow
        };

        context.Rumours.Add(rumour);
        await context.SaveChangesAsync();

        var retrievedRumour = await context.Rumours.FindAsync(rumour.Id);
        Assert.NotNull(retrievedRumour);
        Assert.Equal(rumour.LobbyId, retrievedRumour.LobbyId);
    }
}