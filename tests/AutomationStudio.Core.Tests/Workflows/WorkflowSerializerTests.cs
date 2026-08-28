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
