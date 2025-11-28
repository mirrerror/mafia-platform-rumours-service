using System.Text.Json;
using Grpc.Core;
using MafiaRumoursService.Protos;
using MafiaRumoursService.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace MafiaRumoursService.Tests;

public class GrpcSubscriberServiceTests
{
    private readonly Mock<ILogger<GrpcSubscriberService>> _loggerMock;
    private readonly Mock<IRumourService> _rumourServiceMock;
    private readonly GrpcSubscriberService _service;

    public GrpcSubscriberServiceTests()
    {
        _loggerMock = new Mock<ILogger<GrpcSubscriberService>>();
        _rumourServiceMock = new Mock<IRumourService>();
        _service = new GrpcSubscriberService(_loggerMock.Object, _rumourServiceMock.Object);
    }

    [Fact]
    public async Task ReceiveMessage_ReturnsAcknowledged()
    {
        var context = new Mock<ServerCallContext>();
        var request = new MessageRequest { Payload = "test" };

        var response = await _service.ReceiveMessage(request, context.Object);

        Assert.True(response.Acknowledged);
    }

    [Fact]
    public async Task Prepare_WithEmptyPayload_ReturnsVoteCommitFalse()
    {
        var context = new Mock<ServerCallContext>();
        var request = new PrepareRequest { TransactionId = "tx1", Payload = "" };

        var response = await _service.Prepare(request, context.Object);

        Assert.False(response.VoteCommit);
    }

    [Fact]
    public async Task Prepare_WithNonRumourPayload_ReturnsVoteCommitTrue()
    {
        var context = new Mock<ServerCallContext>();
        var request = new PrepareRequest { TransactionId = "tx2", Payload = "OtherEvent:{}" };

        var response = await _service.Prepare(request, context.Object);

        Assert.True(response.VoteCommit);
    }

    [Fact]
    public async Task Prepare_WithValidActivityRumour_ReturnsVoteCommitTrue()
    {
        var context = new Mock<ServerCallContext>();
        var dto = new CreateRumourDto { LobbyId = "l1", GameId = 1, OwnerId = 1, TargetId = 2, Type = "activity" };
        var json = JsonSerializer.Serialize(dto);
        var request = new PrepareRequest { TransactionId = "tx3", Payload = "CreateRumour:" + json };

        var response = await _service.Prepare(request, context.Object);

        Assert.True(response.VoteCommit);
    }

    [Fact]
    public async Task Prepare_WithInvalidRumourType_ReturnsVoteCommitFalse()
    {
        var context = new Mock<ServerCallContext>();
        var dto = new CreateRumourDto { LobbyId = "l1", Type = "invalid" };
        var json = JsonSerializer.Serialize(dto);
        var request = new PrepareRequest { TransactionId = "tx4", Payload = "CreateRumour:" + json };

        var response = await _service.Prepare(request, context.Object);

        Assert.False(response.VoteCommit);
    }

    [Fact]
    public async Task Prepare_WithInvalidJson_ReturnsVoteCommitFalse()
    {
        var context = new Mock<ServerCallContext>();
        var request = new PrepareRequest { TransactionId = "tx5", Payload = "CreateRumour:{invalid-json}" };

        var response = await _service.Prepare(request, context.Object);

        Assert.False(response.VoteCommit);
    }

    [Fact]
    public async Task Prepare_WithMissingLobbyId_ReturnsVoteCommitFalse()
    {
        var context = new Mock<ServerCallContext>();
        var dto = new CreateRumourDto { LobbyId = "", GameId = 1, OwnerId = 1, TargetId = 2, Type = "activity" };
        var json = JsonSerializer.Serialize(dto);
        var request = new PrepareRequest { TransactionId = "tx_fail", Payload = "CreateRumour:" + json };

        var response = await _service.Prepare(request, context.Object);

        Assert.False(response.VoteCommit);
    }

    [Fact]
    public async Task Prepare_WithNullDto_ReturnsVoteCommitFalse()
    {
        var context = new Mock<ServerCallContext>();
        var request = new PrepareRequest { TransactionId = "tx_null", Payload = "CreateRumour:null" };

        var response = await _service.Prepare(request, context.Object);

        Assert.False(response.VoteCommit);
    }

    [Fact]
    public async Task Commit_WithKnownTransaction_ExecutesCreation()
    {
        var context = new Mock<ServerCallContext>();
        const string txId = "tx6";
        var dto = new CreateRumourDto { LobbyId = "l1", GameId = 10, OwnerId = 20, TargetId = 30, Type = "activity" };
        var payload = "CreateRumour:" + JsonSerializer.Serialize(dto);

        await _service.Prepare(new PrepareRequest { TransactionId = txId, Payload = payload }, context.Object);

        var response = await _service.Commit(new CommitRequest { TransactionId = txId }, context.Object);

        Assert.True(response.Acknowledged);
        _rumourServiceMock.Verify(x => x.CreateRumourAsync("l1", 10, 20, 30, "activity"), Times.Once);
    }

    [Fact]
    public async Task Commit_WithUnknownTransaction_LogsWarning()
    {
        var context = new Mock<ServerCallContext>();
        var response = await _service.Commit(new CommitRequest { TransactionId = "unknown-tx" }, context.Object);

        Assert.True(response.Acknowledged);
        _rumourServiceMock.Verify(x => x.CreateRumourAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Commit_WhenServiceThrowsException_LogsError()
    {
        var context = new Mock<ServerCallContext>();
        const string txId = "tx7";
        var dto = new CreateRumourDto { LobbyId = "l1", Type = "activity" };
        var payload = "CreateRumour:" + JsonSerializer.Serialize(dto);

        await _service.Prepare(new PrepareRequest { TransactionId = txId, Payload = payload }, context.Object);

        _rumourServiceMock.Setup(x => x.CreateRumourAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Database error"));

        var response = await _service.Commit(new CommitRequest { TransactionId = txId }, context.Object);

        Assert.True(response.Acknowledged); 
    }

    [Fact]
    public async Task Rollback_WithKnownTransaction_RemovesIt()
    {
        var context = new Mock<ServerCallContext>();
        const string txId = "tx8";
        await _service.Prepare(new PrepareRequest { TransactionId = txId, Payload = "CreateRumour:{}" }, context.Object);

        var response = await _service.Rollback(new RollbackRequest { TransactionId = txId }, context.Object);

        Assert.True(response.Acknowledged);
        
        await _service.Commit(new CommitRequest { TransactionId = txId }, context.Object);
        _rumourServiceMock.Verify(x => x.CreateRumourAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Rollback_WithUnknownTransaction_LogsWarning()
    {
        var context = new Mock<ServerCallContext>();
        var response = await _service.Rollback(new RollbackRequest { TransactionId = "unknown-tx" }, context.Object);
        Assert.True(response.Acknowledged);
    }
}