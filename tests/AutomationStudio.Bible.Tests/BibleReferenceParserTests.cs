using AutomationStudio.Bible.Parser;
using AutomationStudio.Bible.Services;

namespace AutomationStudio.Bible.Tests;

/// <summary>DAS.md section 17: every one of these spoken/typed shapes must normalize to the same structured reference before automation begins.</summary>
public class BibleReferenceParserTests
{
    private readonly KjvBibleService _bibleService = new();

    [Theory]
    [InlineData("John 3:16")]
    [InlineData("john 3:16")]
    [InlineData("John chapter 3 verse 16")]
    [InlineData("John chapter 3, verse 16")]
    [InlineData("John three sixteen")]
    [InlineData("John three verse sixteen")]
    [InlineData("John chapter three verse sixteen")]
    public void Parse_EveryEquivalentPhrasing_ResolvesToJohnThreeSixteen(string phrasing)
    {
        var reference = BibleReferenceParser.Parse(phrasing, _bibleService);

        Assert.NotNull(reference);
        Assert.Equal("John", reference!.Book);
        Assert.Equal(3, reference.Chapter);
        Assert.Equal(16, reference.StartVerse);
        Assert.Equal(16, reference.EndVerse);
    }

    [Theory]
    [InlineData("Psalm 91:1-5")]
    [InlineData("Psalm 91 verses 1 to 5")]
    [InlineData("Psalm chapter 91 verses 1 to 5")]
    [InlineData("Psalm 91:1 to 5")]
    public void Parse_RangePhrasings_ResolveToTheSameRange(string phrasing)
    {
        var reference = BibleReferenceParser.Parse(phrasing, _bibleService);

        Assert.NotNull(reference);
        Assert.Equal("Psalms", reference!.Book);
        Assert.Equal(91, reference.Chapter);
        Assert.Equal(1, reference.StartVerse);
        Assert.Equal(5, reference.EndVerse);
        Assert.True(reference.IsRange);
    }

    [Theory]
    [InlineData("Psalm 23")]
    [InlineData("Psalm chapter 23")]
    [InlineData("Psalm twenty three")]
    public void Parse_ChapterOnlyPhrasings_ResolveWithNoVerse(string phrasing)
    {
        var reference = BibleReferenceParser.Parse(phrasing, _bibleService);

        Assert.NotNull(reference);
        Assert.Equal("Psalms", reference!.Book);
        Assert.Equal(23, reference.Chapter);
        Assert.Null(reference.StartVerse);
        Assert.True(reference.IsWholeChapter);
    }

    [Fact]
    public void Parse_MultiWordBookName_ResolvesCorrectly()
    {
        var reference = BibleReferenceParser.Parse("Song of Solomon 2:1", _bibleService);

        Assert.NotNull(reference);
        Assert.Equal("Song of Solomon", reference!.Book);
    }

    [Theory]
    [InlineData("1 Corinthians 13:4")]
    [InlineData("first corinthians chapter 13 verse 4")]
    [InlineData("1st corinthians thirteen four")]
    public void Parse_NumberedBookPhrasings_ResolveCorrectly(string phrasing)
    {
        var reference = BibleReferenceParser.Parse(phrasing, _bibleService);

        Assert.NotNull(reference);
        Assert.Equal("1 Corinthians", reference!.Book);
        Assert.Equal(13, reference.Chapter);
        Assert.Equal(4, reference.StartVerse);
    }

    [Fact]
    public void Parse_HandlesLargeSpokenNumbers_ForPsalmOneNineteen()
    {
        // Psalm 119 is the Bible's longest chapter (176 verses) — a real stress case for the
        // hundred-based number-word parser, not just small examples.
        var reference = BibleReferenceParser.Parse("Psalm one hundred nineteen verse one hundred seventy six", _bibleService);

        Assert.NotNull(reference);
        Assert.Equal("Psalms", reference!.Book);
        Assert.Equal(119, reference.Chapter);
        Assert.Equal(176, reference.StartVerse);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello there")]
    [InlineData("Gospel of Thomas 1:1")]
    [InlineData("John")]
    public void Parse_ForUnrecognizableOrInvalidInput_ReturnsNull(string input)
    {
        Assert.Null(BibleReferenceParser.Parse(input, _bibleService));
    }

    [Fact]
    public void Parse_RoundTripsThroughItsOwnToString()
    {
        var reference = BibleReferenceParser.Parse("John 3:16", _bibleService);

        Assert.Equal("John 3:16", reference!.ToString());
    }

    [Fact]
    public void Parse_RangeRoundTripsThroughItsOwnToString()
    {
        var reference = BibleReferenceParser.Parse("Psalm 91:1-5", _bibleService);

        Assert.Equal("Psalms 91:1-5", reference!.ToString());
    }

    [Fact]
    public void Parse_WholeChapterRoundTripsThroughItsOwnToString()
    {
        var reference = BibleReferenceParser.Parse("Psalm 23", _bibleService);

        Assert.Equal("Psalms 23", reference!.ToString());
    }
}
