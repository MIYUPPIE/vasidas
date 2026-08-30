using AutomationStudio.Abstractions;

namespace AutomationStudio.Core.Vision;

/// <summary>
/// Design doc section 12's Target Resolution Engine: tries each tier of a <see cref="VisualTarget"/>
/// in priority order — native element query, then OCR text, then template image, then fixed
/// coordinates — and returns whichever one actually located something, plus which tier that was and
/// how confident it is. A plain orchestration class over already-injected abstractions (matches
/// <see cref="Execution.WorkflowExecutionEngine"/>'s own shape) rather than an interface: there is
/// exactly one real implementation, and callers fake its dependencies, not the resolver itself.
/// </summary>
public sealed class TargetResolver(
    IElementFinder elementFinder,
    IOcrEngine ocrEngine,
    ITemplateMatcher templateMatcher,
    IScreenshotService screenshotService)
{
    /// <summary>Returns null when every tier <paramref name="target"/> actually supplied data for came up empty.</summary>
    public async Task<TargetResolution?> ResolveAsync(VisualTarget target, CancellationToken cancellationToken = default)
    {
        if (target.Element is { } query)
        {
            var element = await elementFinder.FindElementAsync(query, cancellationToken).ConfigureAwait(false);
            if (element is not null)
            {
                return new TargetResolution(TargetStrategy.ElementFinder, element.Bounds.Center, element.Confidence, element);
            }
        }

        Screenshot? screenshot = null;
        async Task<Screenshot> GetScreenshotAsync()
        {
            screenshot ??= await screenshotService.CaptureScreenAsync(cancellationToken).ConfigureAwait(false);
            return screenshot;
        }

        if (target.OcrText is { Length: > 0 } text)
        {
            var shot = await GetScreenshotAsync().ConfigureAwait(false);
            var options = new OcrOptions { MinConfidence = target.OcrMinConfidence, Region = target.SearchRegion };
            var results = await ocrEngine.RecognizeAsync(shot, options, cancellationToken).ConfigureAwait(false);
            var match = results.FirstOrDefault(r => r.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return new TargetResolution(TargetStrategy.Ocr, match.Bounds.Center, match.Confidence);
            }
        }

        if (target.TemplateImage is { Length: > 0 } template)
        {
            var shot = await GetScreenshotAsync().ConfigureAwait(false);
            var match = await templateMatcher
                .FindAsync(shot, template, target.TemplateMinConfidence, target.SearchRegion, cancellationToken)
                .ConfigureAwait(false);
            if (match is not null)
            {
                return new TargetResolution(TargetStrategy.TemplateMatching, match.Bounds.Center, match.Confidence);
            }
        }

        if (target.Coordinates is { } point)
        {
            return new TargetResolution(TargetStrategy.Coordinates, point, 1.0);
        }

        return null;
    }
}
