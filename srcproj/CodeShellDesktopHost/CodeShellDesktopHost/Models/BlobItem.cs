namespace CodeShellDesktopHost.Models;

public sealed class BlobItem
{
    public string Key { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public string MimeType { get; init; } = "application/octet-stream";
    public byte[] Data { get; init; } = [];
    public string UpdatedUtc { get; init; } = string.Empty;
}
