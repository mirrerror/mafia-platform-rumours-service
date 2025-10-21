using MafiaRumoursService.Exceptions;
using MafiaRumoursService.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using MafiaRumoursService.Services;

namespace MafiaRumoursService.Controllers;

[ApiController]
[Route("api/rumours")]
public class RumoursController(
    IHttpClientFactory httpClientFactory, 
    IRumourService rumourService, 
    ILogger<RumoursController> logger
) : ControllerBase
{
    private const int RumourCost = 150;
    private const string RumourCurrency = "coins";

    [HttpPost("{lobbyId}/purchase")]
    public async Task<IActionResult> PurchaseRumour(string lobbyId, [FromBody] PurchaseRumourDto purchaseRumourDto)
    {
        logger.LogInformation(
            "Attempting to purchase rumour in Lobby: {LobbyId} for Game: {GameId}. Sender: {SenderId}, Target: {TargetId}, Type: {RumourType}", 
            lobbyId, purchaseRumourDto.GameId, purchaseRumourDto.SenderId, purchaseRumourDto.TargetId, purchaseRumourDto.RumourType);

        var gatewayServiceUrl = Environment.GetEnvironmentVariable("GATEWAY_SERVICE_URL") ?? "http://localhost:8000";
        var httpClient = httpClientFactory.CreateClient();

        var currencyRequestPayload = new UpdateCurrencyDto
        {
            Currency = RumourCurrency,
            Amount = RumourCost,
            Operation = "subtract"
        };

        var jsonPayload = JsonSerializer.Serialize(currencyRequestPayload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var requestUrl = $"{gatewayServiceUrl}/api/users/currency/{purchaseRumourDto.SenderId}";
        var request = new HttpRequestMessage(HttpMethod.Put, requestUrl)
        {
            Content = content
        };

        try
        {
            logger.LogDebug("Sending currency update request to Gateway: {RequestUrl} for Sender: {SenderId}", requestUrl, purchaseRumourDto.SenderId);
            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogWarning(
                    "Gateway transaction failed for Sender: {SenderId}. Status: {StatusCode}, Response: {ErrorContent}", 
                    purchaseRumourDto.SenderId, response.StatusCode, errorContent);
                var error = new { error = new ErrorResponse("GATEWAY_ERROR", $"Failed to process transaction through gateway: {errorContent}") };
                return StatusCode((int)response.StatusCode, error);
            }
            
            logger.LogInformation("Gateway transaction successful for Sender: {SenderId}", purchaseRumourDto.SenderId);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Gateway service is unavailable at {GatewayUrl}. Sender: {SenderId}", gatewayServiceUrl, purchaseRumourDto.SenderId);
            var error = new { error = new ErrorResponse("SERVICE_UNAVAILABLE", $"Gateway service is unavailable: {ex.Message}") };
            return StatusCode(503, error);
        }

        try
        {
            logger.LogInformation("Creating rumour for Sender: {SenderId} targeting {TargetId}", purchaseRumourDto.SenderId, purchaseRumourDto.TargetId);
            var rumour = await rumourService.CreateRumourAsync(
                lobbyId,
                purchaseRumourDto.GameId, 
                purchaseRumourDto.SenderId,
                purchaseRumourDto.TargetId,
                purchaseRumourDto.RumourType
            );

            logger.LogInformation("Successfully created rumour ID: {RumourId} for Owner: {OwnerId}", rumour.Id, rumour.OwnerId);
            return Ok(new ApiResponse<Rumour> { Data = rumour });
        }
        catch (RumourTypeNotFoundException e)
        {
            logger.LogWarning(e, "Invalid rumour type '{RumourType}' requested by Sender: {SenderId}", purchaseRumourDto.RumourType, purchaseRumourDto.SenderId);
            var error = new { error = new ErrorResponse("BAD_RUMOURS_TYPE", e.Message) };
            return NotFound(error);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An unexpected error occurred while creating a rumour for Sender: {SenderId}", purchaseRumourDto.SenderId);
            var error = new { error = new ErrorResponse("SERVER_ERROR", "An unexpected error occurred.") };
            return StatusCode(500, error);
        }
    }

    [HttpGet("{lobbyId}/user/{ownerId:long}")]
    public async Task<IActionResult> GetUserRumours(string lobbyId, long ownerId)
    {
        logger.LogInformation("Fetching rumours for Lobby: {LobbyId}, Owner: {OwnerId}", lobbyId, ownerId);
        var rumours = await rumourService.GetRumoursByOwnerAsync(lobbyId, ownerId);
        logger.LogInformation("Retrieved {RumourCount} rumours for Owner: {OwnerId}", rumours.Count(), ownerId);
        return Ok(new ApiResponse<IEnumerable<Rumour>> { Data = rumours });
    }
}