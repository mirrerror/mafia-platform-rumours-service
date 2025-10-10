using MafiaRumoursService.Data;
using MafiaRumoursService.Exceptions;
using MafiaRumoursService.Models;
using Microsoft.EntityFrameworkCore;

namespace MafiaRumoursService.Services;

public class PostgresRumourService(RumoursDbContext dbContext, HttpClient httpClient) : IRumourService
{
    private static readonly Random Random = new();

    public async Task<Rumour> CreateRumourAsync(string lobbyId, long gameId, long ownerId, long targetId, string type)
    {
        var externalData = await GetExternalRumourData(type, targetId, gameId);

        var rumour = new Rumour
        {
            LobbyId = lobbyId,
            OwnerId = ownerId,
            TargetId = targetId,
            Type = type,
            Text = GenerateRumourText(type, targetId, externalData),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Rumours.Add(rumour);
        await dbContext.SaveChangesAsync();

        return rumour;
    }

    private async Task<object?> GetExternalRumourData(string rumourType, long targetId, long gameId)
    {
        var gatewayServiceUrl = Environment.GetEnvironmentVariable("GATEWAY_SERVICE_URL");
        if (string.IsNullOrEmpty(gatewayServiceUrl)) return null;

        try
        {
            switch (rumourType.ToLower())
            {
                case "activity":
                    var taskResponse = await httpClient.GetAsync($"{gatewayServiceUrl}/api/tasks/player/{targetId}/tasks?gameId={gameId}");
                    if (!taskResponse.IsSuccessStatusCode) return null;

                    var taskApiResponse = await taskResponse.Content.ReadFromJsonAsync<ApiResponse<TasksListResponseDto>>();
                    return taskApiResponse?.Data?.Tasks;

                case "appearance":
                    var appearanceResponse = await httpClient.GetAsync($"{gatewayServiceUrl}/api/character/{targetId}/appearance");
                    if (!appearanceResponse.IsSuccessStatusCode) return null;

                    var appearanceApiResponse = await appearanceResponse.Content.ReadFromJsonAsync<ApiResponse<PlayerAssetsResponseDto>>();
                    return appearanceApiResponse?.Data?.Assets;

                default:
                    return null;
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Error fetching external data for rumour type '{rumourType}': {e.Message}");
            return null;
        }
    }
    
    public async Task<IEnumerable<Rumour>> GetRumoursByOwnerAsync(string lobbyId, long ownerId)
    {
        return await dbContext.Rumours
            .Where(r => r.LobbyId == lobbyId && r.OwnerId == ownerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    private string GenerateRumourText(string rumourType, long targetId, object? externalData)
    {
        switch (rumourType.ToLower())
        {
            case "activity":
                if (externalData is List<TaskDto> tasks && tasks.Count != 0)
                {
                    return GenerateActivityRumour(targetId, tasks);
                }
                return $"I heard player {targetId} is up to something, but I don't have the details.";
            case "appearance":
                if (externalData is PlayerAssetsDto assets)
                {
                    return GenerateAppearanceRumour(targetId, assets);
                }
                return $"Player {targetId} is trying to blend in, but their disguise is impeccable.";
            default:
                throw new RumourTypeNotFoundException("Rumours type not found");
        }
    }

    private string GenerateActivityRumour(long targetId, List<TaskDto> tasks)
    {
        var task = tasks[Random.Next(tasks.Count)];

        string[] templates = [
            $"Someone saw player {targetId} at the {task.Location}, pretending to '{task.Name}'. What were they really doing?",
            $"Word on the street is player {targetId} was very busy with '{task.Description}' at the {task.Location}.",
            $"I wouldn't trust player {targetId}. They were lurking around the {task.Location} all day."
        ];

        return templates[Random.Next(templates.Length)];
    }

    private string GenerateAppearanceRumour(long targetId, PlayerAssetsDto assets)
    {
        var assetList = new List<KeyValuePair<string, long?>>
        {
            new("hair", assets.Hair),
            new("shirt", assets.Shirt),
            new("pants", assets.Pants)
        }.Where(kv => kv.Value.HasValue).ToList();

        if (assets.Accessories?.Count > 0)
        {
            assetList.Add(new KeyValuePair<string, long?>("accessory", assets.Accessories[Random.Next(assets.Accessories.Count)]));
        }

        if (assetList.Count == 0)
        {
             return $"Player {targetId} has a very plain appearance, almost too plain if you ask me.";
        }
        
        var randomAsset = assetList[Random.Next(assetList.Count)];
        var slot = randomAsset.Key;
        var assetId = randomAsset.Value;

        string[] templates = [
            $"Did you see the odd {slot} player {targetId} was wearing? It had ID {assetId}. Very suspicious.",
            $"Player {targetId}'s choice of {slot} (ID: {assetId}) is... interesting. Makes you wonder.",
            $"I'm not saying anything, but player {targetId}'s {slot} looks just like the one the culprit was described wearing."
        ];

        return templates[Random.Next(templates.Length)];
    }
}