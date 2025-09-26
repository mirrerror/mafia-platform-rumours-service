namespace MafiaRumoursService.Models;

public class PurchaseRumourDto
{
    public required string RumourType { get; set; }
    public required long SenderId { get; set; }
    public required long TargetId { get; set; }
}