using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Services;

public class LocalFileStorageService(IOptions<FileStorageOptions> options) : IFileStorageService
{
    private readonly string _rootPath    = Path.GetFullPath(options.Value.RootPath);
    private readonly string _urlBasePath = options.Value.UrlBasePath.TrimEnd('/');

    public async Task<string> SaveAsync(Stream stream, string originalFileName, string contentType, string subfolder, CancellationToken ct = default)
    {
        var ext    = Path.GetExtension(originalFileName).ToLowerInvariant();
        var stored = $"{Guid.NewGuid():N}{ext}";

        var folder = Path.GetFullPath(Path.Combine(_rootPath, subfolder));
        GuardAgainstTraversal(folder);

        Directory.CreateDirectory(folder);

        var fullPath = Path.Combine(folder, stored);

        await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fs, ct);

        return $"{_urlBasePath}/{subfolder}/{stored}";
    }

    public Task DeleteAsync(string relativeUrl, CancellationToken ct = default)
    {
        // Strip the URL base path to get the path within the storage root.
        // relativeUrl is e.g. "/uploads/menu-items/{guid}.jpg"
        var prefix    = _urlBasePath.TrimStart('/');
        var urlPath   = relativeUrl.TrimStart('/');

        if (!urlPath.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"URL '{relativeUrl}' does not start with the configured base path '{_urlBasePath}'.");

        var pathWithinRoot = urlPath[prefix.Length..].TrimStart('/');
        var fullPath       = Path.GetFullPath(Path.Combine(_rootPath, pathWithinRoot));

        GuardAgainstTraversal(fullPath);

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    private void GuardAgainstTraversal(string resolvedPath)
    {
        if (!resolvedPath.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && resolvedPath != _rootPath)
            throw new InvalidOperationException("Resolved path is outside the configured storage root.");
    }
}
