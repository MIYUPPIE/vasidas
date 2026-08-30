using AutomationStudio.Applications.Planning;

namespace AutomationStudio.Applications.Tests;

public class ApplicationLaunchRegistryTests
{
    [Fact]
    public void ResolvePath_ForAnUnregisteredName_ReturnsNull()
    {
        var registry = new ApplicationLaunchRegistry();

        Assert.Null(registry.ResolvePath("obs"));
    }

    [Fact]
    public void Register_ThenResolvePath_ReturnsTheRegisteredPath()
    {
        var registry = new ApplicationLaunchRegistry();

        registry.Register("obs", "/usr/bin/obs");

        Assert.Equal("/usr/bin/obs", registry.ResolvePath("obs"));
    }

    [Fact]
    public void ResolvePath_IsCaseInsensitive()
    {
        var registry = new ApplicationLaunchRegistry();
        registry.Register("OBS", "/usr/bin/obs");

        Assert.Equal("/usr/bin/obs", registry.ResolvePath("obs"));
        Assert.Equal("/usr/bin/obs", registry.ResolvePath("Obs"));
    }

    [Fact]
    public void Register_CalledTwiceForTheSameName_OverwritesThePreviousPath()
    {
        var registry = new ApplicationLaunchRegistry();
        registry.Register("obs", "/old/path");

        registry.Register("obs", "/new/path");

        Assert.Equal("/new/path", registry.ResolvePath("obs"));
    }
}
