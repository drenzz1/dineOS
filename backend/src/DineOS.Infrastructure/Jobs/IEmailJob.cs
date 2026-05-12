namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Marker interface for jobs that send email. The dead-letter state filter
/// only captures jobs implementing this interface, so unrelated jobs don't
/// pollute the DeadLetterEmails table.
/// </summary>
public interface IEmailJob
{
}
