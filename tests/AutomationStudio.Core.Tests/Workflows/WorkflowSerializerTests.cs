using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Workflows;

namespace AutomationStudio.Core.Tests.Workflows;

public class WorkflowSerializerTests
{
    private static Workflow BuildSampleWorkflow() => new()
    {
        Id = "obs-recording",
        Name = "OBS Recording",
        Version = 1,
        Actions =
        [
            new OpenApplicationAction { Path = "obs" },
            new WaitAction { Milliseconds = 5000 },
            new ClickAction { X = 742, Y = 510 },
            new WaitAction { Milliseconds = 60000 },
            new ClickAction { X = 742, Y = 510 },
        ]
    };

    [Fact]
    public void ToJson_ProducesTheSectionNineActionShape()
    {
        var workflow = new Workflow
        {
            Id = "obs-recording",
            Name = "OBS Recording",
            Version = 1,
            Actions = [new OpenApplicationAction { Path = "obs64.exe" }, new WaitAction { Milliseconds = 5000 }]
        };

        var json = WorkflowSerializer.ToJson(workflow);

        Assert.Contains("\"type\": \"OpenApplication\"", json);
        Assert.Contains("\"path\": \"obs64.exe\"", json);
        Assert.Contains("\"type\": \"Wait\"", json);
        Assert.Contains("\"milliseconds\": 5000", json);
    }

