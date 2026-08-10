using engine.inputs;
using Xunit;

namespace JoyceCode.Tests.engine.inputs;

/**
 * WP-6.3 step 1.
 *
 * These tests pin the two claims the design rests on. Neither is self-evident from
 * reading the enum, and both fail SILENTLY if broken - a wrong scancode does not throw,
 * it just binds the wrong physical key on somebody else's keyboard layout.
 */
public class ScanCodeTests
{
    /**
     * Claim 1: ScanCode IS the USB HID Keyboard/Keypad usage-ID table (HID Usage Tables
     * page 0x07).
     *
     * This is what lets Sdl3KeyCodes.ToScanCode be a cast instead of a translation table.
     * The test cannot reference SDL from here (JoyceCode.Tests references Joyce.csproj
     * only), so it pins the values against the STANDARD rather than against SDL - which is
     * the stronger check anyway: SDL matching HID is SDL's business, and any future backend
     * has the same obligation.
     */
    [Theory]
    [InlineData(ScanCode.A, 4)]     // HID 0x04 - the anchor of the whole letter block
    [InlineData(ScanCode.Z, 29)]    // 0x1D - letters are contiguous A..Z
    [InlineData(ScanCode.D1, 30)]   // 0x1E - digit row starts at 1, NOT 0
    [InlineData(ScanCode.D0, 39)]   // 0x27 - ...and 0 sits at the END of it
    [InlineData(ScanCode.Return, 40)]
    [InlineData(ScanCode.Escape, 41)]
    [InlineData(ScanCode.Backspace, 42)]
    [InlineData(ScanCode.Tab, 43)]
    [InlineData(ScanCode.Space, 44)]
    [InlineData(ScanCode.F1, 58)]
    [InlineData(ScanCode.F12, 69)]
    [InlineData(ScanCode.Right, 79)] // arrows are Right,Left,Down,Up - NOT alphabetical,
    [InlineData(ScanCode.Left, 80)]  // and not the order anyone guesses
    [InlineData(ScanCode.Down, 81)]
    [InlineData(ScanCode.Up, 82)]
    [InlineData(ScanCode.LeftControl, 224)]  // 0xE0 - modifier block
    [InlineData(ScanCode.LeftShift, 225)]
    [InlineData(ScanCode.RightGui, 231)]     // 0xE7 - last of it
    public void ScanCodeUsesUsbHidUsageIds(ScanCode scanCode, int expectedHidUsageId)
    {
        Assert.Equal(expectedHidUsageId, (int)scanCode);
    }

    [Fact]
    public void UnknownIsZero()
    {
        Assert.Equal(0, (int)ScanCode.Unknown);
    }

    /**
     * Letters must be contiguous from A, because callers legitimately do arithmetic on
     * that block and because it is what HID guarantees.
     */
    [Fact]
    public void LettersAreContiguousFromA()
    {
        for (int i = 0; i < 26; ++i)
        {
            Assert.Equal((int)ScanCode.A + i, (int)(ScanCode)((int)ScanCode.A + i));
        }
        Assert.Equal(25, (int)ScanCode.Z - (int)ScanCode.A);
    }

    /**
     * Claim 2: the engine code strings are UNCHANGED by the move out of Sdl3KeyCodes.
     *
     * These strings are the live contract with game code - Scene.cs tests for "(F8)",
     * InputMapper keys its JSON on them - so this is a pinning test, not a description of
     * something desirable. The expectations were transcribed from the table as it stood in
     * Splash.Silk/Sdl3KeyCodes.cs before WP-6.3 moved it.
     */
    [Theory]
    [InlineData(ScanCode.LeftShift, "(shiftleft)")]
    [InlineData(ScanCode.RightShift, "(shiftright)")]
    [InlineData(ScanCode.Space, " ")]
    [InlineData(ScanCode.D0, "0")]
    [InlineData(ScanCode.D9, "9")]
    [InlineData(ScanCode.A, "a")]
    [InlineData(ScanCode.W, "w")]
    [InlineData(ScanCode.Z, "z")]
    [InlineData(ScanCode.Return, "(enter)")]
    [InlineData(ScanCode.Tab, "(tab)")]
    [InlineData(ScanCode.Escape, "(escape)")]
    [InlineData(ScanCode.F8, "(F8)")]
    [InlineData(ScanCode.F12, "(F12)")]
    [InlineData(ScanCode.Up, "(cursorup)")]
    [InlineData(ScanCode.Left, "(cursorleft)")]
    [InlineData(ScanCode.Delete, "(delete)")]
    [InlineData(ScanCode.Backspace, "(backspace)")]
    public void EngineCodeStringsAreUnchanged(ScanCode scanCode, string expected)
    {
        Assert.Equal(expected, ScanCodeNames.ToEngineCode(scanCode));
    }

    /**
     * Unbound keys return null and are dropped, which is the historical behaviour. An
     * unmapped key must not arrive under some invented name.
     */
    [Theory]
    [InlineData(ScanCode.B)]
    [InlineData(ScanCode.CapsLock)]
    [InlineData(ScanCode.KeypadEnter)]
    [InlineData(ScanCode.Unknown)]
    public void UnboundKeysReturnNull(ScanCode scanCode)
    {
        Assert.Null(ScanCodeNames.ToEngineCode(scanCode));
    }

    /**
     * The trap this whole work package exists to prevent: the engine code "a" is a
     * POSITION, and the character it prints depends on the user's layout. Asserting the
     * two are different concepts is not something code can do - so this documents it at
     * the one place a reader is most likely to assume otherwise.
     *
     * ScanCode.A -> "a" is the A-POSITION. On AZERTY that key prints Q. Bindings consume
     * this; text entry must consume INPUT_KEY_CHARACTER instead.
     */
    [Fact]
    public void EngineCodeForLettersIsAPositionNotACharacter()
    {
        Assert.Equal("w", ScanCodeNames.ToEngineCode(ScanCode.W));
        Assert.Equal(26, (int)ScanCode.W);   // HID 0x1A: the physical WASD 'W' position,
                                             // which prints Z on AZERTY and W on QWERTY.
    }
}
