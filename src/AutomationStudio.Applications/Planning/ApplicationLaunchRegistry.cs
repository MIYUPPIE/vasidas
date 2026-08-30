using AutomationStudio.Abstractions;

namespace AutomationStudio.Applications.Planning;

/// <summary>
/// Maps a spoken/typed application name ("obs") to its real launch path — DAS.md section 13's
/// "Application Profiles" concept, applied to the one thing a generic OPEN_APPLICATION intent
/// needs that a plugin like VideoPsalmPlugin doesn't: a real executable path, which varies by
/// machine and isn't something to guess. Empty by default — deliberately: hardcoding a path for a
/// machine this project doesn't run on would be exactly the kind of unverified guess this codebase
/// avoids elsewhere. A caller (the UI's composition root, a settings file once one exists)
/// registers real paths for its own machine.
/// </summary>
public sealed class ApplicationLaunchRegistry
{
    private readonly Dictionary<string, string> _pathsByName = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string name, string path) => _pathsByName[name] = path;

    public string? ResolvePath(string name) => _pathsByName.TryGetValue(name, out var path) ? path : null;
}

/// <summary>
/// Adapts <see cref="ApplicationLaunchRegistry"/> to <see cref="IApplicationNameResolver"/> — the
/// interface lives in Abstractions since <c>AutomationStudio.Voice</c> (where
/// <c>DeterministicIntentRecognizer</c> needs it) doesn't and shouldn't reference
/// <c>AutomationStudio.Applications</c>, only the other way around.
/// </summary>
public sealed class ApplicationLaunchRegistryResolver(ApplicationLaunchRegistry registry) : IApplicationNameResolver
{
    public bool IsKnown(string name) => registry.ResolvePath(name) is not null;
}
