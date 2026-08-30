using AutomationStudio.Abstractions;
using AutomationStudio.Bible.Services;

namespace AutomationStudio.Bible.Tests;

/// <summary>DAS.md section 8's "Next verse" example (John 3:16 → John 3:17) generalized to every boundary case against the real dataset — mid-chapter, end-of-chapter, end-of-book, and the very start/end of the whole Bible.</summary>
public class VerseNavigationTests
{
    private readonly KjvBibleService _service = new();

    [Fact]
    public async Task GetNextVerseAsync_MidChapter_AdvancesTheVerseNumber()
    {
        var current = await _service.GetVerseAsync("John", 3, 16);

        var next = await _service.GetNextVerseAsync(current!);

        Assert.NotNull(next);
        Assert.Equal("John", next!.Book);
        Assert.Equal(3, next.Chapter);
        Assert.Equal(17, next.Number);
    }

    [Fact]
    public async Task GetNextVerseAsync_AtTheEndOfAChapter_RollsOverToTheNextChapter()
    {
        // John 3 has 36 verses.
        var current = await _service.GetVerseAsync("John", 3, 36);

        var next = await _service.GetNextVerseAsync(current!);

        Assert.NotNull(next);
        Assert.Equal("John", next!.Book);
        Assert.Equal(4, next.Chapter);
        Assert.Equal(1, next.Number);
    }

    [Fact]
    public async Task GetNextVerseAsync_AtTheEndOfABook_RollsOverToTheNextBook()
    {
        // Malachi's last verse (4:6) must roll into Matthew 1:1 — the Old/New Testament boundary.
        var malachiChapters = await _service.GetChapterAsync("Malachi", 4);
        var lastMalachiVerse = malachiChapters[^1];

        var next = await _service.GetNextVerseAsync(lastMalachiVerse);

        Assert.NotNull(next);
        Assert.Equal("Matthew", next!.Book);
        Assert.Equal(1, next.Chapter);
        Assert.Equal(1, next.Number);
    }

    [Fact]
    public async Task GetNextVerseAsync_AtTheVeryEndOfTheBible_ReturnsNull()
    {
        var revelationChapters = await _service.GetChapterAsync("Revelation", 22);
        var lastVerseOfTheBible = revelationChapters[^1];

        var next = await _service.GetNextVerseAsync(lastVerseOfTheBible);

        Assert.Null(next);
    }

    [Fact]
    public async Task GetPreviousVerseAsync_MidChapter_DecrementsTheVerseNumber()
    {
        var current = await _service.GetVerseAsync("John", 3, 16);

        var previous = await _service.GetPreviousVerseAsync(current!);

        Assert.NotNull(previous);
        Assert.Equal(3, previous!.Chapter);
        Assert.Equal(15, previous.Number);
    }

    [Fact]
    public async Task GetPreviousVerseAsync_AtTheStartOfAChapter_RollsBackToTheLastVerseOfThePreviousChapter()
    {
        var current = await _service.GetVerseAsync("John", 4, 1);

        var previous = await _service.GetPreviousVerseAsync(current!);

        Assert.NotNull(previous);
        Assert.Equal("John", previous!.Book);
        Assert.Equal(3, previous.Chapter);
        Assert.Equal(36, previous.Number); // John 3's last verse.
    }

    [Fact]
    public async Task GetPreviousVerseAsync_AtTheStartOfABook_RollsBackToTheLastVerseOfThePreviousBook()
    {
        var current = await _service.GetVerseAsync("Matthew", 1, 1);

        var previous = await _service.GetPreviousVerseAsync(current!);

        Assert.NotNull(previous);
        Assert.Equal("Malachi", previous!.Book);
        Assert.Equal(4, previous.Chapter);
        Assert.Equal(6, previous.Number); // Malachi's last verse.
    }

    [Fact]
    public async Task GetPreviousVerseAsync_AtTheVeryStartOfTheBible_ReturnsNull()
    {
        var genesis1_1 = await _service.GetVerseAsync("Genesis", 1, 1);

        var previous = await _service.GetPreviousVerseAsync(genesis1_1!);

        Assert.Null(previous);
    }

    [Fact]
    public async Task NextThenPrevious_ReturnsToTheOriginalVerse()
    {
        // Round trip across a chapter boundary, the trickiest case, both directions.
        var original = await _service.GetVerseAsync("John", 3, 36);

        var next = await _service.GetNextVerseAsync(original!);
        var backAgain = await _service.GetPreviousVerseAsync(next!);

        Assert.Equal(original!.Book, backAgain!.Book);
        Assert.Equal(original.Chapter, backAgain.Chapter);
        Assert.Equal(original.Number, backAgain.Number);
    }

    [Fact]
    public void BookOrder_StartsWithGenesisAndEndsWithRevelation()
    {
        Assert.Equal(66, _service.BookOrder.Count);
        Assert.Equal("Genesis", _service.BookOrder[0]);
        Assert.Equal("Revelation", _service.BookOrder[^1]);
    }
}
