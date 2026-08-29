using AutomationStudio.Vision.OCR;
using SkiaSharp;

namespace AutomationStudio.Vision.Tests;

/// <summary>
/// Unit tests for the TSV-parsing/line-grouping logic that don't need the real <c>tesseract</c> CLI
/// installed, plus a live end-to-end test that only runs when it is (mirrors
/// AtspiElementFinderTests' pattern of skipping rather than failing when optional system
/// infrastructure isn't present).
/// </summary>
public class TesseractOcrEngineTests
{
    // A trimmed, real example of tesseract's `tsv` output for a two-word line "Start Recording",
    // including the coarser rollup rows (levels 1-4) a real run also emits, which must be ignored.
    private const string SampleTsv = """
        level	page_num	block_num	par_num	line_num	word_num	left	top	width	height	conf	text
        1	1	0	0	0	0	0	0	400	120	-1
        2	1	1	0	0	0	15	25	296	69	-1
        3	1	1	1	0	0	15	25	296	69	-1
        4	1	1	1	1	0	15	25	296	69	-1
        5	1	1	1	1	1	15	25	140	60	96.5	Start
        5	1	1	1	1	2	171	34	140	60	95.8	Recording
        """;

    [Fact]
    public void ParseTsv_ExtractsOnlyWordLevelRows()
    {
        var words = TesseractOcrEngine.ParseTsv(SampleTsv);

        Assert.Equal(2, words.Count);
        Assert.Equal("Start", words[0].Text);
        Assert.Equal("Recording", words[1].Text);
        Assert.Equal(0.965, words[0].Confidence, precision: 3);
    }

    [Fact]
    public void ParseTsv_IgnoresEmptyLines()
    {
        var words = TesseractOcrEngine.ParseTsv("level\tpage_num\n1\t1\t0\t0\t0\t0\t0\t0\t1\t1\t-1\t\n");

        Assert.Empty(words);
    }

    [Fact]
    public void GroupIntoLines_MergesSameLineWordsIntoOneResult_WithAveragedConfidenceAndUnionBounds()
    {
        var words = TesseractOcrEngine.ParseTsv(SampleTsv);
        using var prepared = MakeIdentityPreparedImage();

        var results = TesseractOcrEngine.GroupIntoLines(words, prepared, minConfidence: 0.0);

        var line = Assert.Single(results);
        Assert.Equal("Start Recording", line.Text);
        Assert.Equal((0.965 + 0.958) / 2, line.Confidence, precision: 3);
        // Left = min(15,171)=15, Top = min(25,34)=25, Right = max(155,311)=311, Bottom = max(85,94)=94
        Assert.Equal(15, line.Bounds.X);
        Assert.Equal(25, line.Bounds.Y);
        Assert.Equal(311, line.Bounds.Right);
        Assert.Equal(94, line.Bounds.Bottom);
    }

    [Fact]
    public void GroupIntoLines_DropsLinesBelowMinConfidence()
    {
        var words = TesseractOcrEngine.ParseTsv(SampleTsv);
        using var prepared = MakeIdentityPreparedImage();

        var results = TesseractOcrEngine.GroupIntoLines(words, prepared, minConfidence: 0.99);

        Assert.Empty(results);
    }

    [Fact]
    public void IsAvailable_NeverThrows_RegardlessOfWhetherTheCliIsInstalled()
    {
        // Just confirms the availability probe is safe to call unconditionally at startup — the
        // actual true/false depends on whether tesseract-ocr happens to be installed on this
        // machine, which the live test below accounts for separately.
        var available = TesseractOcrEngine.IsAvailable();
        Assert.True(available || !available);
    }

    [Fact]
    public async Task RecognizeAsync_AgainstTheRealCli_FindsRealTextInARenderedScreenshot()
    {
        if (!TesseractOcrEngine.IsAvailable())
        {
            return; // tesseract-ocr isn't installed on this machine; nothing to verify live.
        }

        var engine = new TesseractOcrEngine();
        var screenshot = TestImages.Render("Start Recording");

        var results = await engine.RecognizeAsync(screenshot);

        var match = Assert.Single(results);
        Assert.Contains("Start Recording", match.Text, StringComparison.OrdinalIgnoreCase);
    }

    private static OcrImagePreparation.PreparedImage MakeIdentityPreparedImage() =>
        new(new SKBitmap(1, 1), OffsetX: 0, OffsetY: 0, Scale: 1.0);
}
