using AutomationStudio.Abstractions;
using AutomationStudio.Windows.Win32;

namespace AutomationStudio.Windows.Tests;

/// <summary>
/// Pure-logic tests: <see cref="VirtualKeyMapper"/> is a lookup table, no P/Invoke, so it can be
/// verified from any OS. Everything that actually calls into user32/gdi32 cannot — there is no
/// Windows machine in this dev environment — so those paths are reviewed by inspection against the
/// documented Win32 ABI instead (see <see cref="NativeMethodsLayoutTests"/> for the one thing about
/// them that *can* be checked cross-platform: struct layout/size).
/// </summary>
public class VirtualKeyMapperTests
{
    public static IEnumerable<object[]> AllKeysExceptNone() =>
        Enum.GetValues<Key>().Where(k => k != Key.None).Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(AllKeysExceptNone))]
    public void Resolve_EveryDefinedKey_HasAMapping(Key key)
    {
        var vk = VirtualKeyMapper.Resolve(key);
        Assert.True(vk.Code > 0);
    }

    [Theory]
    [InlineData(Key.A, 0x41)]
    [InlineData(Key.Z, 0x5A)]
    [InlineData(Key.D0, 0x30)]
    [InlineData(Key.D9, 0x39)]
    [InlineData(Key.Enter, 0x0D)]
    [InlineData(Key.Escape, 0x1B)]
    [InlineData(Key.Space, 0x20)]
    [InlineData(Key.F1, 0x70)]
    [InlineData(Key.F12, 0x7B)]
    [InlineData(Key.LeftControl, 0xA2)]
    public void Resolve_KnownKeys_MatchDocumentedVirtualKeyCodes(Key key, ushort expectedCode)
    {
        Assert.Equal(expectedCode, VirtualKeyMapper.Resolve(key).Code);
    }

    [Theory]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    [InlineData(Key.Home)]
    [InlineData(Key.End)]
    [InlineData(Key.Insert)]
    [InlineData(Key.Delete)]
    [InlineData(Key.RightControl)]
    public void Resolve_NavigationAndRightModifierKeys_AreMarkedExtended(Key key)
    {
        Assert.True(VirtualKeyMapper.Resolve(key).IsExtended);
    }

    [Theory]
    [InlineData(Key.A)]
    [InlineData(Key.Enter)]
    [InlineData(Key.LeftControl)]
    public void Resolve_MainBlockKeys_AreNotMarkedExtended(Key key)
    {
        Assert.False(VirtualKeyMapper.Resolve(key).IsExtended);
    }

    [Theory]
    [MemberData(nameof(AllKeysExceptNone))]
    public void TryReverse_EveryDefinedKeysVirtualKeyCode_RoundTripsBackToTheSameKey(Key key)
    {
        var vk = VirtualKeyMapper.Resolve(key);
        Assert.Equal(key, VirtualKeyMapper.TryReverse(vk.Code));
    }

    [Fact]
    public void TryReverse_UnknownVirtualKeyCode_ReturnsNull()
    {
        Assert.Null(VirtualKeyMapper.TryReverse(0xFFFF));
    }
}
