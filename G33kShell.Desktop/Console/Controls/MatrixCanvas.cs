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
using System.Diagnostics;
using CSharp.Core.Extensions;

namespace G33kShell.Desktop.Console.Controls;

/// <summary>
/// A canvas to displaying an animated Matrix effect.
/// </summary>
[DebuggerDisplay("MatrixCanvas:{X},{Y} {Width}x{Height}")]
public class MatrixCanvas : AnimatedCanvas
{
    private readonly Random m_random = new Random();

    public MatrixCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 15)
    {
    }

    public override void OnLoaded(WindowManager windowManager)
    {
        base.OnLoaded(windowManager);

        using (Screen.Lock(out var screen))
        {
            // Seed screen with invisible characters.
            for (var y = 0; y < screen.Height; y++)
            {
                for (var x = 0; x < screen.Width; x++)
                {
                    var ch = (char)('\x3A' + m_random.Next(0, 33));
                    screen.PrintAt(x, y, new Attr(ch)
                    {
                        Foreground = Background
                    });
                }
            }

            // Add 'trails'.
            for (var x = 0; x < screen.Width; x += m_random.Next(2, 4))
            {
                var length = m_random.Next(6, 12);
                var y = m_random.Next(0, screen.Height - length - 1);
                var i = 0;
                for (; i < length - 1; i++)
                    screen.SetForeground(x, y + i, Foreground.SetBrightness(0.2 + 0.6 * i / length));
                screen.SetForeground(x, y + i, Foreground);
            }
        }
    }

    protected override void UpdateFrame(ScreenData screen)
    {
        // Screen the color values so the trails appear to move down the screen.
        for (var x = 0; x < screen.Width; x++)
        {
            var c = screen.Chars[screen.Height - 1][x].Foreground;
            for (var y = screen.Height - 2; y >= 0; y--)
                screen.Chars[y + 1][x].Foreground = screen.Chars[y][x].Foreground;
            screen.Chars[0][x].Foreground = c;
        }
    }
}