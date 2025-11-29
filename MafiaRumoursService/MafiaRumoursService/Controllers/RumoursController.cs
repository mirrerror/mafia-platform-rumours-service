using MafiaRumoursService.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using MafiaRumoursService.Services;
using MafiaRumoursService.Protos;

namespace MafiaRumoursService.Controllers;

[ApiController]
[Route("api/rumours")]
public class RumoursController(
    MessageBrokerService.MessageBrokerServiceClient messageBrokerClient,
    IRumourService rumourService, 
    ILogger<RumoursController> logger
) : ControllerBase
{
    private const string TargetTopic = "user-management"; 
    
    private const int RumourCost = 150;
    private const string RumourCurrency = "coins";

    [HttpPost("{lobbyId}/purchase")]
    public async Task<IActionResult> PurchaseRumour(string lobbyId, [FromBody] PurchaseRumourDto purchaseRumourDto)
    {
        logger.LogInformation(
            "Initiating 2PC for Rumour Purchase. Lobby: {LobbyId}, Sender: {SenderId}", 
            lobbyId, purchaseRumourDto.SenderId);

        var transactionPayload = new 
        {
            action = "2PC_PREPARE", 
            
            user_id = purchaseRumourDto.SenderId,
            currency = RumourCurrency,
            amount = RumourCost,
            operation = "subtract",

            LobbyId = lobbyId,
            GameId = purchaseRumourDto.GameId,
            OwnerId = purchaseRumourDto.SenderId,
            TargetId = purchaseRumourDto.TargetId,
            Type = purchaseRumourDto.RumourType
        };

        var payloadString = JsonSerializer.Serialize(transactionPayload);

        try
        {
            var request = new PublishRequest
            {
                TopicName = TargetTopic,
                Payload = payloadString
            };

            logger.LogDebug("Sending 2PC Union Payload to Broker: {Payload}", payloadString);
            var response = await messageBrokerClient.PublishMessageAsync(request);

            return Accepted(new { 
                message = "Transaction initiated", 
                transactionId = response.MessageId,
                cost = RumourCost,
                currency = RumourCurrency
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to contact Message Broker.");
            return StatusCode(503, new ErrorResponse("BROKER_UNAVAILABLE", "Could not initiate transaction."));
        }
    }

    [HttpGet("{lobbyId}/user/{ownerId:long}")]
    public async Task<IActionResult> GetUserRumours(string lobbyId, long ownerId)
    {
        logger.LogInformation("Fetching rumours for Lobby: {LobbyId}, Owner: {OwnerId}", lobbyId, ownerId);
        var rumours = await rumourService.GetRumoursByOwnerAsync(lobbyId, ownerId);
        return Ok(new ApiResponse<IEnumerable<Rumour>> { Data = rumours });
    }
}