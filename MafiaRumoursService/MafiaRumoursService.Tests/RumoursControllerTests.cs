using Grpc.Core;
using MafiaRumoursService.Controllers;
using MafiaRumoursService.Models;
using MafiaRumoursService.Protos;
using MafiaRumoursService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace MafiaRumoursService.Tests;

public class RumoursControllerTests
{
    private readonly Mock<MessageBrokerService.MessageBrokerServiceClient> _messageBrokerClientMock;
    private readonly Mock<IRumourService> _rumourServiceMock;
    private readonly Mock<ILogger<RumoursController>> _loggerMock;
    private readonly RumoursController _controller;

    public RumoursControllerTests()
    {
        _messageBrokerClientMock = new Mock<MessageBrokerService.MessageBrokerServiceClient>();
        _rumourServiceMock = new Mock<IRumourService>();
        _loggerMock = new Mock<ILogger<RumoursController>>();
        
        _controller = new RumoursController(
            _messageBrokerClientMock.Object, 
            _rumourServiceMock.Object, 
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task PurchaseRumour_WhenBrokerIsAvailable_ShouldSendUnionPayload()
    {
        const string lobbyId = "test-lobby";
        var dto = new PurchaseRumourDto { GameId = 1, SenderId = 10, TargetId = 20, RumourType = "activity" };
        var publishResponse = new PublishResponse { MessageId = "msg-123" };

        var mockCall = new AsyncUnaryCall<PublishResponse>(
            Task.FromResult(publishResponse),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => [],
            () => { });

        PublishRequest capturedRequest = null!;
        _messageBrokerClientMock
            .Setup(c => c.PublishMessageAsync(It.IsAny<PublishRequest>(), null, null, CancellationToken.None))
            .Callback<PublishRequest, Metadata, DateTime?, CancellationToken>((req, _, _, _) => capturedRequest = req)
            .Returns(mockCall);

        var result = await _controller.PurchaseRumour(lobbyId, dto);

        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        Assert.NotNull(capturedRequest);
        
        Assert.Equal("user-management", capturedRequest.TopicName);

        Assert.DoesNotContain("CreateRumour:", capturedRequest.Payload);
        
        Assert.Contains("\"action\":\"2PC_PREPARE\"", capturedRequest.Payload);
        Assert.Contains("\"user_id\":10", capturedRequest.Payload);
        Assert.Contains("\"currency\":\"coins\"", capturedRequest.Payload);
        Assert.Contains("\"amount\":150", capturedRequest.Payload);

        Assert.Contains("\"LobbyId\":\"test-lobby\"", capturedRequest.Payload);
        Assert.Contains("\"Type\":\"activity\"", capturedRequest.Payload);
    }

    [Fact]
    public async Task PurchaseRumour_WhenBrokerFails_ShouldReturnServiceUnavailable()
    {
        const string lobbyId = "test-lobby";
        var dto = new PurchaseRumourDto { GameId = 1, SenderId = 10, TargetId = 20, RumourType = "activity" };

        _messageBrokerClientMock
            .Setup(c => c.PublishMessageAsync(It.IsAny<PublishRequest>(), null, null, CancellationToken.None))
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "Broker unavailable")));

        var result = await _controller.PurchaseRumour(lobbyId, dto);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, objectResult.StatusCode);
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
}