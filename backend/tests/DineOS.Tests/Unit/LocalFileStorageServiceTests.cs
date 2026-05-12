using DineOS.Application.Options;
using DineOS.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace DineOS.Tests.Unit;

public class LocalFileStorageServiceTests
{
    // Each test calls CreateSut() which returns a service backed by a fresh temp
    // directory. The DirectoryInfo is passed back so the caller can delete it in a
    // finally block, ensuring isolation regardless of test outcome.
    private static (LocalFileStorageService svc, DirectoryInfo root) CreateSut(
        string urlBasePath = "/uploads")
    {
        var root = Directory.CreateTempSubdirectory("dineos-lfss-test-");
        var opts = Options.Create(new FileStorageOptions
        {
            RootPath    = root.FullName,
            UrlBasePath = urlBasePath,
        });
        return (new LocalFileStorageService(opts), root);
    }

    // ── SaveAsync ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ReturnedUrlFilename_IsGuid_OriginalNameDiscarded()
    {
        var (svc, root) = CreateSut();
        try
        {
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

            var url = await svc.SaveAsync(stream, "my-photo.PNG", "image/png", "menu-items");

            // URL: /uploads/menu-items/{32-hex}.png
            var filename = Path.GetFileNameWithoutExtension(url.Split('/').Last());
            var ext      = Path.GetExtension(url);

            Assert.Equal(32, filename.Length);
            Assert.True(filename.All(c => "0123456789abcdef".Contains(c)),
                "Filename must be lowercase hex only (no-dashes GUID)");
            Assert.Equal(".png", ext);
            Assert.DoesNotContain("my-photo", url);
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public async Task SaveAsync_ExtensionIsNormalisedToLowercase()
    {
        var (svc, root) = CreateSut();
        try
        {
            using var stream = new MemoryStream(new byte[] { 1 });

            var url = await svc.SaveAsync(stream, "photo.JPG", "image/jpeg", "menu-items");

            Assert.EndsWith(".jpg", url);
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public async Task SaveAsync_FileIsWrittenUnderRootSubfolder_WithCorrectContent()
    {
        var (svc, root) = CreateSut();
        try
        {
            var payload = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic
            using var stream = new MemoryStream(payload);

            var url = await svc.SaveAsync(stream, "image.png", "image/png", "menu-items");

            var storedName   = url.Split('/').Last();
            var expectedPath = Path.Combine(root.FullName, "menu-items", storedName);

            Assert.True(File.Exists(expectedPath));
            Assert.Equal(payload, await File.ReadAllBytesAsync(expectedPath));
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public async Task SaveAsync_SubfolderTraversal_ThrowsInvalidOperationException()
    {
        var (svc, root) = CreateSut();
        try
        {
            using var stream = new MemoryStream(new byte[] { 1 });

            // "../escaped" resolves to a sibling of the root — outside the storage root
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.SaveAsync(stream, "evil.png", "image/png", "../escaped"));
        }
        finally { root.Delete(recursive: true); }
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingFile_DeletesFromDisk()
    {
        var (svc, root) = CreateSut();
        try
        {
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var url = await svc.SaveAsync(stream, "photo.jpg", "image/jpeg", "menu-items");

            await svc.DeleteAsync(url);

            var storedName = url.Split('/').Last();
            Assert.False(File.Exists(Path.Combine(root.FullName, "menu-items", storedName)));
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public async Task DeleteAsync_NonExistentFile_DoesNotThrow()
    {
        var (svc, root) = CreateSut();
        try
        {
            // A well-formed URL that refers to a file that was never written
            var exception = await Record.ExceptionAsync(
                () => svc.DeleteAsync("/uploads/menu-items/00000000000000000000000000000000.png"));

            Assert.Null(exception);
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public async Task DeleteAsync_UrlWithWrongBasePath_ThrowsInvalidOperationException()
    {
        var (svc, root) = CreateSut();
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.DeleteAsync("/wrong-base/menu-items/file.jpg"));
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public async Task DeleteAsync_UrlTraversal_ThrowsInvalidOperationException()
    {
        var (svc, root) = CreateSut();
        try
        {
            // Traversal embedded after the base path prefix — resolves above the storage root
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.DeleteAsync("/uploads/menu-items/../../../etc/passwd"));
        }
        finally { root.Delete(recursive: true); }
    }
}
