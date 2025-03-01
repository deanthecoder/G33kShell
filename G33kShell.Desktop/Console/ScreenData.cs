// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
//  purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.
using System;
using System.Collections.Generic;
using CSharp.Core;

namespace G33kShell.Desktop.Console;

/// <summary>
/// Represents a block of character data intended for output to the screen.
/// </summary>
public class ScreenData
{
    public List<Attr[]> Chars { get; }
    public int Width => Chars[0].Length;
    public int Height
    {
        get => Chars.Count;
        set
        {
            if (Height == value)
                return;
            
            // Trim height down.
            while (Chars.Count > value)
                Chars.RemoveAt(Chars.Count - 1);
            
            // ...or expand it...
            while (Chars.Count < value)
            {
                var row = new Attr[Width];
                for (var x = 0; x < row.Length; x++)
                    row[x] = new Attr();
                Chars.Add(row);
            }
        }
    }

    public ScreenData(int width, int height)
    {
        if (width < 0)
            throw new ArgumentException("Width must not be less than zero", nameof(width));
        if (height < 0)
            throw new ArgumentException("Height must not be less than zero", nameof(height));

        Chars = new List<Attr[]>(height);
        for (var i = 0; i < height; i++)
        {
            var row = new Attr[width];
            for (var j = 0; j < row.Length; j++)
                row[j] = new Attr();
            Chars.Add(row);
        }
    }

    /// <summary>
    /// Reset the screen's character data.
    /// </summary>
    public void ClearChars(char? ch = null)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var attr = Chars[y][x];
                if (ch.HasValue)
                    attr.Set(ch.Value);
                else
                    attr.Clear();
            }
        }
    }

    /// <summary>
    /// Reset the screen's color data.
    /// </summary>
    public void ClearColor(Rgb foreground, Rgb background)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var attr = Chars[y][x];
                attr.Foreground = foreground;
                attr.Background = background;
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

    /// <summary>
    /// Set the x,y character, foreground, and background, all at once.
    /// </summary>
    public void PrintAt(int x, int y, Attr attr)
    {
        if (attr.Ch != '\0' && IsWithinBounds(x, y))
            Chars[y][x].Set(attr);
    }

    /// <summary>
    /// Set the x,y character (without changing colors).
    /// </summary>
    public void PrintAt(int x, int y, char ch)
    {
        if (ch != '\0' && IsWithinBounds(x, y))
            Chars[y][x].Set(ch);
    }

    
    public void PrintAt(int x, int y, string s)
    {
        if (y < 0 || y >= Height)
            return;
        if (x > Width || x + s.Length < 0)
            return;

        // Find the portion of the string that is within the bounds of the screen
        var maxLength = Math.Min(Width - x, s.Length);
        if (x < 0)
        {
            // Adjust the string and starting position if x is out of bounds to the left
            s = s[-x..];
            maxLength = Math.Min(s.Length, Width); // Adjust the max length
            x = 0;
        }

        // Print only the characters within the visible bounds
        for (var i = 0; i < maxLength; i++)
            Chars[y][x + i].Set(s[i]);
    }

    /// <summary>
    /// Set the background color at the specified x,y position.
    /// </summary>
    public void SetBackground(int x, int y, Rgb color)
    {
        if (IsWithinBounds(x, y))
            Chars[y][x].Background = color;
    }

    /// <summary>
    /// Set the background color at the specified x,y position.
    /// </summary>
    public void SetForeground(int x, int y, Rgb color)
    {
        if (IsWithinBounds(x, y))
            Chars[y][x].Foreground = color;
    }

    /// <summary>
    /// Set the background and foreground colors at the specified x,y position.
    /// </summary>
    public void SetColors(int x, int y, Rgb background, Rgb foreground)
    {
        if (!IsWithinBounds(x, y))
            return;
        Chars[y][x].Background = background;
        Chars[y][x].Foreground = foreground;
    }

    private bool IsWithinBounds(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height;
}