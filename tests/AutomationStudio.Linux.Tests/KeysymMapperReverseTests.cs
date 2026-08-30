using AutomationStudio.Abstractions;
using AutomationStudio.Linux.X11;

namespace AutomationStudio.Linux.Tests;

/// <summary>
/// <see cref="KeysymMapper.FromKeysym"/> is what turns a keycode the input recorder (DAS.md section
/// 25) captures back into a logical <see cref="Key"/>/character — pure computation plus one
/// <c>XKeysymToString</c> call, which (unlike the RECORD extension itself) needs no open display
/// connection, so these run unconditionally rather than being skipped on a headless runner.
/// </summary>
public class KeysymMapperReverseTests
{
    [Theory]
    [InlineData(Key.A)]
    [InlineData(Key.Z)]
    [InlineData(Key.D5)]
    [InlineData(Key.Space)]
    [InlineData(Key.Comma)]
    public void FromKeysym_RoundTripsResolveKeysymForDirectCharKeys(Key key)
    {
        var keysym = KeysymMapper.ResolveKeysym(key);
        var (resolvedKey, character) = KeysymMapper.FromKeysym(keysym);

        Assert.Equal(key, resolvedKey);
        Assert.NotNull(character);
    }

    [Theory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Escape)]
    [InlineData(Key.F5)]
    [InlineData(Key.Left)]
    [InlineData(Key.LeftControl)]
    public void FromKeysym_RoundTripsResolveKeysymForNamedKeys(Key key)
    {
        var keysym = KeysymMapper.ResolveKeysym(key);
        var (resolvedKey, character) = KeysymMapper.FromKeysym(keysym);

        Assert.Equal(key, resolvedKey);
        Assert.Null(character);
    }

    [Fact]
    public void FromKeysym_UppercaseLatinKeysym_ReturnsUppercaseCharacterAndTheSameLogicalKey()
    {
        // XK_A = 0x41, distinct from XK_a = 0x61 — X11 has separate keysyms for the shifted case,
        // which is exactly why X11InputRecorder looks up index 1 (shifted) when the captured state
        // has ShiftMask set.
        var (key, character) = KeysymMapper.FromKeysym(0x41);

        Assert.Equal(Key.A, key);
        Assert.Equal("A", character);
    }

    [Fact]
    public void FromKeysym_UnicodeKeysymAboveLatin1_ReturnsTheCharacterWithNoLogicalKey()
    {
        var keysym = KeysymMapper.CodepointToKeysym(0x20AC); // '€', outside Latin-1 and every named key.
        var (key, character) = KeysymMapper.FromKeysym(keysym);

        Assert.Null(key);
        Assert.Equal("€", character);
    }

    [Fact]
    public void FromKeysym_UnassignedKeysym_ReturnsNullForBoth()
    {
        var (key, character) = KeysymMapper.FromKeysym(0x00FFFFFE);

        Assert.Null(key);
        Assert.Null(character);
    }
}
