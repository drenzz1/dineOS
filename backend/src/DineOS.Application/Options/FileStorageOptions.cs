namespace DineOS.Application.Options;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";
    public string RootPath     { get; init; } = "/app/uploads";
    public string UrlBasePath  { get; init; } = "/uploads";
    public long   MaxBytes     { get; init; } = 5_242_880; // 5 MB
    public bool   ServeLocally { get; init; } = false;
}
