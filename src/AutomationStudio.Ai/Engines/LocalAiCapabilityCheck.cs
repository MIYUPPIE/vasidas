namespace AutomationStudio.Ai.Engines;

/// <summary>Whether the local AI model is actually present — cheap and real, a file-existence check only (no model load, matching section 39's "no continuous/expensive inference just to answer a capability question," the same discipline <c>OcrCapabilityCheck</c> already follows).</summary>
public static class LocalAiCapabilityCheck
{
    public static bool IsModelPresent(string modelPath) => File.Exists(modelPath);
}
