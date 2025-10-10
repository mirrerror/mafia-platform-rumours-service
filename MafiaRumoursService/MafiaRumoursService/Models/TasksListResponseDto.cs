using System.Text.Json.Serialization;

namespace MafiaRumoursService.Models;

public class TasksListResponseDto
{
    [JsonPropertyName("tasks")]
    public List<TaskDto>? Tasks { get; set; }
}