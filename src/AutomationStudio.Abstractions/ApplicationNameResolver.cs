namespace AutomationStudio.Abstractions;

/// <summary>
/// Whether a spoken/typed application name actually resolves to something launchable on this
/// machine (DAS.md section 13's Application Profiles concept), checked at intent-RECOGNITION time
/// rather than only at task-planning time. Without this, a generic "start &lt;anything&gt;" pattern
/// has no way to tell a real app-launch request ("start OBS") apart from an unrelated multi-step
/// request that merely starts with the word "start" (DAS.md section 4.3's own worked example, "Start
/// Sunday service") — it would claim the phrase, dispatch OPEN_APPLICATION, and only discover there's
/// no registered path for it once task planning fails, by which point the request never got a chance
/// to reach AI intent classification or dynamic workflow generation (Phase 10's Tier 2/3).
/// </summary>
public interface IApplicationNameResolver
{
    /// <summary>Returns whether <paramref name="name"/> is a launchable application on this machine.</summary>
    bool IsKnown(string name);
}
