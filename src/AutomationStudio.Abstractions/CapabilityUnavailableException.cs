namespace AutomationStudio.Abstractions;

/// <summary>
/// Thrown when a workflow asks for a capability the current session cannot provide — e.g. mouse
/// injection on a Wayland session with no XWayland (section 31). The point of a dedicated
/// exception type is section 32's requirement to report unsupported capabilities clearly rather
/// than fail with an opaque native error.
/// </summary>
public sealed class CapabilityUnavailableException(string capability, string reason)
    : Exception($"{capability} is unavailable in this session: {reason}")
{
    public string Capability { get; } = capability;
}
