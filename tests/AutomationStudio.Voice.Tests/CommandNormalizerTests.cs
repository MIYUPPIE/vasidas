using AutomationStudio.Voice.Commands;

namespace AutomationStudio.Voice.Tests;

public class CommandNormalizerTests
{
    [Theory]
    [InlineData("Start Recording", "start recording")]
    [InlineData("  start   recording  ", "start recording")]
    [InlineData("Start, Recording.", "start recording")]
    [InlineData("Open OBS!", "open obs")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_ProducesTheExpectedForm(string input, string expected)
    {
        Assert.Equal(expected, CommandNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_CollapsesMultipleWhitespaceIntoOneSpace()
    {
        Assert.Equal("next slide please", CommandNormalizer.Normalize("next\t\tslide\n\nplease"));
    }
}
