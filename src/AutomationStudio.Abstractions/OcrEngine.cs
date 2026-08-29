namespace AutomationStudio.Abstractions;

/// <summary>One recognized text region (section 11's "Detected text / Bounding box / Confidence").</summary>
public sealed class OcrResult
{
    public required string Text { get; init; }
    public required ScreenRect Bounds { get; init; }

    /// <summary>0.0 to 1.0.</summary>
    public required double Confidence { get; init; }
}

/// <summary>
/// Configurable OCR knobs (section 11: Language, Confidence threshold, Region of interest, Scale,
/// Preprocessing). <see cref="Region"/> is in full-screenshot coordinates; implementations translate
/// results back to full-screenshot coordinates before returning them, so callers never need to know
/// a region was applied.
/// </summary>
public sealed record OcrOptions
{
    public static OcrOptions Default { get; } = new();

    /// <summary>ISO-639 style language hint (e.g. "en"). Engines that only ship one language may ignore this.</summary>
    public string Language { get; init; } = "en";

    /// <summary>Results below this confidence are dropped before being returned.</summary>
    public double MinConfidence { get; init; } = 0.5;

    /// <summary>Crop to this region of the screenshot before recognizing, instead of the whole image.</summary>
    public ScreenRect? Region { get; init; }

    /// <summary>Upscale the (possibly cropped) image by this factor before recognizing — small UI text often needs this to be readable.</summary>
    public double Scale { get; init; } = 1.0;

    /// <summary>Grayscale + contrast normalization before recognizing. Helps on low-contrast UI themes; unnecessary overhead on most screenshots.</summary>
    public bool Preprocess { get; init; }
}

/// <summary>
/// Text recognition from a screenshot (section 11): PaddleOCR primary, Tesseract fallback. Event-
/// driven per section 39 — called on demand by actions, never run continuously against the screen.
/// </summary>
public interface IOcrEngine
{
    Task<IReadOnlyList<OcrResult>> RecognizeAsync(Screenshot screenshot, OcrOptions? options = null, CancellationToken cancellationToken = default);
}
