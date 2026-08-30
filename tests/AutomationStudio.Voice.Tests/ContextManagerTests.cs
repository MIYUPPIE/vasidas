using AutomationStudio.Abstractions;
using AutomationStudio.Voice.Context;

namespace AutomationStudio.Voice.Tests;

public class ContextManagerTests
{
    [Fact]
    public void Current_StartsEmpty()
    {
        var manager = new ContextManager();

        Assert.Equal(ExecutionContextSnapshot.Empty, manager.Current);
    }

    [Fact]
    public void SetCurrentVerse_UpdatesOnlyThatField()
    {
        var manager = new ContextManager();
        manager.SetCurrentApplication("VideoPsalm");

        manager.SetCurrentVerse(new BibleReference("John", 3, 16, 16));

        Assert.Equal("John", manager.Current.CurrentVerse?.Book);
        Assert.Equal("VideoPsalm", manager.Current.CurrentApplication); // untouched by the verse update.
    }

    [Fact]
    public void RecordCommand_SetsPreviousCommand()
    {
        var manager = new ContextManager();

        manager.RecordCommand(Intent.Simple("NEXT_SLIDE"));

        Assert.Equal("NEXT_SLIDE", manager.Current.PreviousCommand?.Name);
    }

    [Fact]
    public void Clear_ResetsEverythingToEmpty()
    {
        var manager = new ContextManager();
        manager.SetCurrentApplication("VideoPsalm");
        manager.SetCurrentVerse(new BibleReference("John", 3, 16, 16));
        manager.SetCurrentSlide(3);

        manager.Clear();

        Assert.Equal(ExecutionContextSnapshot.Empty, manager.Current);
    }

    [Fact]
    public void EachSetter_UpdatesItsOwnFieldIndependently()
    {
        var manager = new ContextManager();

        manager.SetCurrentApplication("obs");
        manager.SetCurrentWorkflow("wf-1");
        manager.SetCurrentSlide(2);
        manager.SetCurrentMedia("intro.mp4");
        manager.SetCurrentScene("Sermon");
        manager.SetCurrentTask("recording");

        var snapshot = manager.Current;
        Assert.Equal("obs", snapshot.CurrentApplication);
        Assert.Equal("wf-1", snapshot.CurrentWorkflowId);
        Assert.Equal(2, snapshot.CurrentSlide);
        Assert.Equal("intro.mp4", snapshot.CurrentMedia);
        Assert.Equal("Sermon", snapshot.CurrentScene);
        Assert.Equal("recording", snapshot.CurrentTask);
    }
}
