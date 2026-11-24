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
using DTC.Core;
using G33kShell.Desktop.Console.Controls;
using G33kShell.Desktop.Console.Events;
using G33kShell.Desktop.Console.Screensavers.GoldenAxe;
using G33kShell.Desktop.Console.Screensavers.Mario;
using G33kShell.Desktop.Skins;
using NUnit.Framework;

namespace G33kShell.Desktop.Console;

/// <summary>Checks scrolling and the pixel surface lifecycle.</summary>
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

    [TestCase("goldenaxe_play_color", true)]
    [TestCase("goldenaxe_color", true)]
    [TestCase("goldenaxe_play", false)]
    [TestCase("mario_color", true)]
    [TestCase("mario", false)]
    public void ReducedCrtGlowAppliesOnlyToColourGameplay(string activationName, bool reduced)
    {
        var manager = CreateManager();
        ScreensaverBase canvas = activationName.StartsWith("mario", System.StringComparison.Ordinal)
            ? new MarioWithTestBrain()
            : new GoldenAxeCanvas(80, 25);
        canvas.ActivationName = activationName;
        canvas.OnLoaded(manager);
        canvas.Stop();
        try
        {
            canvas.StartScreensaver(new ScreenData(80, 25));
            Assert.That(manager.PixelScreen, Is.Not.Null);
            Assert.That(manager.PixelScreen.ReduceCrtGlow, Is.EqualTo(reduced));
        }
        finally
        {
            canvas.StopScreensaver();
        }

        Assert.That(manager.PixelScreen, Is.Null);
        Assert.That(manager.SetPixelScreen(1, 1, [Rgb.Black]).ReduceCrtGlow, Is.False);
    }

    private static WindowManager CreateManager() => new WindowManager(80, 25, new RetroMonoDos());

    private sealed class MarioWithTestBrain : MarioCanvas
    {
        private readonly byte[] m_brain;

        public MarioWithTestBrain() : base(80, 25) => m_brain = CreateBrain().Save();

        protected override byte[] GetSavedBrainBytes() => m_brain;
    }
}
