using System.Text;
using AutomationStudio.Abstractions;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace AutomationStudio.Ai.Engines;

/// <summary>
/// Local offline LLM inference via LLamaSharp/llama.cpp (design doc section 23) — confirmed live on
/// this dev machine: loads a GGUF model on the CPU backend and produces well-formed, correctly
/// classified JSON for real intent-classification prompts. Uses ChatML-style prompt formatting
/// (<c>&lt;|im_start|&gt;</c>/<c>&lt;|im_end|&gt;</c>), which the chosen model (Qwen2.5-Instruct)
/// expects; a different model family would need different formatting here.
/// </summary>
public sealed class LlamaSharpEngine : ILocalAiEngine, IDisposable
{
    private readonly LLamaWeights _model;
    private readonly StatelessExecutor _executor;

    /// <summary>
    /// <see cref="StatelessExecutor"/>, not <see cref="LLama.InteractiveExecutor"/> — confirmed live
    /// that the interactive executor retains conversation state across calls on a shared context,
    /// which made the second and third of three independent classification prompts return empty
    /// output. Every call this project makes to a local AI engine is an independent, one-shot
    /// completion (design doc section 23's flow: one command in, one classification/plan out), never
    /// a multi-turn conversation, so the stateless executor is the correct choice, not just a
    /// workaround for that symptom.
    /// </summary>
    public LlamaSharpEngine(string modelPath, uint contextSize = 2048)
    {
        var parameters = new ModelParams(modelPath) { ContextSize = contextSize, GpuLayerCount = 0 };
        _model = LLamaWeights.LoadFromFile(parameters);
        _executor = new StatelessExecutor(_model, parameters);
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var prompt = $"<|im_start|>system\n{systemPrompt}<|im_end|>\n<|im_start|>user\n{userPrompt}<|im_end|>\n<|im_start|>assistant\n";

        var inferenceParams = new InferenceParams
        {
            MaxTokens = 512,
            AntiPrompts = ["<|im_end|>", "<|im_start|>"],
            // Low temperature deliberately: this project only ever asks for classification against a
            // fixed list or planning within a fixed action schema, never creative generation — a
            // near-deterministic, most-likely-token answer is what's wanted here.
            SamplingPipeline = new DefaultSamplingPipeline { Temperature = 0.1f },
        };

        var builder = new StringBuilder();
        await foreach (var token in _executor.InferAsync(prompt, inferenceParams).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            builder.Append(token);
        }

        return builder.ToString().Trim();
    }

    public void Dispose() => _model.Dispose();
}
