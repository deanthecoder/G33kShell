using System;
using Avalonia;
using DTC.Core;
using G33kShell.Desktop.Console;
using G33kShell.Desktop.Console.Views;
using G33kShell.Desktop.Skins;
using NUnit.Framework;

namespace G33kShell.Desktop.Tests.Console.Views;

[TestFixture]
public class ConsoleSelectionTests
{
    [Test]
    public void HighlightBlendsTowardCellForeground()
    {
        Assert.That(ConsoleView.BrightenSelectionBackground(new Rgb(10, 20, 30), new Rgb(110, 220, 80)),
            Is.EqualTo(new Rgb(30, 60, 40)));
        Assert.That(ConsoleView.BrightenSelectionBackground(new Rgb(200, 150, 100), new Rgb(100, 50, 0)),
            Is.EqualTo(new Rgb(180, 130, 80)));
    }

    [Test]
    public void ClearingAfterCopyRemovesSelection()
    {
        var view = CreateView();
        view.BeginSelection(new Point(1, 1));
        view.ExtendSelection(new Point(17, 1));
        view.EndSelection();
        var copiedText = view.GetSelectionText();
        view.ClearSelection();
        Assert.That(copiedText, Is.EqualTo("abc"));
        Assert.That(view.GetSelectionText(), Is.Null);
    }

    [TestCase(1, 12)]
    [TestCase(12, 1)]
    public void CopyPreservesLinesAndIndentationButTrimsPadding(int anchor, int end)
    {
        var chars = "abc    de  fghi ".ToCharArray();
        Assert.That(ConsoleView.ExtractSelectionText(chars, 5, anchor, end),
            Is.EqualTo($"bc{Environment.NewLine}  de{Environment.NewLine} fg"));
    }

    [Test]
    public void CopySingleCharacter()
    {
        Assert.That(ConsoleView.ExtractSelectionText("abc".ToCharArray(), 3, 1, 1), Is.EqualTo("b"));
    }

    [Test]
    public void CopyConvertsEmptyCellsToSpaces()
    {
        Assert.That(ConsoleView.ExtractSelectionText("a\0b\0".ToCharArray(), 4, 0, 3), Is.EqualTo("a b"));
    }

    [Test]
    public void FlatDisplayMapsScaledCoordinates()
    {
        var point = ConsoleView.MapDisplayPosition(new Point(400, 100), new Size(800, 400), new Size(400, 200), false);
        Assert.That(point, Is.EqualTo(new Point(200, 50)));
    }

    [Test]
    public void CurvedDisplayMatchesShaderSampling()
    {
        var point = ConsoleView.MapDisplayPosition(new Point(720, 300), new Size(800, 400), new Size(800, 400), true);
        // crt.sksl: 2.5 * (uv * 1.05) / sqrt(6.25 - dot(uv * 1.05, uv * 1.05)).
        Assert.That(point.X, Is.EqualTo(742.794508).Within(0.00001));
        Assert.That(point.Y, Is.EqualTo(307.123284).Within(0.00001));
    }

    [Test]
    public void ClickClearsSelectionAndDragClampsAtScreenEdge()
    {
        var view = CreateView();
        view.BeginSelection(new Point(9, 1));
        view.ExtendSelection(new Point(200, 200));
        view.EndSelection();
        Assert.That(view.GetSelectionText(), Is.EqualTo($"bcde{Environment.NewLine}fghij"));
        view.BeginSelection(new Point(9, 1));
        view.EndSelection();
        Assert.That(view.GetSelectionText(), Is.Null);
    }

    [Test]
    public void ChangedTextClearsSelectionBeforeCopy()
    {
        var view = CreateView();
        view.BeginSelection(new Point(1, 1));
        view.ExtendSelection(new Point(17, 1));
        view.EndSelection();
        Assert.That(view.GetSelectionText(), Is.EqualTo("abc"));
        using (view.WindowManager.Screen.Lock(out var screen))
            screen.PrintAt(0, 1, 'X');
        Assert.That(view.GetSelectionText(), Is.Null);
    }

    private static ConsoleView CreateView()
    {
        var manager = new WindowManager(5, 2, new RetroMonoDos());
        using (manager.Screen.Lock(out var screen))
        {
            screen.PrintAt(0, 0, "abcde");
            screen.PrintAt(0, 1, "fghij");
        }
        return new ConsoleView { WindowManager = manager };
    }
}
