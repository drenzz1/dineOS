namespace DineOS.Application.Interfaces.Services;

public interface IDatabaseMigrator
{
    Task MigrateAsync(CancellationToken ct = default);
}
