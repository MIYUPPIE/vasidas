using System.Text.RegularExpressions;
using AutomationStudio.Abstractions;
using AutomationStudio.Voice.Commands;

namespace AutomationStudio.Voice.Intents;

/// <summary>
/// Rule-based intent matching for Phase 3's command set (DAS.md section 37): "Open OBS", "Start
/// recording", "Stop recording", "Next slide", "Pause", "Resume" — plus reasonable synonyms
/// (section 7 requires synonym support). Deterministic pattern matching, not an LLM: DAS.md's own
/// roadmap (Phase 10) is explicit that local AI only enters the picture once this layer is stable.
/// </summary>
public sealed partial class DeterministicIntentRecognizer : IIntentRecognizer
{
    private static readonly (Regex Pattern, Func<Match, Intent> Build)[] Rules =
    [
        (StartRecordingPattern(), _ => Intent.Simple("START_RECORDING")),
        (StopRecordingPattern(), _ => Intent.Simple("STOP_RECORDING")),
        (NextSlidePattern(), _ => Intent.Simple("NEXT_SLIDE")),
        (PreviousSlidePattern(), _ => Intent.Simple("PREVIOUS_SLIDE")),
        (PausePattern(), _ => Intent.Simple("PAUSE_MEDIA")),
        (ResumePattern(), _ => Intent.Simple("PLAY_MEDIA")),
        (OpenApplicationPattern(), m => new Intent("OPEN_APPLICATION", new Dictionary<string, string> { ["application"] = m.Groups["app"].Value })),
        (CloseApplicationPattern(), m => new Intent("CLOSE_APPLICATION", new Dictionary<string, string> { ["application"] = m.Groups["app"].Value })),
    ];

    public Intent? Recognize(string text)
    {
        var normalized = CommandNormalizer.Normalize(text);
        if (normalized.Length == 0)
        {
            return null;
        }

        foreach (var (pattern, build) in Rules)
        {
            var match = pattern.Match(normalized);
            if (match.Success)
            {
                return build(match);
            }
        }

        return null;
    }

    [GeneratedRegex(@"^(start|begin)\s+recording$")]
    private static partial Regex StartRecordingPattern();

    [GeneratedRegex(@"^stop\s+recording$")]
    private static partial Regex StopRecordingPattern();

    [GeneratedRegex(@"^next(\s+slide)?$")]
    private static partial Regex NextSlidePattern();

    [GeneratedRegex(@"^(previous|back|go\s+back)(\s+slide)?$")]
    private static partial Regex PreviousSlidePattern();

    [GeneratedRegex(@"^pause( it)?$")]
    private static partial Regex PausePattern();

    [GeneratedRegex(@"^(resume|play|continue|unpause)( it)?$")]
    private static partial Regex ResumePattern();

    [GeneratedRegex(@"^(open|launch|start)\s+(?<app>.+)$")]
    private static partial Regex OpenApplicationPattern();

    [GeneratedRegex(@"^close\s+(?<app>.+)$")]
    private static partial Regex CloseApplicationPattern();
}
