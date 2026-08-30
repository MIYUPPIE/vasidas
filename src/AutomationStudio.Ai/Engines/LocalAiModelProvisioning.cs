namespace AutomationStudio.Ai.Engines;

/// <summary>
/// One-time setup for the local LLM model file — never committed to the repo (a model weight, not
/// source; CLAUDE.md bans committing model weights, and design doc section 29 explicitly allows AI
/// models to be a separate install because of their size). Downloads once to the given path and
/// reuses it on every later run; nothing here runs on the hot path of an actual command.
/// </summary>
public static class LocalAiModelProvisioning
{
    /// <summary>
    /// Qwen2.5-1.5B-Instruct, Q4_K_M quantization (~1GB) — confirmed live before choosing this:
    /// loads via LLamaSharp's CPU backend and reliably produces well-formed JSON for both intent
    /// classification and workflow-generation prompts on this dev machine. Small enough to run on
    /// modest hardware with no GPU required, strong enough at structured output for what this
    /// project actually asks of it (classify against a fixed list, or plan within a fixed action
    /// schema — never open-ended chat).
    /// </summary>
    public const string DefaultModelUrl =
        "https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/qwen2.5-1.5b-instruct-q4_k_m.gguf";

    private static readonly HttpClient HttpClient = new();

    public static async Task<string> EnsureModelAsync(string modelPath, string? modelUrl = null, CancellationToken cancellationToken = default)
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

        // Downloaded to a temp file and atomically moved into place, matching
        // WhisperModelProvisioning's own pattern — a process killed mid-download must never leave a
        // truncated file at the real path (File.Exists would then wrongly report "already cached").
        var tempPath = modelPath + ".downloading";
        using (var response = await HttpClient
            .GetAsync(modelUrl ?? DefaultModelUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var fileStream = File.Create(tempPath);
            await contentStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, modelPath, overwrite: true);
        return modelPath;
    }
}
