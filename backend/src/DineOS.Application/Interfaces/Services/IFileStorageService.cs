namespace DineOS.Application.Interfaces.Services;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream stream, string originalFileName, string contentType, string subfolder, CancellationToken ct = default);

    Task DeleteAsync(string relativeUrl, CancellationToken ct = default);
}
