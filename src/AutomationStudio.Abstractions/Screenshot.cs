namespace AutomationStudio.Abstractions;

/// <summary>
/// An in-memory captured screen image. Pixel data is always stored pre-encoded as PNG so that
/// every consumer (disk save, OCR, template matching, UI preview) shares one representation and
/// no platform-specific pixel buffer leaks out of the backend that captured it.
/// </summary>
public sealed class Screenshot
{
    public required byte[] PngData { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(path, PngData, cancellationToken).ConfigureAwait(false);
    }
}
