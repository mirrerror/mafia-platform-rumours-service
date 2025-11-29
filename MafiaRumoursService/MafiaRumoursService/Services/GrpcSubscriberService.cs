using System.Collections.Concurrent;
using System.Text.Json;
using Grpc.Core;
using MafiaRumoursService.Protos;

namespace MafiaRumoursService.Services;

public class RumourTransactionDto
{
    public string? Action { get; set; }
    public string? LobbyId { get; set; }
    public long GameId { get; set; }
    public long OwnerId { get; set; }
    public long TargetId { get; set; }
    public string? Type { get; set; }
}

public class GrpcSubscriberService(
    ILogger<GrpcSubscriberService> logger,
    IRumourService rumourService) : MessageSubscriber.MessageSubscriberBase
{
    private static readonly ConcurrentDictionary<string, string> PendingTransactions = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public override Task<MessageResponse> ReceiveMessage(MessageRequest request, ServerCallContext context)
    {
        return Task.FromResult(new MessageResponse { Acknowledged = true });
    }

    public override Task<PrepareResponse> Prepare(PrepareRequest request, ServerCallContext context)
    {
        logger.LogInformation("2PC [Phase 1: Prepare] Tx: {TxId}", request.TransactionId);

        if (string.IsNullOrWhiteSpace(request.Payload))
            return Task.FromResult(new PrepareResponse { VoteCommit = false });

        var voteYes = false;

        try
        {
            var dto = JsonSerializer.Deserialize<RumourTransactionDto>(request.Payload, JsonOptions);

            if (dto != null && !string.IsNullOrEmpty(dto.LobbyId) && !string.IsNullOrEmpty(dto.Type))
            {
                if (dto.Type.Equals("activity", StringComparison.OrdinalIgnoreCase) || 
                    dto.Type.Equals("appearance", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Tx {TxId}: Rumour Data Valid. Voting YES.", request.TransactionId);
                    voteYes = true;
                }
                else
                {
                    logger.LogWarning("Tx {TxId}: Invalid Rumour Type '{Type}'. Voting NO.", request.TransactionId, dto.Type);
                }
            }
            else
            {
                logger.LogWarning("Tx {TxId}: Payload missing LobbyId/Type. Voting NO.", request.TransactionId);
                voteYes = false; 
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tx {TxId}: Deserialization failed.", request.TransactionId);
            voteYes = false;
        }

        if (voteYes)
        {
            PendingTransactions[request.TransactionId] = request.Payload;
        }

        return Task.FromResult(new PrepareResponse { VoteCommit = voteYes });
    }

    public override async Task<CommitResponse> Commit(CommitRequest request, ServerCallContext context)
    {
        logger.LogInformation("2PC [Phase 2: Commit] Tx: {TxId}", request.TransactionId);

        if (PendingTransactions.TryRemove(request.TransactionId, out var payload))
        {
            try
            {
                var dto = JsonSerializer.Deserialize<RumourTransactionDto>(payload, JsonOptions);
                if (dto != null)
                {
                    await rumourService.CreateRumourAsync(dto.LobbyId!, dto.GameId, dto.OwnerId, dto.TargetId, dto.Type!);
                    logger.LogInformation("Tx {TxId}: Rumour successfully created in DB.", request.TransactionId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Tx {TxId}: Failed to execute Commit logic.", request.TransactionId);
            }
        }
        else
        {
            logger.LogWarning("Tx {TxId}: Commit received but no pending data found (already committed or timeout).", request.TransactionId);
        }

        return new CommitResponse { Acknowledged = true };
    }

    public override Task<RollbackResponse> Rollback(RollbackRequest request, ServerCallContext context)
    {
        logger.LogInformation("2PC [Phase 2: Rollback] Tx: {TxId}", request.TransactionId);
        
        if (PendingTransactions.TryRemove(request.TransactionId, out _))
        {
            logger.LogInformation("Tx {TxId}: Rolled back. Pending data discarded.", request.TransactionId);
        }
        
        return Task.FromResult(new RollbackResponse { Acknowledged = true });
    }
}