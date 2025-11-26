using System.Collections.Concurrent;
using System.Text.Json;
using Grpc.Core;
using MafiaRumoursService.Protos;

namespace MafiaRumoursService.Services;

public class CreateRumourDto
{
    public string LobbyId { get; set; } = null!;
    public long GameId { get; set; }
    public long OwnerId { get; set; }
    public long TargetId { get; set; }
    public string Type { get; set; } = null!;
}

public class GrpcSubscriberService(
    ILogger<GrpcSubscriberService> logger,
    IRumourService rumourService) : MessageSubscriber.MessageSubscriberBase
{
    private static readonly ConcurrentDictionary<string, string> PendingTransactions = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public override Task<MessageResponse> ReceiveMessage(MessageRequest request, ServerCallContext context)
    {
        logger.LogInformation("Received gRPC Message: {Payload}", request.Payload);
        return Task.FromResult(new MessageResponse { Acknowledged = true });
    }

    public override Task<PrepareResponse> Prepare(PrepareRequest request, ServerCallContext context)
    {
        logger.LogInformation("2PC Prepare Phase for Tx {TxId}.", request.TransactionId);

        if (string.IsNullOrWhiteSpace(request.Payload))
        {
            logger.LogWarning("Tx {TxId} rejected: Empty payload", request.TransactionId);
            return Task.FromResult(new PrepareResponse { VoteCommit = false });
        }

        var canCommit = false;

        try
        {
            if (request.Payload.StartsWith("CreateRumour:"))
            {
                var jsonPart = request.Payload["CreateRumour:".Length..];
                var dto = JsonSerializer.Deserialize<CreateRumourDto>(jsonPart, JsonOptions);

                if (dto != null && !string.IsNullOrEmpty(dto.LobbyId) && !string.IsNullOrEmpty(dto.Type))
                {
                    if (dto.Type.Equals("activity", StringComparison.OrdinalIgnoreCase) || 
                        dto.Type.Equals("appearance", StringComparison.OrdinalIgnoreCase))
                    {
                        canCommit = true;
                    }
                    else
                    {
                        logger.LogWarning("Tx {TxId} rejected: Invalid rumour type '{Type}'", request.TransactionId, dto.Type);
                    }
                }
                else
                {
                    logger.LogWarning("Tx {TxId} rejected: Invalid JSON or missing required fields", request.TransactionId);
                }
            }
            else
            {
                canCommit = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during Prepare phase for Tx {TxId}", request.TransactionId);
            canCommit = false;
        }

        if (canCommit)
        {
            PendingTransactions[request.TransactionId] = request.Payload;
        }

        return Task.FromResult(new PrepareResponse { VoteCommit = canCommit });
    }

    public override async Task<CommitResponse> Commit(CommitRequest request, ServerCallContext context)
    {
        logger.LogInformation("2PC Commit Phase for Tx {TxId}", request.TransactionId);

        if (PendingTransactions.TryRemove(request.TransactionId, out var payload))
        {
            try
            {
                if (payload.StartsWith("CreateRumour:"))
                {
                    var jsonPart = payload["CreateRumour:".Length..];
                    var dto = JsonSerializer.Deserialize<CreateRumourDto>(jsonPart, JsonOptions);

                    if (dto != null)
                    {
                        logger.LogInformation("Executing Commit: Creating Rumour for Lobby {LobbyId}", dto.LobbyId);
                        await rumourService.CreateRumourAsync(dto.LobbyId, dto.GameId, dto.OwnerId, dto.TargetId, dto.Type);
                        logger.LogInformation("Successfully committed Tx {TxId}: Created Rumour", request.TransactionId);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute Commit for Tx {TxId}", request.TransactionId);
            }
        }
        else
        {
            logger.LogWarning("Received Commit for unknown or already processed Tx {TxId}", request.TransactionId);
        }

        return new CommitResponse { Acknowledged = true };
    }

    public override Task<RollbackResponse> Rollback(RollbackRequest request, ServerCallContext context)
    {
        logger.LogInformation("2PC Rollback Phase for Tx {TxId}", request.TransactionId);

        if (PendingTransactions.TryRemove(request.TransactionId, out _))
        {
            logger.LogInformation("Rolled back Tx {TxId}. Discarded payload.", request.TransactionId);
        }
        else
        {
            logger.LogWarning("Received Rollback for unknown Tx {TxId}", request.TransactionId);
        }

        return Task.FromResult(new RollbackResponse { Acknowledged = true });
    }
}