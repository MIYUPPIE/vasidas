using System.Runtime.InteropServices;
using AutomationStudio.Windows.Win32;

namespace AutomationStudio.Windows.Tests;

/// <summary>
/// <c>Marshal.SizeOf</c> computes managed struct layout without needing the target native library
/// to be loadable, so these run fine on Linux even though the structs exist purely for the Win32
/// ABI. They exist because <see cref="NativeMethods.Input"/>'s layout is exactly the kind of thing
/// that silently breaks <c>SendInput</c> at runtime if a field is missing or misordered — this is
/// the only automated check this dev environment can give that struct before it ever runs on real
/// Windows. Expected sizes are the well-documented x64 Win32 ABI values for these exact structs.
/// </summary>
public class NativeMethodsLayoutTests
{
    [Fact]
    public void MouseInput_MatchesDocumentedX64Size()
    {
        Assert.Equal(32, Marshal.SizeOf<NativeMethods.MouseInput>());
    }

    [Fact]
    public void KeybdInput_MatchesDocumentedX64Size()
    {
        Assert.Equal(24, Marshal.SizeOf<NativeMethods.KeybdInput>());
    }

    [Fact]
    public void InputUnion_SizeIsTheLargestVariant()
    {
        Assert.Equal(32, Marshal.SizeOf<NativeMethods.InputUnion>());
    }

    [Fact]
    public void Input_MatchesDocumentedX64Size()
    {
        // type (4 bytes) padded to 8-byte alignment for the union + the 32-byte union = 40.
        Assert.Equal(40, Marshal.SizeOf<NativeMethods.Input>());
    }

    [Fact]
    public void BitmapInfoHeader_MatchesDocumentedSize()
    {
        // BITMAPINFOHEADER's own biSize field is documented as 40 for this exact layout.
        Assert.Equal(40, Marshal.SizeOf<NativeMethods.BitmapInfoHeader>());
    }

    [Fact]
    public void InputUnion_MouseAndKeyboardFields_ShareOffsetZero()
    {
        var mouseOffset = Marshal.OffsetOf<NativeMethods.InputUnion>(nameof(NativeMethods.InputUnion.mi));
        var keyboardOffset = Marshal.OffsetOf<NativeMethods.InputUnion>(nameof(NativeMethods.InputUnion.ki));

        Assert.Equal(IntPtr.Zero, mouseOffset);
        Assert.Equal(IntPtr.Zero, keyboardOffset);
    }

    // The three structs below back WindowsInputRecorder (WH_KEYBOARD_LL/WH_MOUSE_LL, DAS.md section
    // 25). Documented x64 Win32 ABI sizes: KBDLLHOOKSTRUCT=24, MSLLHOOKSTRUCT=32, MSG=48.

    [Fact]
    public void KbdllHookStruct_MatchesDocumentedX64Size()
    {
        Assert.Equal(24, Marshal.SizeOf<NativeMethods.KbdllHookStruct>());
    }

    [Fact]
    public void MsllHookStruct_MatchesDocumentedX64Size()
    {
        Assert.Equal(32, Marshal.SizeOf<NativeMethods.MsllHookStruct>());
    }

    [Fact]
    public void Msg_MatchesDocumentedX64Size()
    {
        Assert.Equal(48, Marshal.SizeOf<NativeMethods.Msg>());
    }
}
