using System.Text.Json.Serialization;

namespace MafiaRumoursService.Models;

public class ApiResponse<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; set; }
}