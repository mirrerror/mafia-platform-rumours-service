using System.Text.Json.Serialization;

namespace MafiaRumoursService.Models;

public class AppearanceDataDto
{
    [JsonPropertyName("assets")]
    public Dictionary<string, object> Assets { get; set; } = [];
}