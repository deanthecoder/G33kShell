// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// for any purpose. The software is provided as is, without warranty of any kind.

using System;
using System.Collections.Generic;
using G33kShell.Desktop.Console.Screensavers.GoldenAxe;
using NUnit.Framework;

namespace G33kShell.Desktop.Tests;

public class GoldenAxeSceneryCoverageTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void BottomRowsHaveSceneryThroughoutTheCameraRange(int area)
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory, area);
        var finalX = 4089 - data.Stops[^1];
        for (var x = data.InitialCameraX; x >= finalX; x--)
        {
            var range = data.CameraRange(x);
            for (var y = range.Low; y <= range.High; y++)
                CheckView(data, x, y);
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void AutoplayKeepsTheBottomRowsCovered(int area)
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory, area);
        for (var character = 0; character < HeroDefinition.All.Length; character++)
        {
            var game = new Game(data, character, new Random(0), playOpening: true);
            var controller = new CodedController();
            var checkedViews = new HashSet<(int X, int Y)>();
            // Check the eased view itself: it can pass through positions outside the camera's target range.
            for (var tick = 0; ; tick++)
            {
                var view = (X: (int)game.ViewCamera.X, Y: (int)game.ViewCamera.Y);
                if (checkedViews.Add(view))
                    CheckView(data, view.X, view.Y);
                if (tick == 60 * 300 || game.Complete || game.GameOver)
                    break;
                game.Tick(controller.Read(game));
            }
            TestContext.WriteLine($"Stage {data.StageNumber}, {game.Character.Name}: {checkedViews.Count} views, progress {game.Progress:F0}, cleared = {game.Complete}.");
        }
    }

    private static void CheckView(ArcadeData data, int cameraX, int cameraY)
    {
        var u = -7 - cameraX;
        var v = -512 - cameraY;
        var backgroundX = (int)Math.Floor(u * data.Parallax[0][0] + data.Parallax[0][1]);
        var backgroundY = (int)(v * data.Parallax[1][0] + data.Parallax[1][1]);
        if (data.FindBottomSceneryGap(u, v, backgroundX, backgroundY) is { } gap)
            Assert.Fail($"Stage {data.StageNumber}: possible scenery gap at world ({gap.X}, {gap.Y}), camera ({cameraX}, {cameraY}).");
    }
}
