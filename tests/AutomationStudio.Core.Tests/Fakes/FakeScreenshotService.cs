using AutomationStudio.Abstractions;

namespace AutomationStudio.Core.Tests.Fakes;

public sealed class FakeScreenshotService : IScreenshotService
{
    public int CaptureCount { get; private set; }

    private static Screenshot MakeScreenshot(int width, int height) => new()
    {
        // A minimal but well-formed 1x1 PNG is unnecessary for these tests — nothing decodes it,
        // only SaveAsync (a plain byte write) is exercised.
        PngData = [137, 80, 78, 71, 13, 10, 26, 10],
        Width = width,
        Height = height
    };

    public Task<Screenshot> CaptureScreenAsync(CancellationToken cancellationToken = default)
    {
        CaptureCount++;
        return Task.FromResult(MakeScreenshot(1920, 1080));
    }

    public Task<Screenshot> CaptureRegionAsync(ScreenRect region, CancellationToken cancellationToken = default)
    {
        CaptureCount++;
        return Task.FromResult(MakeScreenshot(region.Width, region.Height));
    }
}
