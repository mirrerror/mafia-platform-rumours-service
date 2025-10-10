using System.ComponentModel.DataAnnotations;

namespace MafiaRumoursService.Models;

public class PurchaseRumourDto
{
    [Required]
    public long GameId { get; set; }

    [Required]
    public long SenderId { get; set; }

    [Required]
    public long TargetId { get; set; }

    [Required]
    public string RumourType { get; set; } = string.Empty;
}