using System.Diagnostics;
using AutomationStudio.Abstractions;
using AutomationStudio.Voice.SpeechToText;
using Whisper.net.Ggml;

namespace AutomationStudio.Voice.Tests;

/// <summary>
/// Exercises the real whisper.cpp pipeline — confirmed live before writing
/// <see cref="WhisperSpeechToText"/> that it loads a downloaded ggml model and transcribes real
/// audio end to end on this dev machine with no native-binary issues (unlike the OpenCvSharp
/// problem Phase 4's OCR research hit). The model is downloaded once to a shared temp path and
/// reused across the whole run — model download needs network access once; nothing about
/// transcription itself does afterward.
/// </summary>
public class WhisperSpeechToTextTests : IAsyncLifetime
{
    private static string ModelPath => Path.Combine(Path.GetTempPath(), "automationstudio-tests", "ggml-tiny.bin");

    public async Task InitializeAsync() => await WhisperModelProvisioning.EnsureModelAsync(ModelPath, GgmlType.Tiny);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EnsureModelAsync_DownloadsOnce_AndReusesTheCachedFileOnLaterCalls()
    {
        Assert.True(File.Exists(ModelPath));
        var sizeAfterFirstEnsure = new FileInfo(ModelPath).Length;

        // Should be a no-op file-existence check now, not a second download.
        var stopwatch = Stopwatch.StartNew();
        await WhisperModelProvisioning.EnsureModelAsync(ModelPath, GgmlType.Tiny);
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"expected a cached no-op, took {stopwatch.ElapsedMilliseconds}ms");
        Assert.Equal(sizeAfterFirstEnsure, new FileInfo(ModelPath).Length);
    }

    [Fact]
    public async Task TranscribeAsync_AgainstRealAudio_RunsEndToEndWithoutThrowing()
    {
        using var stt = new WhisperSpeechToText(ModelPath);
        var audio = new AudioSamples { Samples = GenerateTone(440, TimeSpan.FromSeconds(2), 16000), SampleRate = 16000 };

        var text = await stt.TranscribeAsync(audio);

        // A pure tone isn't real speech, so the exact text isn't asserted — the load-bearing check
        // is that the whole pipeline (model load, inference, segment concatenation) runs without
        // throwing and returns a string rather than hanging or crashing.
        Assert.NotNull(text);
    }

    [Fact]
    public async Task TranscribeAsync_OnSilence_ReturnsWithoutThrowing()
    {
        using var stt = new WhisperSpeechToText(ModelPath);
        var audio = new AudioSamples { Samples = new float[16000 * 2], SampleRate = 16000 }; // 2s of silence

        var text = await stt.TranscribeAsync(audio);

        Assert.NotNull(text);
    }

    private static float[] GenerateTone(double frequencyHz, TimeSpan duration, int sampleRate)
    {
        var sampleCount = (int)(duration.TotalSeconds * sampleRate);
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = (float)(0.3 * Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate));
        }

        return samples;
    }
}
