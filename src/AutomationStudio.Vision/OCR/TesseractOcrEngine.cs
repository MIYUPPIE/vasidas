using System.Diagnostics;
using System.Globalization;
using AutomationStudio.Abstractions;
using SkiaSharp;

namespace AutomationStudio.Vision.OCR;

/// <summary>
/// Fallback OCR engine (section 11): shells out to the system's <c>tesseract</c> CLI rather than
/// P/Invoking libtesseract directly — this project's established pattern (see the xdg-desktop-portal
/// and AT-SPI backends) is to prefer a stable, well-known CLI over a native binding once one turns
/// out fragile, and this is the fallback engine, not the primary, so simplicity wins over raw speed.
/// Requires the <c>tesseract-ocr</c> system package (and its language data) to be installed —
/// <see cref="IsAvailableAsync"/> lets callers check that cheaply before relying on it.
/// </summary>
public sealed class TesseractOcrEngine : IOcrEngine
{
    private const string Executable = "tesseract";

    /// <summary>Cheap, synchronous availability probe (matches <c>AtspiCapabilityCheck</c>'s style) — used both by capability detection at startup and by <see cref="CompositeOcrEngine"/> before falling back.</summary>
    public static bool IsAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(Executable, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            if (process is null)
            {
                return false;
            }

            return process.WaitForExit(2000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<OcrResult>> RecognizeAsync(Screenshot screenshot, OcrOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= OcrOptions.Default;
        using var prepared = OcrImagePreparation.Prepare(screenshot.PngData, options);

        var tempImagePath = Path.Combine(Path.GetTempPath(), $"automationstudio-ocr-{Guid.NewGuid():n}.png");
        try
        {
            using (var stream = File.Create(tempImagePath))
            {
                prepared.Bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
            }

            var tsv = await RunTesseractAsync(tempImagePath, options.Language, cancellationToken).ConfigureAwait(false);
            var words = ParseTsv(tsv);
            return GroupIntoLines(words, prepared, options.MinConfidence);
        }
        finally
        {
            try { File.Delete(tempImagePath); } catch { /* best-effort cleanup */ }
        }
    }

    private static async Task<string> RunTesseractAsync(string imagePath, string language, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(Executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(imagePath);
        startInfo.ArgumentList.Add("stdout");
        startInfo.ArgumentList.Add("-l");
        startInfo.ArgumentList.Add(NormalizeLanguageCode(language));
        startInfo.ArgumentList.Add("tsv");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the 'tesseract' process.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var stdout = await stdoutTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            var stderr = await stderrTask.ConfigureAwait(false);
            throw new InvalidOperationException($"tesseract exited with code {process.ExitCode}: {stderr}");
        }

        return stdout;
    }

    /// <summary>Tesseract uses ISO 639-2/T three-letter codes ("eng"); accept either that or a bare "en" and normalize.</summary>
    private static string NormalizeLanguageCode(string language) => language.Trim().ToLowerInvariant() switch
    {
        "en" or "eng" => "eng",
        _ => language.Trim().ToLowerInvariant()
    };

    internal sealed record TsvWord(int BlockNum, int ParNum, int LineNum, int Left, int Top, int Width, int Height, double Confidence, string Text);

    /// <summary>
    /// Parses tesseract's <c>tsv</c> output format: level, page_num, block_num, par_num, line_num,
    /// word_num, left, top, width, height, conf, text — one row per detected word (level 5), plus
    /// coarser rollup rows (levels 1-4) this only needs the word rows from.
    /// </summary>
    internal static List<TsvWord> ParseTsv(string tsv)
    {
        var words = new List<TsvWord>();
        using var reader = new StringReader(tsv);
        var header = reader.ReadLine(); // column header row
        if (header is null)
        {
            return words;
        }

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var columns = line.Split('\t');
            if (columns.Length < 12 || columns[0] != "5") // level 5 = word
            {
                continue;
            }

            var text = columns[11].Trim();
            if (text.Length == 0)
            {
                continue;
            }

            if (!double.TryParse(columns[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var conf) || conf < 0)
            {
                continue; // tesseract emits -1 for non-text rows; already filtered by level, but be defensive.
            }

            words.Add(new TsvWord(
                BlockNum: int.Parse(columns[2], CultureInfo.InvariantCulture),
                ParNum: int.Parse(columns[3], CultureInfo.InvariantCulture),
                LineNum: int.Parse(columns[4], CultureInfo.InvariantCulture),
                Left: int.Parse(columns[6], CultureInfo.InvariantCulture),
                Top: int.Parse(columns[7], CultureInfo.InvariantCulture),
                Width: int.Parse(columns[8], CultureInfo.InvariantCulture),
                Height: int.Parse(columns[9], CultureInfo.InvariantCulture),
                Confidence: conf / 100.0, // tesseract reports 0-100
                Text: text));
        }

        return words;
    }

    /// <summary>Merges word-level rows sharing a (block, paragraph, line) into one OcrResult per line, matching PaddleOCR's line-level granularity so both engines behave the same to callers.</summary>
    internal static List<OcrResult> GroupIntoLines(List<TsvWord> words, OcrImagePreparation.PreparedImage prepared, double minConfidence)
    {
        var results = new List<OcrResult>();
        foreach (var lineWords in words.GroupBy(w => (w.BlockNum, w.ParNum, w.LineNum)))
        {
            var ordered = lineWords.ToList();
            var text = string.Join(' ', ordered.Select(w => w.Text));
            var confidence = ordered.Average(w => w.Confidence);
            if (confidence < minConfidence)
            {
                continue;
            }

            var minX = ordered.Min(w => w.Left);
            var minY = ordered.Min(w => w.Top);
            var maxX = ordered.Max(w => w.Left + w.Width);
            var maxY = ordered.Max(w => w.Top + w.Height);

            results.Add(new OcrResult
            {
                Text = text,
                Bounds = prepared.ToScreenRect(minX, minY, maxX, maxY),
                Confidence = confidence
            });
        }

        return results;
    }
}
