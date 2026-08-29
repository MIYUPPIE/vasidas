using AutomationStudio.Abstractions;
using Microsoft.Extensions.Logging;

namespace AutomationStudio.Vision.OCR;

/// <summary>
/// PaddleOCR primary, Tesseract fallback (section 11) — an engine-availability fallback, not a
/// per-search cascade: if the primary engine fails to initialize (missing model files, a broken ONNX
/// install), every subsequent call in this process goes straight to Tesseract instead of re-trying
/// and re-failing PaddleOCR on every single search. Both engines return the same
/// <see cref="OcrResult"/> shape, so callers never know which one actually answered.
/// </summary>
public sealed class CompositeOcrEngine : IOcrEngine, IDisposable
{
    private readonly IOcrEngine _primary;
    private readonly IOcrEngine _fallback;
    private readonly Func<bool> _isFallbackAvailable;
    private readonly ILogger<CompositeOcrEngine>? _logger;
    private volatile bool _primaryUnavailable;

    public CompositeOcrEngine(ILogger<CompositeOcrEngine>? logger = null)
        : this(new RapidOcrEngine(), new TesseractOcrEngine(), TesseractOcrEngine.IsAvailable, logger)
    {
    }

    /// <summary>Test seam: real callers use the parameterless constructor above.</summary>
    internal CompositeOcrEngine(IOcrEngine primary, IOcrEngine fallback, Func<bool> isFallbackAvailable, ILogger<CompositeOcrEngine>? logger = null)
    {
        _primary = primary;
        _fallback = fallback;
        _isFallbackAvailable = isFallbackAvailable;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OcrResult>> RecognizeAsync(Screenshot screenshot, OcrOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!_primaryUnavailable)
        {
            try
            {
                return await _primary.RecognizeAsync(screenshot, options, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _primaryUnavailable = true;
                _logger?.LogWarning(ex, "Primary OCR engine (PaddleOCR/RapidOcrNet) failed; falling back to Tesseract for the rest of this session.");
            }
        }

        if (_isFallbackAvailable())
        {
            return await _fallback.RecognizeAsync(screenshot, options, cancellationToken).ConfigureAwait(false);
        }

        throw new CapabilityUnavailableException(
            "OCR",
            "Neither the primary OCR engine (PaddleOCR) nor the fallback (Tesseract CLI) is available on this machine.");
    }

    public void Dispose() => (_primary as IDisposable)?.Dispose();
}
