// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// If you modify the code, please retain this copyright header.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.
using Avalonia.Input;
using G33kShell.Desktop.Console.Screensavers.GoldenAxe;
using NUnit.Framework;

namespace G33kShell.Desktop.Tests;

/// <summary>
/// Checks held movement and button taps crossing UI and simulation ticks.
/// </summary>
/// <remarks>
/// No arcade assets or running Avalonia window are required.
/// </remarks>
[TestFixture]
public class GoldenAxeInputTests
{
    [Test]
    public void HeldMovementAndAttackPersistUntilReleasedAndFocusLossClearsInput()
    {
        var keyboard = new KeyboardController();
        keyboard.SetKey(Key.Left, true);
        keyboard.SetKey(Key.Up, true);
        keyboard.SetKey(Key.LeftCtrl, true);
        Assert.That(keyboard.Read(), Is.EqualTo(new GameInput(-1, -1, Attack: true)));
        Assert.That(keyboard.Read(), Is.EqualTo(new GameInput(-1, -1, Attack: true)));
        keyboard.SetKey(Key.LeftCtrl, false);
        Assert.That(keyboard.Read().Attack, Is.False);
        keyboard.Clear();
        Assert.That(keyboard.Read(), Is.EqualTo(default(GameInput)));
    }

    [Test]
    public void ShortButtonTapsSurviveUntilReadAndAutoRepeatDoesNotRetriggerJump()
    {
        var keyboard = new KeyboardController();
        keyboard.SetKey(Key.Tab, true);
        keyboard.SetKey(Key.Tab, false);
        keyboard.SetKey(Key.Space, true);
        Assert.That(keyboard.Read(), Is.EqualTo(new GameInput(Jump: true, Magic: true)));
        keyboard.SetKey(Key.Space, true);
        Assert.That(keyboard.Read(), Is.EqualTo(default(GameInput)));
        keyboard.SetKey(Key.Space, false);
        keyboard.SetKey(Key.Space, true);
        Assert.That(keyboard.Read().Jump, Is.True);
        keyboard.Clear();
        Assert.That(keyboard.Read(), Is.EqualTo(default(GameInput)));
    }

    [Test]
    public void ShiftSpaceRequestsDismountInsteadOfJump()
    {
        var keyboard = new KeyboardController();
        keyboard.SetKey(Key.LeftShift, true);
        keyboard.SetKey(Key.Space, true);
        keyboard.SetKey(Key.Space, false);
        keyboard.SetKey(Key.LeftShift, false);
        Assert.That(keyboard.Read(), Is.EqualTo(new GameInput(Dismount: true)));
        Assert.That(keyboard.Read(), Is.EqualTo(default(GameInput)));
    }
}
