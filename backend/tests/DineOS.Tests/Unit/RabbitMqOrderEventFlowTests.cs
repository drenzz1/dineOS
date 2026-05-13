using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Messaging;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Messaging.Contracts;
using DineOS.Application.Orders;
using DineOS.Infrastructure.Messaging;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class RabbitMqOrderEventFlowTests
{
    [Fact]
    public async Task CreateOrderAsync_PublishesOrderCreatedMessage()
    {
        var tenantService = Substitute.For<ITenantService>();
        tenantService.TenantId.Returns(42L);

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns("cashier-1");

        var publisher = Substitute.For<IMessagePublisher>();
        publisher.TryPublishAsync(
            Arg.Any<OrderCreatedMessage>(),
            MessageRouting.OrderCreatedRoutingKey,
            Arg.Any<CancellationToken>()).Returns(true);
        var notifications = Substitute.For<IOrderNotificationService>();
        await using var db = CreateDb(tenantService);

        var service = new OrderService(
            db,
            tenantService,
            currentUser,
            new CreateOrderRequestValidator(),
            new UpdateOrderStatusRequestValidator(),
            publisher,
            notifications,
            NullLogger<OrderService>.Instance);

        var result = await service.CreateOrderAsync(new CreateOrderRequest
        {
            OrderType = "dine-in",
            TableNumber = 7,
            Items =
            [
                new CreateOrderItemRequest
                {
                    Name = "Burger",
                    Quantity = 2,
                    UnitPrice = 8.50m
                }
            ]
        });

        Assert.True(result.IsSuccess);
        await publisher.Received(1).TryPublishAsync(
            Arg.Is<OrderCreatedMessage>(message =>
                message.MessageId == $"order-created-{result.Value!.Id}" &&
                message.OrderId == result.Value.Id &&
                message.TenantId == 42L &&
                message.OrderType == "dine-in" &&
                message.TableNumber == 7 &&
                message.Status == "New" &&
                message.Total == 17.00m &&
                message.Items.Count == 1 &&
                message.Items[0].Name == "Burger"),
            MessageRouting.OrderCreatedRoutingKey,
            Arg.Any<CancellationToken>());
        await notifications.DidNotReceive().BroadcastOrderCreatedAsync(
            Arg.Any<long>(),
            Arg.Any<OrderDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrderAsync_WhenPublishReturnsFalse_BroadcastsOrderCreatedAndMarksMessageProcessed()
    {
        await AssertOrderCreatedFallbackAsync(publisher =>
        {
            publisher.TryPublishAsync(
                Arg.Any<OrderCreatedMessage>(),
                MessageRouting.OrderCreatedRoutingKey,
                Arg.Any<CancellationToken>()).Returns(false);
        });
    }

    [Fact]
    public async Task CreateOrderAsync_WhenPublishThrows_BroadcastsOrderCreatedAndMarksMessageProcessed()
    {
        await AssertOrderCreatedFallbackAsync(publisher =>
        {
            publisher.TryPublishAsync(
                Arg.Any<OrderCreatedMessage>(),
                MessageRouting.OrderCreatedRoutingKey,
                Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromException<bool>(new InvalidOperationException("RabbitMQ unavailable.")));
        });
    }

    private static async Task AssertOrderCreatedFallbackAsync(Action<IMessagePublisher> configurePublisher)
    {
        var tenantService = Substitute.For<ITenantService>();
        tenantService.TenantId.Returns(42L);

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns("cashier-1");

        var publisher = Substitute.For<IMessagePublisher>();
        configurePublisher(publisher);
        var notifications = Substitute.For<IOrderNotificationService>();
        await using var db = CreateDb(tenantService);

        var service = new OrderService(
            db,
            tenantService,
            currentUser,
            new CreateOrderRequestValidator(),
            new UpdateOrderStatusRequestValidator(),
            publisher,
            notifications,
            NullLogger<OrderService>.Instance);

        var result = await service.CreateOrderAsync(new CreateOrderRequest
        {
            OrderType = "pickup",
            Items =
            [
                new CreateOrderItemRequest
                {
                    Name = "Soup",
                    Quantity = 1,
                    UnitPrice = 6.25m
                }
            ]
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await db.ProcessedMessages.CountAsync());
        Assert.Equal($"order-created-{result.Value!.Id}", await db.ProcessedMessages.Select(m => m.MessageId).SingleAsync());

        await notifications.Received(1).BroadcastOrderCreatedAsync(
            42,
            Arg.Is<OrderDto>(order =>
                order.Id == result.Value.Id &&
                order.TenantId == 42 &&
                order.OrderType == "pickup" &&
                order.Total == 6.25m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrderCreatedMessageHandler_SkipsDuplicateMessageIds()
    {
        var tenantService = Substitute.For<ITenantService>();
        var notifications = Substitute.For<IOrderNotificationService>();
        await using var db = CreateDb(tenantService);

        var handler = new OrderCreatedMessageHandler(
            db,
            notifications,
            NullLogger<OrderCreatedMessageHandler>.Instance);

        var message = new OrderCreatedMessage(
            MessageId: "order-created-123",
            OrderId: 123,
            TenantId: 42,
            OrderType: "pickup",
            TableNumber: null,
            Status: "New",
            Total: 12.50m,
            Notes: null,
            CreatedAt: DateTime.UtcNow,
            OccurredAt: DateTime.UtcNow,
            Items:
            [
                new OrderItemMessage(1, "Soup", 1, 12.50m, null)
            ]);

        await handler.HandleAsync(message);
        await handler.HandleAsync(message);

        Assert.Equal(1, await db.ProcessedMessages.CountAsync());
        await notifications.Received(1).BroadcastOrderCreatedAsync(
            42,
            Arg.Is<OrderDto>(order => order.Id == 123 && order.Total == 12.50m),
            Arg.Any<CancellationToken>());
    }

    private static AppDbContext CreateDb(ITenantService tenantService) =>
        new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantService);
}
