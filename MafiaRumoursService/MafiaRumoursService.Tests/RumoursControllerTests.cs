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

        Environment.SetEnvironmentVariable("GATEWAY_SERVICE_URL", null);
    }

    [Fact]
    public async Task PurchaseRumour_WhenGatewayIsSuccessful_ShouldReturnOk()
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
        var apiResponse = Assert.IsType<ApiResponse<Rumour>>(okResult.Value);
        var returnedRumour = apiResponse.Data;
        Assert.NotNull(returnedRumour);
        Assert.Equal(rumour.LobbyId, returnedRumour.LobbyId);
    }

    [Fact]
    public async Task PurchaseRumour_WhenGatewayServiceFails_ShouldReturnStatusCode()
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
        
        AssertErrorResponse(objectResult.Value, "GATEWAY_ERROR", $"Failed to process transaction through gateway: {errorContent}");
    }

    [Fact]
    public async Task PurchaseRumour_WhenGatewayServiceIsUnavailable_ShouldReturnServiceUnavailable()
    {
        const string lobbyId = "test-lobby";
        var purchaseRumourDto = new PurchaseRumourDto { SenderId = 1, TargetId = 2, RumourType = "role" };
        const string exceptionMessage = "Service unavailable";

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException(exceptionMessage));

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await _controller.PurchaseRumour(lobbyId, purchaseRumourDto);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, objectResult.StatusCode);
        
        AssertErrorResponse(objectResult.Value, "SERVICE_UNAVAILABLE", $"Gateway service is unavailable: {exceptionMessage}");
    }

    [Fact]
    public async Task PurchaseRumour_WithUnknownRumourType_ShouldReturnNotFound()
    {
        const string lobbyId = "test-lobby";
        var purchaseRumourDto = new PurchaseRumourDto { SenderId = 1, TargetId = 2, RumourType = "unknown" };
        const string exceptionMessage = "Rumour type not found";

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
            .ThrowsAsync(new RumourTypeNotFoundException(exceptionMessage));

        var result = await _controller.PurchaseRumour(lobbyId, purchaseRumourDto);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        
        AssertErrorResponse(notFoundResult.Value, "BAD_RUMOURS_TYPE", exceptionMessage);
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
        var apiResponse = Assert.IsType<ApiResponse<IEnumerable<Rumour>>>(okResult.Value);
        Assert.Equal(rumours.Count, apiResponse.Data!.Count());
    }

    [Fact]
    public async Task PurchaseRumour_WhenEnvVarIsNull_ShouldUseDefaultUrl()
    {
        const string lobbyId = "test-lobby";
        var purchaseRumourDto = new PurchaseRumourDto { SenderId = 1, TargetId = 2, RumourType = "role" };
        
        // FIXED: Initialized the mock rumour object with all required properties.
        var rumour = new Rumour { Id = 1, LobbyId = lobbyId, OwnerId = 1, TargetId = 2, Type = "role", Text = "Test rumour", CreatedAt = DateTime.UtcNow };

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().StartsWith("http://localhost:8000")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK })
            .Verifiable();

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        _rumourServiceMock.Setup(s => s.CreateRumourAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(rumour);

        await _controller.PurchaseRumour(lobbyId, purchaseRumourDto);

        httpMessageHandlerMock.Verify();
    }
    
    [Fact]
    public async Task PurchaseRumour_WhenEnvVarIsSet_ShouldUseUrlFromEnv()
    {
        const string lobbyId = "test-lobby";
        var purchaseRumourDto = new PurchaseRumourDto { SenderId = 1, TargetId = 2, RumourType = "role" };

        var rumour = new Rumour { Id = 1, LobbyId = lobbyId, OwnerId = 1, TargetId = 2, Type = "role", Text = "Test rumour", CreatedAt = DateTime.UtcNow };
        const string customGatewayUrl = "http://custom-gateway-service:8080";

        Environment.SetEnvironmentVariable("GATEWAY_SERVICE_URL", customGatewayUrl);

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().StartsWith(customGatewayUrl)),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK })
            .Verifiable();

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        _rumourServiceMock.Setup(s => s.CreateRumourAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(rumour);

        await _controller.PurchaseRumour(lobbyId, purchaseRumourDto);

        httpMessageHandlerMock.Verify();
        
        Environment.SetEnvironmentVariable("GATEWAY_SERVICE_URL", null);
    }
    
    private static void AssertErrorResponse(object? value, string expectedCode, string expectedMessage)
    {
        Assert.NotNull(value);
        var errorProperty = value.GetType().GetProperty("error");
        Assert.NotNull(errorProperty);

        var errorObject = errorProperty.GetValue(value);
        Assert.NotNull(errorObject);

        var codeProperty = errorObject.GetType().GetProperty("Code");
        var messageProperty = errorObject.GetType().GetProperty("Message");
        Assert.NotNull(codeProperty);
        Assert.NotNull(messageProperty);

        var actualCode = codeProperty.GetValue(errorObject) as string;
        var actualMessage = messageProperty.GetValue(errorObject) as string;

        Assert.Equal(expectedCode, actualCode);
        Assert.Equal(expectedMessage, actualMessage);
    }
}