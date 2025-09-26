using MafiaRumoursService.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using DotNetEnv;
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
        var currencyServiceUrl = Env.GetString("CURRENCY_SERVICE_URL") ?? "http://localhost:8000";
        var httpClient = httpClientFactory.CreateClient();

        var currencyRequestPayload = new
        {
            currency = RumourCurrency,
            amount = RumourCost,
            operation = "subtract"
        };
        
        var jsonPayload = JsonSerializer.Serialize(currencyRequestPayload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Put, $"{currencyServiceUrl}/currency/{purchaseRumourDto.SenderId}")
        {
            Content = content
        };

        try
        {
            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, $"Failed to process transaction: {errorContent}");
            }
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(503, $"Currency service is unavailable: {ex.Message}");
        }

        var rumour = await rumourService.CreateRumourAsync(
            lobbyId, 
            purchaseRumourDto.SenderId, 
            purchaseRumourDto.TargetId,
            purchaseRumourDto.RumourType
        );
        
        return Ok(rumour);
    }
    
    [HttpGet("{lobbyId}/user/{ownerId:long}")]
    public async Task<IActionResult> GetUserRumours(string lobbyId, long ownerId)
    {
        var rumours = await rumourService.GetRumoursByOwnerAsync(lobbyId, ownerId);
        return Ok(rumours);
    }
}