using AutomationStudio.Voice.Microphone;

namespace AutomationStudio.Voice.Tests;

/// <summary>
/// Exercises the real capture device on whatever machine runs this suite — confirmed live on this
/// dev machine before writing <see cref="PvMicrophoneCapture"/> that PvRecorder opens the real
/// hardware and returns real 16kHz mono frames. Safe to run unattended: it only reads from the
/// microphone, never plays anything or asserts on the audio's *content* (ambient noise varies
/// machine to machine), only its shape.
/// </summary>
public class PvMicrophoneCaptureTests
{
    private static bool HasCaptureDevice()
    {
        try
        {
            return Pv.PvRecorder.GetAvailableDevices().Length > 0;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task CaptureAsync_OnARealDevice_YieldsRealFramesAt16kHz()
    {
        if (!HasCaptureDevice())
        {
            return; // no audio input device on this machine (e.g. a minimal CI runner).
        }

        using var capture = new PvMicrophoneCapture();
        Assert.Equal(16000, capture.SampleRate);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var frameCount = 0;
        await foreach (var frame in capture.CaptureAsync(cts.Token))
        {
            Assert.True(frame.Length > 0);
            Assert.All(frame, sample => Assert.InRange(sample, -1.0f, 1.0f));

            frameCount++;
            if (frameCount >= 10)
            {
                break;
            }
        }

        Assert.Equal(10, frameCount);
    }

    [Fact]
    public async Task CaptureAsync_StopsCleanlyOnCancellation_RatherThanHanging()
    {
        if (!HasCaptureDevice())
        {
            return;
        }

        using var capture = new PvMicrophoneCapture();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        // The real assertion is that this completes at all — a prior version of this loop could
        // deadlock waiting for a blocking PvRecorder.Read() that never unblocked after Stop().
        var completed = await Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in capture.CaptureAsync(cts.Token))
                {
                }
            }
            catch (OperationCanceledException)
            {
                // expected once the token fires.
            }

            return true;
        }).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(completed);
    }
}
