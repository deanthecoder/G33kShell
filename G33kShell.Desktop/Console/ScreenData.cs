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
using Avalonia.Media;

namespace G33kShell.Desktop.Console;

public class ScreenData
{
    public Attr[][] Chars { get; }
    public int Width => Chars[0].Length;
    public int Height => Chars.Length;
    public IBrush Foreground { get; set; }
    public IBrush Background { get; set; }

    public ScreenData(int width, int height)
    {
        Chars = new Attr[height][];
        for (var y = 0; y < height; y++)
        {
            Chars[y] = new Attr[width];
            for (var x = 0; x < width; x++)
                Chars[y][x] = new Attr();
        }
    }
    
    public void Clear(char? ch = null)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var attr = Chars[y][x];
                attr.Foreground = Foreground;
                attr.Background = Background;
                if (ch.HasValue)
                    attr.Set(ch.Value);
                else
                    attr.Clear();
            }
        }
    }

    public void CopyTo(ScreenData target, int xOffset, int yOffset)
    {
        if (target == this)
            return;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
                target.PrintAt(x + xOffset, y + yOffset, Chars[y][x]);
        }
    }

    public void PrintAt(int x, int y, Attr attr)
    {
        if (attr.Ch != '\0' && IsWithinBounds(x, y))
            Chars[y][x].Set(attr);
    }

    public void PrintAt(int x, int y, char ch)
    {
        if (ch != '\0' && IsWithinBounds(x, y))
            Chars[y][x].Set(ch);
    }

    public void PrintAt(int x, int y, string s)
    {
        if (y < 0 || y >= Height)
            return;

        // Find the portion of the string that is within the bounds of the screen
        var maxLength = Math.Min(Width - x, s.Length);
        if (x < 0)
        {
            // Adjust the string and starting position if x is out of bounds to the left
            s = s.Substring(-x);
            maxLength = Math.Min(s.Length, Width); // Adjust the max length
            x = 0;
        }

        // Print only the characters within the visible bounds
        for (var i = 0; i < maxLength; i++)
            Chars[y][x + i].Set(s[i]);
    }

    private bool IsWithinBounds(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height;
}