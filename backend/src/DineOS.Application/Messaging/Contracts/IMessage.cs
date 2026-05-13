namespace DineOS.Application.Messaging.Contracts;

public interface IMessage
{
    string MessageId { get; }
    DateTime OccurredAt { get; }
}
