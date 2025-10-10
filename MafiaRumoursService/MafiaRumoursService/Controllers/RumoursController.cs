using MafiaRumoursService.Exceptions;
using MafiaRumoursService.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using MafiaRumoursService.Services;

namespace MafiaRumoursService.Controllers;

[ApiController]
[Route("api/rumours")]
public class RumoursController(IHttpClientFactory httpClientFactory, IRumourService rumourService) : ControllerBase
{
    private const int RumourCost = 150;
    private const string RumourCurrency = "coins";

    [HttpPost("{lobbyId}/purchase")]
    public async Task<IActionResult> PurchaseRumour(string lobbyId, [FromBody] PurchaseRumourDto purchaseRumourDto)
    {
        var gatewayServiceUrl = Environment.GetEnvironmentVariable("GATEWAY_SERVICE_URL") ?? "http://localhost:8000";
        var httpClient = httpClientFactory.CreateClient();

        var currencyRequestPayload = new
        {
            currency = RumourCurrency,
            amount = RumourCost,
            operation = "subtract"
        };

        var jsonPayload = JsonSerializer.Serialize(currencyRequestPayload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Put, $"{gatewayServiceUrl}/api/currency/{purchaseRumourDto.SenderId}")
        {
            Content = content
        };

        try
        {
            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var error = new { error = new ErrorResponse("GATEWAY_ERROR", $"Failed to process transaction through gateway: {errorContent}") };
                return StatusCode((int)response.StatusCode, error);
            }
        }
        catch (HttpRequestException ex)
        {
            var error = new { error = new ErrorResponse("SERVICE_UNAVAILABLE", $"Gateway service is unavailable: {ex.Message}") };
            return StatusCode(503, error);
        }

        try
        {
            var rumour = await rumourService.CreateRumourAsync(
                lobbyId,
                purchaseRumourDto.GameId, 
                purchaseRumourDto.SenderId,
                purchaseRumourDto.TargetId,
                purchaseRumourDto.RumourType
            );

            return Ok(new ApiResponse<Rumour> { Data = rumour });
        }
        catch (RumourTypeNotFoundException e)
        {
            var error = new { error = new ErrorResponse("BAD_RUMOURS_TYPE", e.Message) };
            return NotFound(error);
        }
    }

    [HttpGet("{lobbyId}/user/{ownerId:long}")]
    public async Task<IActionResult> GetUserRumours(string lobbyId, long ownerId)
    {
        var rumours = await rumourService.GetRumoursByOwnerAsync(lobbyId, ownerId);
        return Ok(new ApiResponse<IEnumerable<Rumour>> { Data = rumours });
    }
}