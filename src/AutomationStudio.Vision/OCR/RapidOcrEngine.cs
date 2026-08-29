using AutomationStudio.Abstractions;
using RapidOcrNet;
using OcrResult = AutomationStudio.Abstractions.OcrResult;

namespace AutomationStudio.Vision.OCR;

/// <summary>
/// Primary OCR engine (section 11): PaddleOCR's PP-OCRv5 models run via RapidOcrNet (ONNX Runtime +
/// SkiaSharp — see <see cref="AutomationStudio.Vision.csproj"/>'s comment for why this binding was
/// chosen over Sdcb.PaddleSharp). Models ship inside the RapidOcrNet NuGet package itself, so this
/// needs no network access and no separately-installed runtime (section 29's offline requirement).
/// </summary>
public sealed class RapidOcrEngine : IOcrEngine, IDisposable
{
    private readonly object _initLock = new();
    private RapidOcr? _ocr;

    public async Task<IReadOnlyList<OcrResult>> RecognizeAsync(Screenshot screenshot, OcrOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= OcrOptions.Default;
        var ocr = GetOrInitOcr();

        using var prepared = OcrImagePreparation.Prepare(screenshot.PngData, options);

        var rapidOptions = RapidOcrOptions.Default with { TextScore = (float)options.MinConfidence };
        var result = await ocr.DetectAsync(prepared.Bitmap, rapidOptions, progress: null, cancellationToken).ConfigureAwait(false);

        var results = new List<OcrResult>(result.TextBlocks.Length);
        foreach (var block in result.TextBlocks)
        {
            if (string.IsNullOrWhiteSpace(block.Text))
            {
                continue;
            }

            var confidence = block.CharScores is { Length: > 0 } scores ? scores.Average() : 0.0;
            if (confidence < options.MinConfidence)
            {
                continue;
            }

            var points = block.BoxPoints;
            var minX = points.Min(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxX = points.Max(p => p.X);
            var maxY = points.Max(p => p.Y);

            results.Add(new OcrResult
            {
                Text = block.Text,
                Bounds = prepared.ToScreenRect(minX, minY, maxX, maxY),
                Confidence = confidence
            });
        }

        return results;
    }

    /// <summary>Model loading (three ONNX sessions) is expensive enough to do once, not per call — lazily initialized so a capability check that never recognizes anything never pays for it.</summary>
    private RapidOcr GetOrInitOcr()
    {
        if (_ocr is not null)
        {
            return _ocr;
        }

        lock (_initLock)
        {
            if (_ocr is null)
            {
                var ocr = new RapidOcr();
                ocr.InitModels();
                _ocr = ocr;
            }

            return _ocr;
        }
    }

    public void Dispose() => _ocr?.Dispose();
}
