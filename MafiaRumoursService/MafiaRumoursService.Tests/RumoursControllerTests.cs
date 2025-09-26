using System.Net;
using System.Text;
using MafiaRumoursService.Controllers;
using MafiaRumoursService.Exceptions;
using MafiaRumoursService.Models;
using MafiaRumoursService.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.Protected;

namespace MafiaRumoursService.Tests;

public class RumoursControllerTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IRumourService> _rumourServiceMock;
    private readonly RumoursController _controller;

    public RumoursControllerTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _rumourServiceMock = new Mock<IRumourService>();
        _controller = new RumoursController(_httpClientFactoryMock.Object, _rumourServiceMock.Object);

        Environment.SetEnvironmentVariable("CURRENCY_SERVICE_URL", null);
    }

    [Fact]
    public async Task PurchaseRumour_WhenCurrencyServiceIsSuccessful_ShouldReturnOk()
    {
        const string lobbyId = "test-lobby";
        var purchaseRumourDto = new PurchaseRumourDto { SenderId = 1, TargetId = 2, RumourType = "role" };
        var rumour = new Rumour { Id = 1, LobbyId = lobbyId, OwnerId = 1, TargetId = 2, Type = "role", Text = "Test rumour", CreatedAt = DateTime.UtcNow };

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        _rumourServiceMock.Setup(s => s.CreateRumourAsync(lobbyId, purchaseRumourDto.SenderId, purchaseRumourDto.TargetId, purchaseRumourDto.RumourType))
            .ReturnsAsync(rumour);

        var result = await _controller.PurchaseRumour(lobbyId, purchaseRumourDto);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedRumour = Assert.IsType<Rumour>(okResult.Value);
        Assert.Equal(rumour.LobbyId, returnedRumour.LobbyId);
        Assert.Equal(rumour.OwnerId, returnedRumour.OwnerId);
        Assert.Equal(rumour.TargetId, returnedRumour.TargetId);
    }

    [Fact]
    public async Task PurchaseRumour_WhenCurrencyServiceFails_ShouldReturnStatusCode()
    {
        const string lobbyId = "test-lobby";
        var purchaseRumourDto = new PurchaseRumourDto { SenderId = 1, TargetId = 2, RumourType = "role" };
        const string errorContent = "Insufficient funds";

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent(errorContent)
            });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await _controller.PurchaseRumour(lobbyId, purchaseRumourDto);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        Assert.Equal($"Failed to process transaction: {errorContent}", objectResult.Value);
    }

    [Fact]
    public async Task PurchaseRumour_WhenCurrencyServiceIsUnavailable_ShouldReturnServiceUnavailable()
    {
        const string lobbyId = "test-lobby";
        var purchaseRumourDto = new PurchaseRumourDto { SenderId = 1, TargetId = 2, RumourType = "role" };

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await _controller.PurchaseRumour(lobbyId, purchaseRumourDto);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, objectResult.StatusCode);
        Assert.Equal("Currency service is unavailable: Service unavailable", objectResult.Value);
    }

    [Fact]
    public async Task PurchaseRumour_WithUnknownRumourType_ShouldReturnNotFound()
    {
        const string lobbyId = "test-lobby";
        var purchaseRumourDto = new PurchaseRumourDto { SenderId = 1, TargetId = 2, RumourType = "unknown" };

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        _rumourServiceMock.Setup(s => s.CreateRumourAsync(lobbyId, purchaseRumourDto.SenderId, purchaseRumourDto.TargetId, purchaseRumourDto.RumourType))
            .ThrowsAsync(new RumourTypeNotFoundException("Rumour type not found"));

        var result = await _controller.PurchaseRumour(lobbyId, purchaseRumourDto);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var resultValue = notFoundResult.Value;
        var error = resultValue!.GetType().GetProperty("error")!.GetValue(resultValue, null);
        var code = error!.GetType().GetProperty("code")!.GetValue(error, null);
        var message = error.GetType().GetProperty("message")!.GetValue(error, null);

        Assert.Equal("BAD_RUMOURS_TYPE", code);
        Assert.Equal("Rumour type not found", message);
    }

    [Fact]
    public async Task GetUserRumours_ShouldReturnOkWithRumours()
    {
        const string lobbyId = "test-lobby";
        const long ownerId = 1;
        var rumours = new List<Rumour>
        {
            new() { Id = 1, LobbyId = lobbyId, OwnerId = ownerId, TargetId = 2, Type = "role", Text = "Test rumour 1", CreatedAt = DateTime.UtcNow },
            new() { Id = 2, LobbyId = lobbyId, OwnerId = ownerId, TargetId = 3, Type = "activity", Text = "Test rumour 2", CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
        };

        _rumourServiceMock.Setup(s => s.GetRumoursByOwnerAsync(lobbyId, ownerId))
            .ReturnsAsync(rumours);

        var result = await _controller.GetUserRumours(lobbyId, ownerId);

        var okResult = Assert.IsType<OkObjectResult>(result);

        Assert.NotNull(okResult.Value);

        var returnedRumours = Assert.IsAssignableFrom<IEnumerable<Rumour>>(okResult.Value);
        Assert.Equal(rumours.Count, returnedRumours.Count());
    }

    [Fact]
    public async Task PurchaseRumour_WhenEnvVarIsNull_ShouldUseDefaultUrl()
    {
        const string lobbyId = "test-lobby";
        var purchaseRumourDto = new PurchaseRumourDto { SenderId = 1, TargetId = 2, RumourType = "role" };
        var rumour = new Rumour { Id = 1, LobbyId = lobbyId, OwnerId = 1, TargetId = 2, Type = "role", Text = "Test rumour", CreatedAt = DateTime.UtcNow };

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().StartsWith("http://localhost:8000")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{}", Encoding.UTF8, "application/json") })
            .Verifiable();

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        _rumourServiceMock.Setup(s => s.CreateRumourAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(rumour);

        await _controller.PurchaseRumour(lobbyId, purchaseRumourDto);

        httpMessageHandlerMock.Verify();
    }
}