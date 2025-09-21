using MafiaRumoursService.Data;
using MafiaRumoursService.Models;
using Microsoft.EntityFrameworkCore;

namespace MafiaRumoursService.Services;

public class PostgresRumourService(RumoursDbContext dbContext) : IRumourService
{
    public async Task<Rumour> CreateRumourAsync(string lobbyId, long ownerId, long targetId, string type)
    {
        var rumour = new Rumour
        {
            LobbyId = lobbyId,
            OwnerId = ownerId,
            TargetId = targetId,
            Type = type,
            Text = GenerateRumourText(type, targetId),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Rumours.Add(rumour);
        await dbContext.SaveChangesAsync();

        return rumour;
    }

    public async Task<IEnumerable<Rumour>> GetRumoursByOwnerAsync(string lobbyId, long ownerId)
    {
        return await dbContext.Rumours
            .Where(r => r.LobbyId == lobbyId && r.OwnerId == ownerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    private string GenerateRumourText(string rumourType, long targetId)
    {
        // TODO: Replace with a more sophisticated rumour generation logic, potentially involving another service.
        return rumourType.ToLower() switch
        {
            "role" => $"I heard a whisper that player {targetId} might be the... well, you know.",
            "activity" => $"Someone saw player {targetId} sneaking around last night.",
            "allegiance" => $"Player {targetId} seems unusually friendly with the wrong crowd.",
            _ => $"There's a strange rumour going around about player {targetId}."
        };
    }
}