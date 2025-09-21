using System.ComponentModel.DataAnnotations.Schema;

namespace MafiaRumoursService.Models;

public class Rumour
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }
    
    public required string LobbyId { get; set; }
    public required string Type { get; set; }
    public required long OwnerId { get; set; }
    public required long TargetId { get; set; }
    public required string Text { get; set; }
    public required DateTime CreatedAt { get; set; }
}