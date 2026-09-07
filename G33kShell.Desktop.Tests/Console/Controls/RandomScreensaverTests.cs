// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.
using System;
using System.Collections.Generic;
using G33kShell.Desktop.Console.Screensavers;
using NUnit.Framework;

namespace G33kShell.Desktop.Console.Controls;

/// <summary>Checks random playback at cycle boundaries and timeout.</summary>
/// <remarks>Uses deterministic screensavers to exercise switching without animation threads.</remarks>
[TestFixture]
public class RandomScreensaverTests
{
    [SetUp]
    public void SetUp() => TestSaver.Instances.Clear();

    [Test]
    public void CompletedCycleSwitchesOnNextFrameAndReleasesPreviousSaver()
    {
        var screen = new ScreenData(20, 10);
        var random = CreateRandom(screen);
        var first = TestSaver.Instances[0];
        first.CompleteOnNextFrame = true;

        random.UpdateFrame(screen);
        Assert.That(TestSaver.Instances.Count, Is.EqualTo(1));
        random.UpdateFrame(screen);
        Assert.That(TestSaver.Instances.Count, Is.EqualTo(2));
        Assert.That(first.StopCount, Is.EqualTo(1));
        Assert.That(TestSaver.Instances[1].StartCount, Is.EqualTo(1));

        first.CompleteCycle();
        random.UpdateFrame(screen);
        Assert.That(TestSaver.Instances.Count, Is.EqualTo(2), "Old cycle events must not skip the new saver.");
        Assert.That(TestSaver.Instances[1].FrameNumber, Is.Zero);
    }

    [Test]
    public void ContinuousSaverSwitchesAfterFiveMinutes()
    {
        var screen = new ScreenData(20, 10);
        var random = CreateRandom(screen);
        for (var i = 0; i < 300 * random.TargetFps; i++)
            random.UpdateFrame(screen);
        Assert.That(TestSaver.Instances.Count, Is.EqualTo(1));
        random.UpdateFrame(screen);
        Assert.That(TestSaver.Instances.Count, Is.EqualTo(2));
    }

    [Test]
    public void RestartClearsPendingCompletionAndRestartsTimeout()
    {
        var screen = new ScreenData(20, 10);
        var random = CreateRandom(screen);
        random.UpdateFrame(screen);
        TestSaver.Instances[0].CompleteCycle();
        random.StopScreensaver();
        random.StartScreensaver(screen);
        random.UpdateFrame(screen);
        Assert.That(TestSaver.Instances.Count, Is.EqualTo(1));
        Assert.That(TestSaver.Instances[0].FrameNumber, Is.Zero);
    }

    [Test]
    public void EmptySelectionDoesNotAttemptToSwitch()
    {
        var random = new RandomScreensaver(80, 25, Array.Empty<Type>());
        var screen = new ScreenData(80, 25);
        random.BuildScreen(screen);
        random.UpdateFrame(screen);
        Assert.That(random.IsReadyToRun, Is.False);
    }

    [Test]
    public void HeistReportsReassemblyAndKeepsLoopingStandalone()
    {
        var screen = new ScreenData(20, 10);
        screen.PrintAt(5, 5, "hello");
        var heist = new HeistCanvas(20, 10);
        var cycles = 0;
        heist.CycleCompleted += (_, _) => cycles++;
        heist.StartScreensaver(screen);

        for (var i = 0; i < 10000 && cycles < 2; i++)
            heist.UpdateFrame(screen);

        Assert.That(cycles, Is.EqualTo(2));
    }

    private static RandomScreensaver CreateRandom(ScreenData screen)
    {
        var random = new RandomScreensaver(screen.Width, screen.Height, new[] { typeof(TestSaver) });
        random.BuildScreen(screen);
        random.StartScreensaver(screen);
        return random;
    }

    public sealed class TestSaver : ScreensaverBase
    {
        public static readonly List<TestSaver> Instances = new List<TestSaver>();
        public bool CompleteOnNextFrame { get; set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }

        public TestSaver(int width, int height) : base(width, height, 2)
        {
            Name = "test";
            Instances.Add(this);
        }

        public void CompleteCycle() => OnCycleCompleted();
        public override void StartScreensaver(ScreenData shellScreen) => StartCount++;
        public override void StopScreensaver() => StopCount++;
        public override void UpdateFrame(ScreenData screen)
        {
            if (!CompleteOnNextFrame)
                return;
            CompleteOnNextFrame = false;
            CompleteCycle();
        }
    }
}
