using MafiaRumoursService.Data;
using MafiaRumoursService.Exceptions;
using MafiaRumoursService.Models;
using Microsoft.EntityFrameworkCore;

namespace MafiaRumoursService.Services;

public class PostgresRumourService(
    RumoursDbContext dbContext, 
    HttpClient httpClient, 
    ILogger<PostgresRumourService> logger
) : IRumourService
{
    public async Task<Rumour> CreateRumourAsync(string lobbyId, long gameId, long ownerId, long targetId, string type)
    {
        logger.LogInformation(
            "Creating rumour. Lobby: {LobbyId}, Game: {GameId}, Owner: {OwnerId}, Target: {TargetId}, Type: {Type}",
            lobbyId, gameId, ownerId, targetId, type);
        
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
        logger.LogInformation("Rumour {RumourId} saved to database.", rumour.Id);

        return rumour;
    }

    private async Task<object?> GetExternalRumourData(string rumourType, long targetId, long gameId)
    {
        var gatewayServiceUrl = Environment.GetEnvironmentVariable("GATEWAY_SERVICE_URL");
        if (string.IsNullOrEmpty(gatewayServiceUrl))
        {
            logger.LogWarning("GATEWAY_SERVICE_URL is not set. Cannot fetch external data for rumour.");
            return null;
        }

        try
        {
            string? url;
            switch (rumourType.ToLower())
            {
                case "activity":
                    url = $"{gatewayServiceUrl}/api/tasks/player/{targetId}/tasks?gameId={gameId}";
                    logger.LogDebug("Fetching external data for 'activity' rumour from: {Url}", url);
                    var taskResponse = await httpClient.GetAsync(url);
                    if (!taskResponse.IsSuccessStatusCode)
                    {
                        logger.LogWarning("Failed to fetch 'activity' data. Status: {StatusCode}, URL: {Url}", taskResponse.StatusCode, url);
                        return null;
                    }

                    var taskApiResponse = await taskResponse.Content.ReadFromJsonAsync<ApiResponse<TasksListResponseDto>>();
                    logger.LogDebug("Successfully fetched {TaskCount} tasks for 'activity' rumour.", taskApiResponse?.Data?.Tasks?.Count ?? 0);
                    return taskApiResponse?.Data?.Tasks;

                case "appearance":
                    url = $"{gatewayServiceUrl}/api/character/{targetId}/appearance";
                    logger.LogDebug("Fetching external data for 'appearance' rumour from: {Url}", url);
                    var appearanceResponse = await httpClient.GetAsync(url);
                    if (!appearanceResponse.IsSuccessStatusCode)
                    {
                        logger.LogWarning("Failed to fetch 'appearance' data. Status: {StatusCode}, URL: {Url}", appearanceResponse.StatusCode, url);
                        return null;
                    }

                    var appearanceApiResponse = await appearanceResponse.Content.ReadFromJsonAsync<ApiResponse<PlayerAssetsResponseDto>>();
                    logger.LogDebug("Successfully fetched 'appearance' data for Target: {TargetId}", targetId);
                    return appearanceApiResponse?.Data?.Assets;

                default:
                    logger.LogWarning("No external data fetch logic for rumour type: {RumourType}", rumourType);
                    return null;
            }
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e, "Error fetching external data for rumour type '{RumourType}' from Gateway.", rumourType);
            return null;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An unexpected error occurred while fetching external data for rumour type '{RumourType}'.", rumourType);
            return null;
        }
    }
    
    public async Task<IEnumerable<Rumour>> GetRumoursByOwnerAsync(string lobbyId, long ownerId)
    {
        logger.LogDebug("Querying database for rumours. Lobby: {LobbyId}, Owner: {OwnerId}", lobbyId, ownerId);
        return await dbContext.Rumours
            .Where(r => r.LobbyId == lobbyId && r.OwnerId == ownerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    private string GenerateRumourText(string rumourType, long targetId, object? externalData)
    {
        logger.LogDebug("Generating rumour text for Type: {RumourType}, Target: {TargetId}", rumourType, targetId);
        switch (rumourType.ToLower())
        {
            case "activity":
                if (externalData is List<TaskDto> tasks && tasks.Count != 0)
                {
                    return GenerateActivityRumour(targetId, tasks);
                }
                logger.LogInformation("No 'activity' data found for Target: {TargetId}. Using default text.", targetId);
                return $"I heard player {targetId} is up to something, but I don't have the details.";
            case "appearance":
                if (externalData is PlayerAssetsDto assets)
                {
                    return GenerateAppearanceRumour(targetId, assets);
                }
                logger.LogInformation("No 'appearance' data found for Target: {TargetId}. Using default text.", targetId);
                return $"Player {targetId} is trying to blend in, but their disguise is impeccable.";
            default:
                logger.LogWarning("Attempted to generate rumour text for unknown type: {RumourType}", rumourType);
                throw new RumourTypeNotFoundException("Rumours type not found");
        }
    }

    private string GenerateActivityRumour(long targetId, List<TaskDto> tasks)
    {
        var task = tasks[Random.Shared.Next(tasks.Count)];
        logger.LogDebug("Generating 'activity' rumour text using Task: {TaskName} at {Location}", task.Name, task.Location);

        string[] templates = [
            $"Someone saw player {targetId} at the {task.Location}, pretending to '{task.Name}'. What were they really doing?",
            $"Word on the street is player {targetId} was very busy with '{task.Description}' at the {task.Location}.",
            $"I wouldn't trust player {targetId}. They were lurking around the {task.Location} all day."
        ];

        return templates[Random.Shared.Next(templates.Length)];
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
            assetList.Add(new KeyValuePair<string, long?>("accessory", assets.Accessories[Random.Shared.Next(assets.Accessories.Count)]));
        }

        if (assetList.Count == 0)
        {
             logger.LogDebug("No specific assets found for 'appearance' rumour on Target: {TargetId}. Using plain text.", targetId);
             return $"Player {targetId} has a very plain appearance, almost too plain if you ask me.";
        }
        
        var (slot, assetId) = assetList[Random.Shared.Next(assetList.Count)];
        logger.LogDebug("Generating 'appearance' rumour text using AssetSlot: {Slot}, AssetId: {AssetId}", slot, assetId);

        string[] templates = [
            $"Did you see the odd {slot} player {targetId} was wearing? It had ID {assetId}. Very suspicious.",
            $"Player {targetId}'s choice of {slot} (ID: {assetId}) is... interesting. Makes you wonder.",
            $"I'm not saying anything, but player {targetId}'s {slot} (ID: {assetId}) looks just like the one the culprit was described wearing."
        ];

        return templates[Random.Shared.Next(templates.Length)];
    }
}