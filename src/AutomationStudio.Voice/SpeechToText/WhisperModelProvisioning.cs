using Whisper.net.Ggml;

namespace AutomationStudio.Voice.SpeechToText;

/// <summary>
/// One-time setup for the local Whisper model file — never committed to the repo (a model weight,
/// not source; CLAUDE.md bans committing model weights, and section 29 explicitly allows AI models
/// to be a separate install because of their size). Downloads once to the given path and reuses it
/// on every later run; nothing here runs on the hot path of an actual voice command.
/// </summary>
public static class WhisperModelProvisioning
{
    /// <summary>"Base" balances accuracy against the model being usable on modest hardware; callers needing something smaller/larger can pass a different <see cref="GgmlType"/>.</summary>
    public static async Task<string> EnsureModelAsync(string modelPath, GgmlType modelType = GgmlType.Base, CancellationToken cancellationToken = default)
    {
        if (File.Exists(modelPath))
        {
            return modelPath;
        }

        var directory = Path.GetDirectoryName(modelPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(modelType, cancellationToken: cancellationToken).ConfigureAwait(false);
        var tempPath = modelPath + ".downloading";
        using (var fileStream = File.Create(tempPath))
        {
            await modelStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, modelPath, overwrite: true);
        return modelPath;
    }
}
