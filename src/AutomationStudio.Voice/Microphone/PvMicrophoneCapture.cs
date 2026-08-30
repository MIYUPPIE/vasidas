using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AutomationStudio.Abstractions;
using Pv;

namespace AutomationStudio.Voice.Microphone;

/// <summary>
/// Wraps PvRecorder (Picovoice) — confirmed live on this dev machine to open the real capture
/// device and return real 16kHz mono frames, matching Whisper's native input rate exactly (no
/// resampling needed on the common path). <see cref="PvRecorder.Read"/> blocks until a frame is
/// ready, so capture runs on a dedicated background thread and frames cross to the async consumer
/// through a bounded channel — an async method can't call a blocking API directly without either
/// blocking a thread-pool thread or, as here, isolating the blocking call on its own thread.
/// </summary>
public sealed class PvMicrophoneCapture : IMicrophoneCapture
{
    private readonly PvRecorder _recorder;

    public PvMicrophoneCapture(int frameLength = 512)
    {
        _recorder = PvRecorder.Create(frameLength: frameLength);
    }

    public int SampleRate => _recorder.SampleRate;

    public async IAsyncEnumerable<float[]> CaptureAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<float[]>(new BoundedChannelOptions(32) { FullMode = BoundedChannelFullMode.DropOldest });

        _recorder.Start();
        var captureThread = new Thread(() => RunCaptureLoop(channel.Writer, cancellationToken))
        {
            IsBackground = true,
            Name = "PvMicrophoneCapture"
        };
        captureThread.Start();

        try
        {
            await foreach (var frame in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return frame;
            }
        }
        finally
        {
            _recorder.Stop();
        }
    }

    private void RunCaptureLoop(ChannelWriter<float[]> writer, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                short[] frame;
                try
                {
                    frame = _recorder.Read();
                }
                catch (Exception)
                {
                    // Stop()/Dispose() racing with an in-flight Read() from the consumer side surfaces
                    // as PvRecorderInvalidArgumentException, not ObjectDisposedException (confirmed
                    // live — an unhandled exception here would otherwise crash the whole process, since
                    // this runs on a raw Thread, not a Task). Either way, the recorder is going away;
                    // end the loop quietly rather than assume which exception type it throws.
                    break;
                }

                writer.TryWrite(ToFloatSamples(frame));
            }
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private static float[] ToFloatSamples(short[] frame)
    {
        var samples = new float[frame.Length];
        for (var i = 0; i < frame.Length; i++)
        {
            samples[i] = frame[i] / 32768f;
        }

        return samples;
    }

    public void Dispose() => _recorder.Dispose();
}
