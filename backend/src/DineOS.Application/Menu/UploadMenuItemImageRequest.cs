using DineOS.Application.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace DineOS.Application.Menu;

public record UploadMenuItemImageRequest(Stream Content, string FileName, string ContentType, long Length);

public class UploadMenuItemImageRequestValidator : AbstractValidator<UploadMenuItemImageRequest>
{
    private static readonly HashSet<string> AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp"];

    private static readonly HashSet<string> AllowedExtensions =
        [".jpg", ".jpeg", ".png", ".webp"];

    private static readonly IReadOnlyDictionary<string, string[]> TypeToExtensions =
        new Dictionary<string, string[]>
        {
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/png"]  = [".png"],
            ["image/webp"] = [".webp"],
        };

    public UploadMenuItemImageRequestValidator(IOptions<FileStorageOptions> options)
    {
        var maxBytes = options.Value.MaxBytes;

        RuleFor(x => x.Length)
            .GreaterThan(0)
            .WithErrorCode("FILE_EMPTY")
            .WithMessage("File must not be empty.")
            .LessThanOrEqualTo(maxBytes)
            .WithErrorCode("FILE_TOO_LARGE")
            .WithMessage($"File must not exceed {maxBytes / 1_048_576} MB.");

        RuleFor(x => x.ContentType)
            .Must(ct => AllowedContentTypes.Contains(ct))
            .WithErrorCode("UNSUPPORTED_CONTENT_TYPE")
            .WithMessage("Only image/jpeg, image/png, and image/webp are accepted.");

        RuleFor(x => x.FileName)
            .Must(fn => AllowedExtensions.Contains(Path.GetExtension(fn).ToLowerInvariant()))
            .WithErrorCode("INVALID_EXTENSION")
            .WithMessage("File extension must be .jpg, .jpeg, .png, or .webp.");

        RuleFor(x => x)
            .Must(r =>
            {
                var ext = Path.GetExtension(r.FileName).ToLowerInvariant();
                return TypeToExtensions.TryGetValue(r.ContentType, out var exts) && exts.Contains(ext);
            })
            .WithErrorCode("EXTENSION_MISMATCH")
            .WithMessage("Content-Type does not match the file extension.");
    }
}
