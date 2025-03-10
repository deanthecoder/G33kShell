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
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using CSharp.Core;
using CSharp.Core.Extensions;
using JetBrains.Annotations;
using WenceyWang.FIGlet;

namespace G33kShell.Desktop.Console.Controls;

[DebuggerDisplay("ScrollerCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class ScrollerCanvas : ScreensaverBase
{
    private int m_fontHeight;
    private List<string> m_textColumns;

    public ScrollerCanvas(int width, int height) : base(width, height)
    {
        Name = "scroller";
    }

    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);

        const string message = "                            DTC forging pixels from pure ASCII since the ZX Spectrum.   Scanlines are my canvas.   CRT effects or bust.   Keep calm and press ALT+ENTER  -  Hack the planet!!!          ";

        var font = LoadFont();
        var alphabet = message.Distinct().ToDictionary(ch => ch, ch => LoadLetter(font, ch));
        m_fontHeight = alphabet.Values.Max(lines => lines.Length);

        m_textColumns = new List<string>();
        foreach (var ch in message)
        {
            var glyph = alphabet[ch];
            var advance = glyph.Max(o => o.Length);
            for (var i = 0; i < advance; i++)
            {
                var column = string.Empty;
                for (var y = 0; y < glyph.Length; y++)
                    column += glyph[y][i];
                
                m_textColumns.Add(column);
            }
        }
    }

    private static string[] LoadLetter(FIGletFont font, char ch)
    {
        var lineIndices = Enumerable.Range(0, font.Height);
        var lines = lineIndices.Select(n => font.GetCharacter(ch, n)).ToList();
        
        // Remove empty lines from the end.
        while (string.IsNullOrWhiteSpace(lines.Last()) && lines.Count > 1)
            lines.RemoveAt(lines.Count - 1);
        
        return lines.ToArray();
    }

    private static FIGletFont LoadFont()
    {
        // Enumerate all Avalonia embedded resources.
        var fontFolder = Assembly.GetExecutingAssembly().GetDirectory();
        var fontFile = fontFolder.GetFiles("Assets/Fonts/Figlet/*.flf").Single();

        // Load the font.
        using var fontStream = fontFile.OpenRead();
        return new FIGletFont(fontStream);
    }

    public override void UpdateFrame(ScreenData screen)
    {
        screen.Clear(Foreground, Background);
        var highResScreen = new HighResScreen(screen);

        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Width; x++)
            {
                var v = new Vector2(x - 0.5f * screen.Width, y - 0.5f * screen.Height);
                v = Vector2.Transform(v, Matrix3x2.CreateRotation((float)(Time * 0.2)));
                
                var boost = (Math.Sin(v.X * 0.3) - Math.Cos(v.Y * 0.3)) * 0.05;
                if (boost < 0.0)
                    boost = -boost * 2.0;
                screen.SetBackground(x, y, boost.Lerp(Background, Foreground));
            }
        }

        var scrollX = (int)(Time * 25.0);
        for (var x = 0; x < screen.Width; x++)
        {
            var columnIndex = (x + scrollX) % m_textColumns.Count;
            var column = m_textColumns[columnIndex];

            var y = (screen.Height - m_fontHeight) / 1.7 + Math.Sin(x * 0.04 + scrollX * 0.02) * 6.0;
            var row = 0;
            foreach (var ch in column)
            {
                if (ch == '█')
                {
                    highResScreen.Plot(x, (int)y + row, GetTextShade(row, (column.Length - 1) * 2));
                    highResScreen.Plot(x, (int)y + row + 1, GetTextShade(row + 1, (column.Length - 1) * 2));
                }
                
                row += 2;
            }

            DrawWave(screen, x, 0.0, 2.0, 0.3);
            DrawWave(screen, x, 6.0, 3.0, 0.7);
            DrawWave(screen, x, 18.0, 5.0, 1.0);
        }
    }

    private void DrawWave(ScreenData screen, double x, double phase, double amp, double brightness)
    {
        var ox = (int)x;
        x += phase;
        var y = screen.Height * 3.2 / 4.0 + GetWave(x, amp, phase);
        var grad = (GetWave(x + 0.01, amp, phase) - GetWave(x, amp, phase)) / 0.01;
        grad = (grad + 1.0) / 2.0; // -1, -0.5, 0, 0.5, 1.0
        screen.PrintAt(ox, (int)y, @"//--\\"[(int)grad.Clamp(0.0, 1.0).Lerp(0, 5.99)]);
        screen.SetForeground(ox, (int)y, brightness.Lerp(Background, Foreground));
    }

    private double GetWave(double x, double amp, double phase)
    {
        return Math.Sin(x * 0.15 + Time * 2.0) * amp * Math.Sin((Time + phase) * amp * 0.5);
    }

    private Rgb GetTextShade(int row, int maxRow)
    {
        var f = 2.0 * row / maxRow;
        f = f <= 1.0 ? f.Lerp(0.4, 1.0) : (f - 1.0).Lerp(0.2, 1.0);
        return f.Lerp(Background, Foreground);
    }
}
