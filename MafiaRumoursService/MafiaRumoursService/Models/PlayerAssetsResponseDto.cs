using System.Text.Json.Serialization;

namespace MafiaRumoursService.Models;

public class PlayerAssetsResponseDto
{
    [JsonPropertyName("assets")]
    public PlayerAssetsDto? Assets { get; set; }
}