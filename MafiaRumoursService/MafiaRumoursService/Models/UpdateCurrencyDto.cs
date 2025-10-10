using System.Text.Json.Serialization;

namespace MafiaRumoursService.Models;

public class UpdateCurrencyDto
{
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;
}