    /// <summary>
    /// Regression test: [JsonIgnore] on WorkflowAction.ActionType (the abstract base declaration)
    /// does not propagate to derived classes' "override string ActionType" properties — attribute
    /// inheritance across property overrides isn't automatic in .NET reflection, so each derived
    /// action needs its own [JsonIgnore]. Caught live: a saved workflow's JSON had both "type" (the
    /// intended discriminator) and a redundant "actionType" field for every single action type.
    /// </summary>
    [Fact]
    public void ToJson_NeverIncludesTheActionTypeProperty_ForAnyActionType()
    {
        var workflow = new Workflow
        {
            Id = "wf",
            Name = "wf",
            Actions =
            [
                new OpenApplicationAction { Path = "p" },
                new CloseApplicationAction { ProcessName = "p" },
                new FocusWindowAction { TitleContains = "t" },
                new FocusApplicationAction { ProcessName = "p" },
                new MoveMouseAction { X = 1, Y = 1 },
                new ClickAction(),
                new DoubleClickAction(),
                new RightClickAction(),
                new MouseDownAction(),
                new MouseUpAction(),
                new ScrollAction(),
                new TypeTextAction { Text = "t" },
                new PressKeyAction { Key = Key.Enter },
                new KeyCombinationAction { Keys = [Key.Enter] },
                new WaitAction { Milliseconds = 1 },
                new TakeScreenshotAction(),
                new FindElementAction { Target = new ElementTarget { Text = "t" } },
                new ClickElementAction { Target = new ElementTarget { Text = "t" } },
                new TypeIntoElementAction { Target = new ElementTarget { Text = "t" }, Text = "t" },
                new FindTextAction { Text = "t" },
                new ClickTextAction { Text = "t" },
                new VerifyTextAction { Text = "t" },
            ]
        };

        var json = WorkflowSerializer.ToJson(workflow);

        Assert.DoesNotContain("actionType", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Matches section 9's canonical JSON exactly: <c>{ "type": "FindText", "text": "Start Recording", "timeout": 10000 }</c> followed by a bare Click.</summary>
    [Fact]
    public void ToJson_FindTextAction_MatchesSectionNinesCanonicalShape()
    {
        var workflow = new Workflow
        {
            Id = "wf",
            Name = "wf",
            Actions =
            [
                new FindTextAction { Text = "Start Recording", TimeoutMilliseconds = 10000 },
                new ClickAction(),
            ]
        };

        var json = WorkflowSerializer.ToJson(workflow);

        Assert.Contains("\"type\": \"FindText\"", json);
        Assert.Contains("\"text\": \"Start Recording\"", json);
        Assert.Contains("\"timeoutMilliseconds\": 10000", json);
        Assert.Contains("\"type\": \"Click\"", json);

        var reparsed = WorkflowSerializer.FromJson(json);
        var findText = Assert.IsType<FindTextAction>(reparsed.Actions[0]);
        Assert.Equal("Start Recording", findText.Text);
        Assert.Null(Assert.IsType<ClickAction>(reparsed.Actions[1]).X);
    }

    /// <summary>Matches section 15's exact JSON shape: <c>{ "type": "ClickElement", "target": { "text": "Export" } }</c>.</summary>
    [Fact]
    public void ToJson_ClickElementAction_MatchesSectionFifteensTargetShape()
    {
        var workflow = new Workflow
        {
            Id = "wf",
            Name = "wf",
            Actions = [new ClickElementAction { Target = new ElementTarget { Text = "Export" }, RetryCount = 3, TimeoutMilliseconds = 10000 }]
        };

        var json = WorkflowSerializer.ToJson(workflow);

        Assert.Contains("\"type\": \"ClickElement\"", json);
        Assert.Contains("\"target\": {", json);
        Assert.Contains("\"text\": \"Export\"", json);
        Assert.Contains("\"retryCount\": 3", json);
        Assert.Contains("\"timeoutMilliseconds\": 10000", json);

        var reparsed = WorkflowSerializer.FromJson(json);
        var reparsedAction = Assert.IsType<ClickElementAction>(reparsed.Actions[0]);
        Assert.Equal("Export", reparsedAction.Target.Text);
    }

    [Fact]
    public void RoundTrip_PreservesEveryPhaseOneActionType()
    {
        var workflow = new Workflow
        {
            Id = "roundtrip",
            Name = "Round trip",
            Actions =
            [
                new OpenApplicationAction { Path = "/usr/bin/obs", Arguments = "--startrecording" },
                new CloseApplicationAction { ProcessName = "obs" },
                new MoveMouseAction { X = 10, Y = 20 },
                new ClickAction { X = 1, Y = 2, Button = MouseButton.Right },
                new DoubleClickAction { X = 3, Y = 4 },
                new RightClickAction { X = 5, Y = 6 },
                new MouseDownAction { Button = MouseButton.Middle },
                new MouseUpAction { Button = MouseButton.Middle },
                new ScrollAction { DeltaX = 1, DeltaY = -3 },
                new TypeTextAction { Text = "hello world" },
                new PressKeyAction { Key = Key.Enter },
                new KeyCombinationAction { Keys = [Key.LeftControl, Key.S] },
                new WaitAction { Milliseconds = 1500 },
                new TakeScreenshotAction { SavePath = "out.png" },
            ]
        };

        var json = WorkflowSerializer.ToJson(workflow);
        var parsed = WorkflowSerializer.FromJson(json);

        Assert.Equal(workflow.Actions.Count, parsed.Actions.Count);
        for (var i = 0; i < workflow.Actions.Count; i++)
        {
            Assert.Equal(workflow.Actions[i].ActionType, parsed.Actions[i].ActionType);
            Assert.Equal(workflow.Actions[i].GetType(), parsed.Actions[i].GetType());
        }

        var reparsedClick = Assert.IsType<ClickAction>(parsed.Actions[3]);
        Assert.Equal(1, reparsedClick.X);
        Assert.Equal(2, reparsedClick.Y);
        Assert.Equal(MouseButton.Right, reparsedClick.Button);

        var reparsedCombo = Assert.IsType<KeyCombinationAction>(parsed.Actions[11]);
        Assert.Equal([Key.LeftControl, Key.S], reparsedCombo.Keys);
    }

    [Fact]
    public async Task SaveAndLoadFromFile_RoundTrips()
    {
        var workflow = BuildSampleWorkflow();
        var path = Path.Combine(Path.GetTempPath(), $"automationstudio-test-{Guid.NewGuid():n}.json");

        try
        {
            await WorkflowSerializer.SaveToFileAsync(workflow, path);
            var loaded = await WorkflowSerializer.LoadFromFileAsync(path);

            Assert.Equal(workflow.Id, loaded.Id);
            Assert.Equal(workflow.Actions.Count, loaded.Actions.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FromJson_UnknownActionType_ThrowsRatherThanSilentlyDropping()
    {
        const string json = """
        {
          "id": "bad",
          "name": "bad",
          "version": 1,
          "actions": [ { "type": "SomeFutureActionType" } ]
        }
        """;

        Assert.ThrowsAny<System.Text.Json.JsonException>(() => WorkflowSerializer.FromJson(json));
    }
}
