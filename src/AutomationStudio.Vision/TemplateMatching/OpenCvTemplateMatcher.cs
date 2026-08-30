using AutomationStudio.Abstractions;
using OpenCvSharp;

namespace AutomationStudio.Vision.TemplateMatching;

/// <summary>
/// Template matching via OpenCV's normalized cross-correlation (design doc section 24). See the
/// package-choice comment in <c>AutomationStudio.Vision.csproj</c> for why OpenCvSharp4 (and
/// specifically its newer portable Linux runtime package) was chosen and confirmed live.
/// </summary>
public sealed class OpenCvTemplateMatcher : ITemplateMatcher
{
    public Task<TemplateMatchResult?> FindAsync(
        Screenshot screenshot, byte[] templatePngData, double minConfidence = 0.8,
        ScreenRect? searchRegion = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var full = Cv2.ImDecode(screenshot.PngData, ImreadModes.Color);
        if (full.Empty())
        {
            throw new InvalidOperationException("Could not decode screenshot PNG for template matching.");
        }

        using var template = Cv2.ImDecode(templatePngData, ImreadModes.Color);
        if (template.Empty())
        {
            throw new InvalidOperationException("Could not decode template PNG for template matching.");
        }

        var offsetX = 0;
        var offsetY = 0;
        Mat? cropped = null;

        try
        {
            var target = full;
            if (searchRegion is { } region)
            {
                var rect = Clamp(region, full.Width, full.Height);
                if (rect.Width < template.Width || rect.Height < template.Height)
                {
                    return Task.FromResult<TemplateMatchResult?>(null);
                }

                cropped = new Mat(full, rect);
                target = cropped;
                offsetX = rect.X;
                offsetY = rect.Y;
            }

            if (template.Width > target.Width || template.Height > target.Height)
            {
                return Task.FromResult<TemplateMatchResult?>(null);
            }

            using var result = new Mat();
            Cv2.MatchTemplate(target, template, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var confidence, out _, out var location);

            if (confidence < minConfidence)
            {
                return Task.FromResult<TemplateMatchResult?>(null);
            }

            var bounds = new ScreenRect(offsetX + location.X, offsetY + location.Y, template.Width, template.Height);
            return Task.FromResult<TemplateMatchResult?>(new TemplateMatchResult(bounds, confidence));
        }
        finally
        {
            cropped?.Dispose();
        }
    }

    /// <summary>Clamps a requested search region to the screenshot's actual bounds — a region hanging off the edge (a stale bound from a resized window, say) should shrink to what's actually there rather than throw.</summary>
    private static Rect Clamp(ScreenRect region, int imageWidth, int imageHeight)
    {
        var x = Math.Clamp(region.X, 0, imageWidth);
        var y = Math.Clamp(region.Y, 0, imageHeight);
        var width = Math.Clamp(region.Width, 0, imageWidth - x);
        var height = Math.Clamp(region.Height, 0, imageHeight - y);
        return new Rect(x, y, width, height);
    }
}
