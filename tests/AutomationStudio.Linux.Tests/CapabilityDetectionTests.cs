using AutomationStudio.Abstractions;
using AutomationStudio.Linux;

namespace AutomationStudio.Linux.Tests;

/// <summary>
/// Runs against whatever session actually launched the test process — on the dev machine this is
/// a real COSMIC/Wayland session with XWayland reachable, which is exactly the scenario section 31
/// describes: Wayland restricts raw injection in general, but XWayland gives us a working X11
/// display in practice, so capabilities should come back Available, not Restricted.
/// </summary>
public class CapabilityDetectionTests
{
    [Fact]
    public void DetectCapabilities_ReportsLinuxAsOperatingSystem()
    {
        var provider = new LinuxCapabilityProvider();
        var capabilities = provider.DetectCapabilities();

        Assert.Equal("Linux", capabilities.OperatingSystem);
    }

    [Fact]
    public void DetectCapabilities_NeverThrows_RegardlessOfSessionType()
    {
        var provider = new LinuxCapabilityProvider();
        var capabilities = provider.DetectCapabilities();

        Assert.True(Enum.IsDefined(capabilities.DisplayServer));
        Assert.True(Enum.IsDefined(capabilities.MouseInjection));
        Assert.True(Enum.IsDefined(capabilities.KeyboardInjection));
        Assert.True(Enum.IsDefined(capabilities.ScreenCapture));
    }

    [Fact]
    public void DetectCapabilities_WhenRestricted_AlwaysExplainsWhy()
    {
        var provider = new LinuxCapabilityProvider();
        var capabilities = provider.DetectCapabilities();

        var anyRestricted = capabilities.MouseInjection != CapabilityState.Available
            || capabilities.ScreenCapture != CapabilityState.Available;

        if (anyRestricted)
        {
            Assert.NotEmpty(capabilities.Notes);
        }
    }

    [Fact]
    public void DetectCapabilities_OnThisDevMachine_XWaylandMakesInputAndCaptureAvailable()
    {
        if (Environment.GetEnvironmentVariable("DISPLAY") is null)
        {
            return; // no $DISPLAY here (e.g. a headless CI runner) — nothing to assert.
        }

        var capabilities = new LinuxCapabilityProvider().DetectCapabilities();

        Assert.Equal(CapabilityState.Available, capabilities.MouseInjection);
        Assert.Equal(CapabilityState.Available, capabilities.KeyboardInjection);
        Assert.Equal(CapabilityState.Available, capabilities.ScreenCapture);
    }
}
