using AutomationStudio.Abstractions;
using AutomationStudio.Bible.Services;
using AutomationStudio.Voice.Context;

namespace AutomationStudio.Voice.Tests;

/// <summary>DAS.md section 8's canonical example, verbatim: "Show John 3:16" then "Next verse" must resolve to John 3:17 without the user repeating the reference.</summary>
public class IntentContextResolverTests
{
    private readonly ContextManager _contextManager = new();
    private readonly IntentContextResolver _resolver;

    public IntentContextResolverTests()
    {
        _resolver = new IntentContextResolver(_contextManager, new KjvBibleService());
    }

    [Fact]
    public async Task ResolveAsync_ExplicitBibleVerseIntent_PassesThroughAndUpdatesContext()
    {
        var intent = new Intent("DISPLAY_BIBLE_VERSE", new Dictionary<string, string>
        {
            ["book"] = "John", ["chapter"] = "3", ["startVerse"] = "16", ["endVerse"] = "16"
        });

        var resolved = await _resolver.ResolveAsync(intent);

        Assert.Same(intent, resolved);
        Assert.Equal("John", _contextManager.Current.CurrentVerse?.Book);
        Assert.Equal(16, _contextManager.Current.CurrentVerse?.StartVerse);
    }

    [Fact]
    public async Task ResolveAsync_NextVerseAfterAnExplicitVerse_ResolvesToJohnThreeSeventeen()
    {
        // The exact DAS.md section 8 scenario: "Show John 3:16" then "Next verse" → John 3:17.
        await _resolver.ResolveAsync(new Intent("DISPLAY_BIBLE_VERSE", new Dictionary<string, string>
        {
            ["book"] = "John", ["chapter"] = "3", ["startVerse"] = "16", ["endVerse"] = "16"
        }));

        var resolved = await _resolver.ResolveAsync(Intent.Simple("NEXT_VERSE"));

        Assert.NotNull(resolved);
        Assert.Equal("DISPLAY_BIBLE_VERSE", resolved!.Name);
        Assert.Equal("John", resolved.Parameter("book"));
        Assert.Equal("3", resolved.Parameter("chapter"));
        Assert.Equal("17", resolved.Parameter("startVerse"));
    }

    [Fact]
    public async Task ResolveAsync_NextVerseWithNoPriorContext_ReturnsNull()
    {
        var resolved = await _resolver.ResolveAsync(Intent.Simple("NEXT_VERSE"));

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveAsync_PreviousVerse_MovesBackward()
    {
        await _resolver.ResolveAsync(new Intent("DISPLAY_BIBLE_VERSE", new Dictionary<string, string>
        {
            ["book"] = "John", ["chapter"] = "3", ["startVerse"] = "16", ["endVerse"] = "16"
        }));

        var resolved = await _resolver.ResolveAsync(Intent.Simple("PREVIOUS_VERSE"));

        Assert.Equal("15", resolved?.Parameter("startVerse"));
    }

    [Fact]
    public async Task ResolveAsync_ChainedNextVerse_KeepsAdvancingFromTheUpdatedContext()
    {
        await _resolver.ResolveAsync(new Intent("DISPLAY_BIBLE_VERSE", new Dictionary<string, string>
        {
            ["book"] = "John", ["chapter"] = "3", ["startVerse"] = "16", ["endVerse"] = "16"
        }));

        await _resolver.ResolveAsync(Intent.Simple("NEXT_VERSE")); // -> 17
        var thirdCall = await _resolver.ResolveAsync(Intent.Simple("NEXT_VERSE")); // -> 18

        Assert.Equal("18", thirdCall?.Parameter("startVerse"));
    }

    [Fact]
    public async Task ResolveAsync_NextVerseAtTheEndOfTheBible_ReturnsNull()
    {
        await _resolver.ResolveAsync(new Intent("DISPLAY_BIBLE_VERSE", new Dictionary<string, string>
        {
            ["book"] = "Revelation", ["chapter"] = "22", ["startVerse"] = "21", ["endVerse"] = "21"
        }));

        var resolved = await _resolver.ResolveAsync(Intent.Simple("NEXT_VERSE"));

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveAsync_NextVerseWhenOnlyAWholeChapterWasShown_ReturnsNull()
    {
        // "Show Psalm 23" has no specific verse to advance from.
        await _resolver.ResolveAsync(new Intent("DISPLAY_BIBLE_VERSE", new Dictionary<string, string>
        {
            ["book"] = "Psalms", ["chapter"] = "23"
        }));

        var resolved = await _resolver.ResolveAsync(Intent.Simple("NEXT_VERSE"));

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveAsync_NonVerseIntent_PassesThroughUnchanged_AndIsRecordedAsPreviousCommand()
    {
        var intent = Intent.Simple("START_RECORDING");

        var resolved = await _resolver.ResolveAsync(intent);

        Assert.Same(intent, resolved);
        Assert.Equal("START_RECORDING", _contextManager.Current.PreviousCommand?.Name);
    }
}
