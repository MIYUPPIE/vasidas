using AutomationStudio.Abstractions;

namespace AutomationStudio.Linux.Tests;

/// <summary>
/// Exercises the real X11 RECORD extension against whatever session this test process runs under —
/// this is the live end-to-end proof that the empirically-derived wire-format byte offsets in
/// <c>AutomationStudio.Linux.X11.X11InputRecorder</c> are actually correct, not just reasoned about.
/// Injects synthetic input via the same real <c>IDesktopController</c> Phase 1's own tests already
/// proved works on this machine, so a real click/keystroke really does round-trip back out as a
/// captured <see cref="RecordedInputEvent"/>.
/// </summary>
public class X11InputRecorderTests
{
    private static bool HasDisplay => Environment.GetEnvironmentVariable("DISPLAY") is not null;

    [Fact]
    public async Task StartAndStop_AroundRealInjectedInput_CapturesItBackOut()
    {
        if (!HasDisplay)
        {
            return; // headless environment; nothing to capture.
        }

        using var environment = LinuxAutomationEnvironment.Create();
        if (environment.InputRecorder is not { } recorder)
        {
            return; // RECORD extension not available on this session.
        }

        recorder.Start();
        await Task.Delay(300);

        await environment.DesktopController.ClickAsync(MouseButton.Left, new ScreenPoint(300, 200));
        await Task.Delay(100);
        await environment.DesktopController.TypeTextAsync("hi");
        await Task.Delay(300);

        var events = recorder.Stop();

        Assert.Contains(events, e =>
            e.Kind == RecordedInputEventKind.MouseDown && e.Button == MouseButton.Left &&
            e.Position.X == 300 && e.Position.Y == 200);
        Assert.Contains(events, e =>
            e.Kind == RecordedInputEventKind.MouseUp && e.Button == MouseButton.Left &&
            e.Position.X == 300 && e.Position.Y == 200);

        var typedCharacters = events
            .Where(e => e.Kind == RecordedInputEventKind.KeyDown && e.Character is not null)
            .Select(e => e.Character)
            .ToList();
        Assert.Contains("h", typedCharacters);
        Assert.Contains("i", typedCharacters);
    }

    [Fact]
    public void Stop_WithoutStart_Throws()
    {
        if (!HasDisplay)
        {
            return;
        }

        using var environment = LinuxAutomationEnvironment.Create();
        if (environment.InputRecorder is not { } recorder)
        {
            return;
        }

        Assert.Throws<InvalidOperationException>(() => recorder.Stop());
    }

    [Fact]
    public void Start_CalledTwiceWithoutStop_Throws()
    {
        if (!HasDisplay)
        {
            return;
        }

        using var environment = LinuxAutomationEnvironment.Create();
        if (environment.InputRecorder is not { } recorder)
        {
            return;
        }

        recorder.Start();
        try
        {
            Assert.Throws<InvalidOperationException>(recorder.Start);
        }
        finally
        {
            recorder.Stop();
        }
    }

    [Fact]
    public async Task StartStopStart_SupportsMultipleRecordingSessionsOnTheSameRecorder()
    {
        if (!HasDisplay)
        {
            return;
        }

        using var environment = LinuxAutomationEnvironment.Create();
        if (environment.InputRecorder is not { } recorder)
        {
            return;
        }

        recorder.Start();
        await Task.Delay(200);
        await environment.DesktopController.ClickAsync(MouseButton.Left, new ScreenPoint(100, 100));
        await Task.Delay(200);
        var first = recorder.Stop();

        recorder.Start();
        await Task.Delay(200);
        await environment.DesktopController.ClickAsync(MouseButton.Left, new ScreenPoint(150, 150));
        await Task.Delay(200);
        var second = recorder.Stop();

        Assert.Contains(first, e => e.Kind == RecordedInputEventKind.MouseDown && e.Position.X == 100);
        Assert.Contains(second, e => e.Kind == RecordedInputEventKind.MouseDown && e.Position.X == 150);
        Assert.DoesNotContain(second, e => e.Kind == RecordedInputEventKind.MouseDown && e.Position.X == 100);
    }
}
