using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Messaging.Contracts;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Persistence.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DineOS.Infrastructure.Messaging;

public sealed class OrderCreatedMessageHandler(
    AppDbContext db,
    IOrderNotificationService notificationService,
    ILogger<OrderCreatedMessageHandler> logger)
{
    public async Task HandleAsync(OrderCreatedMessage message, CancellationToken ct = default)
    {
        if (await db.ProcessedMessages.AnyAsync(m => m.MessageId == message.MessageId, ct))
        {
            logger.LogInformation(
                "Duplicate RabbitMQ event skipped: MessageId={MessageId} EventType={EventType} OrderId={OrderId} TenantId={TenantId}",
                message.MessageId, nameof(OrderCreatedMessage), message.OrderId, message.TenantId);
            return;
        }

        var processedMessage = new ProcessedMessage
        {
            MessageId = message.MessageId,
            MessageType = nameof(OrderCreatedMessage),
            TenantId = message.TenantId,
            ProcessedAt = DateTime.UtcNow
        };

        db.ProcessedMessages.Add(processedMessage);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            logger.LogInformation(
                ex,
                "Duplicate RabbitMQ event skipped after insert conflict: MessageId={MessageId} EventType={EventType} OrderId={OrderId} TenantId={TenantId}",
                message.MessageId, nameof(OrderCreatedMessage), message.OrderId, message.TenantId);
            return;
        }

        try
        {
            await notificationService.BroadcastOrderCreatedAsync(message.TenantId, ToDto(message), ct);
        }
        catch
        {
            db.ProcessedMessages.Remove(processedMessage);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        logger.LogInformation(
            "RabbitMQ event handled: MessageId={MessageId} EventType={EventType} OrderId={OrderId} TenantId={TenantId}",
            message.MessageId, nameof(OrderCreatedMessage), message.OrderId, message.TenantId);
    }

    private static OrderDto ToDto(OrderCreatedMessage message) => new()
    {
        Id = message.OrderId,
        OrderType = message.OrderType,
        TableNumber = message.TableNumber,
        Status = message.Status,
        Total = message.Total,
        Notes = message.Notes,
        TenantId = message.TenantId,
        CreatedAt = message.CreatedAt,
        Items = message.Items.Select(i => new OrderItemDto
        {
            Id = i.Id,
            Name = i.Name,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Notes = i.Notes
        }).ToList()
    };

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
