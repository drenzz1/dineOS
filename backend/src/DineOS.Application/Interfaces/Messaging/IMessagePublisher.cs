using DineOS.Application.Messaging.Contracts;

namespace DineOS.Application.Interfaces.Messaging;

public interface IMessagePublisher
{
    Task<bool> TryPublishAsync<TMessage>(
        TMessage message,
        string routingKey,
        CancellationToken ct = default)
        where TMessage : IMessage;
}
