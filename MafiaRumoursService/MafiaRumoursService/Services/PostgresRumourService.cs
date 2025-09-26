using DotNetEnv;
using MafiaRumoursService.Data;
using MafiaRumoursService.Exceptions;
using MafiaRumoursService.Models;
using Microsoft.EntityFrameworkCore;

namespace MafiaRumoursService.Services;

public class PostgresRumourService(RumoursDbContext dbContext, HttpClient httpClient) : IRumourService
{
    private static readonly Random Random = new();

    public async Task<Rumour> CreateRumourAsync(string lobbyId, long ownerId, long targetId, string type)
    {
        var externalData = await GetExternalRumourData(type, targetId);

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

    private async Task<object?> GetExternalRumourData(string rumourType, long targetId)
    {
        var taskServiceUrl = Env.GetString("TASK_SERVICE_URL");
        var characterServiceUrl = Env.GetString("CHARACTER_SERVICE_URL");

        try
        {
            switch (rumourType.ToLower())
            {
                case "activity":
                    if (string.IsNullOrEmpty(taskServiceUrl)) return null;

                    var taskResponse = await httpClient.GetAsync($"{taskServiceUrl}/player/{targetId}/tasks?gameId=latest");
                    if (!taskResponse.IsSuccessStatusCode) return null;

                    var taskApiResponse = await taskResponse.Content.ReadFromJsonAsync<ApiResponse<Dictionary<string, List<TaskDto>>>>();
                    return taskApiResponse?.Data?["tasks"];

                case "appearance":
                    if (string.IsNullOrEmpty(characterServiceUrl)) return null;

                    var appearanceResponse = await httpClient.GetAsync($"{characterServiceUrl}/{targetId}/appearance");
                    if (!appearanceResponse.IsSuccessStatusCode) return null;

                    var appearanceApiResponse = await appearanceResponse.Content.ReadFromJsonAsync<ApiResponse<AppearanceDataDto>>();
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
                if (externalData is Dictionary<string, object> assets && assets.Count != 0)
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

    private string GenerateAppearanceRumour(long targetId, Dictionary<string, object> assets)
    {
        var (slot, asset) = assets.ElementAt(Random.Next(assets.Count));
        var assetName = asset.ToString();

        string[] templates = [
            $"Did you see the odd {slot} player {targetId} was wearing? Very suspicious.",
            $"Player {targetId}'s choice of {assetName} for their {slot} is... interesting. Makes you wonder.",
            $"I'm not saying anything, but player {targetId}'s {slot} looks just like the one the culprit was described wearing."
        ];

        return templates[Random.Next(templates.Length)];
    }
}