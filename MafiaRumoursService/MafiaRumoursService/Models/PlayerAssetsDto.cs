using System.Text.Json.Serialization;

namespace MafiaRumoursService.Models;

public class PlayerAssetsDto
{
    [JsonPropertyName("hair")]
    public long? Hair { get; set; }

    [JsonPropertyName("shirt")]
    public long? Shirt { get; set; }

    [JsonPropertyName("pants")]
    public long? Pants { get; set; }

    [JsonPropertyName("accessories")]
    public List<long>? Accessories { get; set; }
}