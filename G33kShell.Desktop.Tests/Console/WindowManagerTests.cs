// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.
using Avalonia.Input;
using G33kShell.Desktop.Console.Events;
using G33kShell.Desktop.Skins;
using NUnit.Framework;

namespace G33kShell.Desktop.Console;

/// <summary>Checks scrolling through the terminal input queue.</summary>
/// <remarks>Keyboard and wheel input must use the same view offset.</remarks>
[TestFixture]
public class WindowManagerTests
{
    [TestCase(1, Key.Up)]
    [TestCase(-1, Key.Down)]
    public void WheelMatchesControlArrow(int delta, Key key)
    {
        var wheel = CreateManager();
        var keyboard = CreateManager();
        wheel.QueueEvent(new ScrollConsoleEvent(delta));
        keyboard.QueueEvent(new KeyConsoleEvent(key, KeyModifiers.Control, KeyConsoleEvent.KeyDirection.Down));
        wheel.ProcessEvents();
        keyboard.ProcessEvents();
        Assert.That(wheel.OffsetY, Is.EqualTo(delta));
        Assert.That(wheel.OffsetY, Is.EqualTo(keyboard.OffsetY));
    }

    [Test]
    public void FractionalWheelDeltasAccumulateAndReverse()
    {
        var manager = CreateManager();
        manager.QueueEvent(new ScrollConsoleEvent(0.5));
        manager.ProcessEvents();
        Assert.That(manager.OffsetY, Is.Zero);
        manager.QueueEvent(new ScrollConsoleEvent(0.75));
        manager.ProcessEvents();
        Assert.That(manager.OffsetY, Is.EqualTo(1));
        manager.QueueEvent(new ScrollConsoleEvent(-1.25));
        manager.ProcessEvents();
        Assert.That(manager.OffsetY, Is.Zero);
    }

    [Test]
    public void TypingResetsWheelOffsetAndFractionalRemainder()
    {
        var manager = CreateManager();
        manager.QueueEvent(new ScrollConsoleEvent(2.5));
        manager.QueueEvent(new KeyConsoleEvent(Key.A, KeyModifiers.None, KeyConsoleEvent.KeyDirection.Down));
        manager.QueueEvent(new ScrollConsoleEvent(0.5));
        manager.ProcessEvents();
        Assert.That(manager.OffsetY, Is.Zero);
    }

    [TestCase(3.5)]
    [TestCase(-3.5)]
    [TestCase(0.5)]
    public void ResetViewScrollClearsOffsetAndPartialMovement(double delta)
    {
        var manager = CreateManager();
        manager.QueueEvent(new ScrollConsoleEvent(delta));
        manager.ProcessEvents();
        manager.Render();

        manager.ResetViewScroll();

        Assert.That(manager.OffsetY, Is.Zero);
        if ((int)delta != 0)
            Assert.That(manager.Root.IsInvalidatedVisual, Is.True);
        manager.QueueEvent(new ScrollConsoleEvent(delta > 0 ? 0.5 : -0.5));
        manager.ProcessEvents();
        Assert.That(manager.OffsetY, Is.Zero);
    }

    private static WindowManager CreateManager() => new WindowManager(80, 25, new RetroMonoDos());
}
