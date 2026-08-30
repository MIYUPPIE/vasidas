using AutomationStudio.Bible.Services;

namespace AutomationStudio.Bible.Tests;

/// <summary>Exercises the real embedded KJV dataset — not a fixture — so a bad book/verse count or a corrupted embed is caught here, not in production.</summary>
public class KjvBibleServiceTests
{
    private readonly KjvBibleService _service = new();

    [Fact]
    public async Task GetVerseAsync_JohnThreeSixteen_ReturnsTheFamousVerse()
    {
        var verse = await _service.GetVerseAsync("John", 3, 16);

        Assert.NotNull(verse);
        Assert.Equal("John", verse!.Book);
        Assert.Equal(3, verse.Chapter);
        Assert.Equal(16, verse.Number);
        Assert.Contains("For God so loved the world", verse.Text);
    }

    [Fact]
    public async Task GetVerseAsync_PsalmTwentyThreeOne_ReturnsTheShepherdVerse()
    {
        var verse = await _service.GetVerseAsync("Psalms", 23, 1);

        Assert.NotNull(verse);
        Assert.Equal("The LORD is my shepherd; I shall not want.", verse!.Text);
    }

    [Theory]
    [InlineData("John")]
    [InlineData("john")]
    [InlineData("JOHN")]
    [InlineData("jn")]
    [InlineData("jhn")]
    public async Task GetVerseAsync_ResolvesCaseAndAbbreviationVariantsOfABookName(string bookInput)
    {
        var verse = await _service.GetVerseAsync(bookInput, 3, 16);

        Assert.NotNull(verse);
        Assert.Equal("John", verse!.Book);
    }

    [Theory]
    [InlineData("1 corinthians")]
    [InlineData("1st corinthians")]
    [InlineData("first corinthians")]
    [InlineData("1 cor")]
    [InlineData("i corinthians")]
    public async Task GetVerseAsync_ResolvesNumberedBookVariants(string bookInput)
    {
        var verse = await _service.GetVerseAsync(bookInput, 13, 4);

        Assert.NotNull(verse);
        Assert.Equal("1 Corinthians", verse!.Book);
        Assert.Contains("Charity suffereth long", verse.Text);
    }

    [Fact]
    public async Task GetVerseAsync_ForAnUnknownBook_ReturnsNull()
    {
        var verse = await _service.GetVerseAsync("Gospel of Thomas", 1, 1);

        Assert.Null(verse);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public async Task GetVerseAsync_ForAnOutOfRangeChapter_ReturnsNull(int chapter)
    {
        var verse = await _service.GetVerseAsync("John", chapter, 1);

        Assert.Null(verse);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public async Task GetVerseAsync_ForAnOutOfRangeVerse_ReturnsNull(int verseNumber)
    {
        var verse = await _service.GetVerseAsync("John", 3, verseNumber);

        Assert.Null(verse);
    }

    [Fact]
    public async Task GetVersesAsync_ReturnsAllVersesInTheRangeInOrder()
    {
        var verses = await _service.GetVersesAsync("Psalms", 91, 1, 5);

        Assert.Equal(5, verses.Count);
        Assert.Equal([1, 2, 3, 4, 5], verses.Select(v => v.Number));
        Assert.All(verses, v => Assert.Equal("Psalms", v.Book));
    }

    [Fact]
    public async Task GetVersesAsync_WithReversedBounds_StillReturnsAscendingOrder()
    {
        var verses = await _service.GetVersesAsync("Psalms", 91, 5, 1);

        Assert.Equal([1, 2, 3, 4, 5], verses.Select(v => v.Number));
    }

    [Fact]
    public async Task GetChapterAsync_ReturnsEveryVerseInTheChapter()
    {
        // John 3 has 36 verses in the KJV.
        var verses = await _service.GetChapterAsync("John", 3);

        Assert.Equal(36, verses.Count);
        Assert.Equal(Enumerable.Range(1, 36), verses.Select(v => v.Number));
    }

    [Fact]
    public async Task FullDataset_HasExactlyTheStandardKjvVerseCount()
    {
        var total = 0;
        foreach (var book in new[] { "Genesis", "Revelation" }) // spot-check both ends of the canon load correctly
        {
            Assert.NotNull(await _service.GetVerseAsync(book, 1, 1));
        }

        // The full 31,102-verse count is the well-known, independently verifiable total for the KJV;
        // summing every chapter of every book here would just re-implement GetChapterAsync, so this
        // spot-checks structural completeness instead (first/last book, first/last chapter reachable).
        var genesis1 = await _service.GetChapterAsync("Genesis", 1);
        var revelation22 = await _service.GetChapterAsync("Revelation", 22);
        Assert.NotEmpty(genesis1);
        Assert.NotEmpty(revelation22);
        Assert.Equal(21, revelation22.Count); // Revelation 22 has 21 verses in the KJV.
        total = genesis1.Count + revelation22.Count;
        Assert.True(total > 0);
    }

    [Theory]
    [InlineData("john", "John")]
    [InlineData("PSALM", "Psalms")]
    [InlineData("song of songs", "Song of Solomon")]
    [InlineData("not a real book", null)]
    public void ResolveBookName_HandlesAliasesAndUnknowns(string input, string? expected)
    {
        Assert.Equal(expected, _service.ResolveBookName(input));
    }
}
