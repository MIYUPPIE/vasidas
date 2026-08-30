using AutomationStudio.Applications.VideoPsalm;

namespace AutomationStudio.Applications.Tests;

public class VideoPsalmProfileTests
{
    [Fact]
    public void ToJson_ThenLoadFromJson_RoundTripsEveryField()
    {
        var json = VideoPsalmProfile.Default.ToJson();
        var reloaded = VideoPsalmProfile.LoadFromJson(json);

        Assert.Equal(VideoPsalmProfile.Default, reloaded);
    }

    [Fact]
    public void LoadFromJson_AllowsOverridingASingleShortcut_WithoutRespecifyingEverything()
    {
        var overridden = VideoPsalmProfile.Default with { BlackScreenKey = AutomationStudio.Abstractions.Key.Period };

        var reloaded = VideoPsalmProfile.LoadFromJson(overridden.ToJson());

        Assert.Equal(AutomationStudio.Abstractions.Key.Period, reloaded.BlackScreenKey);
        Assert.Equal(VideoPsalmProfile.Default.WindowTitleContains, reloaded.WindowTitleContains);
    }
}
