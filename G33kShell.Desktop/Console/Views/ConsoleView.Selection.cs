// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
// purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Text;
using Avalonia;
using DTC.Core;

namespace G33kShell.Desktop.Console.Views;

public partial class ConsoleView
{
    private char[] m_selectionChars;
    private int m_selectionWidth;
    private int m_selectionOffset;
    private int m_selectionAnchor;
    private int m_selectionEnd;
    private Point m_selectionOrigin;
    private bool m_selecting;
    private bool m_hasSelection;

    // Match crt.sksl's display-to-source sampling, including the curved surround.
    internal static Point MapDisplayPosition(Point point, Size display, Size source, bool surround)
    {
        var x = point.X / display.Width - 0.5;
        var y = point.Y / display.Height - 0.5;
        if (surround)
        {
            x *= 1.05;
            y *= 1.05;
            var scale = 2.5 / Math.Sqrt(Math.Max(0.001, 6.25 - x * x - y * y));
            x *= scale;
            y *= scale;
        }
        return new Point((x + 0.5) * source.Width, (y + 0.5) * source.Height);
    }

    internal bool BeginSelection(Point point)
    {
        ClearSelection();
        if (m_windowManager == null)
            return false;
        using var screenLock = m_windowManager.Screen.Lock(out var screen);
        if (!new Rect(Padding.Left, Padding.Top, screen.Width * CharWidth, screen.Height * CharHeight).Contains(point))
            return false;

        m_selectionWidth = screen.Width;
        m_selectionOffset = m_windowManager.OffsetY;
        m_selectionChars = new char[screen.Width * screen.Height];
        for (var y = 0; y < screen.Height; y++)
        for (var x = 0; x < screen.Width; x++)
            m_selectionChars[y * screen.Width + x] = screen.Chars[y][x].Ch;
        m_selectionOrigin = point;
        m_selectionAnchor = m_selectionEnd = GetSelectionCell(point, screen);
        m_selecting = true;
        return true;
    }

    internal void ExtendSelection(Point point)
    {
        if (!m_selecting)
            return;
        using var screenLock = m_windowManager.Screen.Lock(out var screen);
        ValidateSelection(screen);
        if (!m_selecting)
            return;
        var delta = point - m_selectionOrigin;
        if (!m_hasSelection && delta.X * delta.X + delta.Y * delta.Y < 9)
            return;
        m_hasSelection = true;
        m_selectionEnd = GetSelectionCell(point, screen);
        InvalidateVisual();
    }

    internal void EndSelection() => m_selecting = false;

    private int GetSelectionCell(Point point, ScreenData screen)
    {
        var x = (int)Math.Clamp(Math.Floor((point.X - Padding.Left) / CharWidth), 0, screen.Width - 1);
        var y = (int)Math.Clamp(Math.Floor((point.Y - Padding.Top) / CharHeight), 0, screen.Height - 1);
        return y * screen.Width + x;
    }

    private bool IsSelected(int index) => m_hasSelection &&
        index >= Math.Min(m_selectionAnchor, m_selectionEnd) &&
        index <= Math.Max(m_selectionAnchor, m_selectionEnd);

    internal static Rgb BrightenSelectionBackground(Rgb background, Rgb foreground) => new(
        (byte)(background.R + (foreground.R - background.R) / 5),
        (byte)(background.G + (foreground.G - background.G) / 5),
        (byte)(background.B + (foreground.B - background.B) / 5));

    internal void ClearSelection(bool invalidate = true)
    {
        m_selectionChars = null;
        m_hasSelection = m_selecting = false;
        if (invalidate)
            InvalidateVisual();
    }

    private void ValidateSelection(ScreenData screen, bool invalidate = true)
    {
        if (m_selectionChars == null)
            return;
        if (m_selectionWidth != screen.Width || m_selectionChars.Length != screen.Width * screen.Height ||
            m_selectionOffset != m_windowManager.OffsetY)
        {
            ClearSelection(invalidate);
            return;
        }
        for (var y = 0; y < screen.Height; y++)
        for (var x = 0; x < screen.Width; x++)
        {
            if (m_selectionChars[y * screen.Width + x] == screen.Chars[y][x].Ch)
                continue;
            ClearSelection(invalidate);
            return;
        }
    }

    internal string GetSelectionText()
    {
        if (!m_hasSelection)
            return null;
        using var screenLock = m_windowManager.Screen.Lock(out var screen);
        ValidateSelection(screen);
        return m_hasSelection
            ? ExtractSelectionText(m_selectionChars, m_selectionWidth, m_selectionAnchor, m_selectionEnd)
            : null;
    }

    internal static string ExtractSelectionText(char[] chars, int width, int anchor, int end)
    {
        var start = Math.Min(anchor, end);
        var finish = Math.Max(anchor, end);
        var text = new StringBuilder();
        for (var row = start / width; row <= finish / width; row++)
        {
            if (row > start / width)
                text.AppendLine();
            var first = Math.Max(start, row * width);
            var last = Math.Min(finish, (row + 1) * width - 1);
            text.Append(new string(chars, first, last - first + 1).Replace('\0', ' ').TrimEnd(' '));
        }
        return text.ToString();
    }
}
