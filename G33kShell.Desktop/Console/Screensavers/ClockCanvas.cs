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
using System.Linq;
using System.Reflection;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;
using WenceyWang.FIGlet;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// Digital clock that bounces around the screen.
/// </summary>
[DebuggerDisplay("ClockCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class ClockCanvas : ScreensaverBase
{
    private readonly FIGletFont m_font;
    private int m_posX;
    private int m_posY;
    private int m_dX = 1;
    private int m_dY = 1;

    public ClockCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 5)
    {
        Name = "clock";

        m_font = LoadFont();
    }

    public override void UpdateFrame(ScreenData screen)
    {
        screen.Clear(Foreground, Background);
        var highResScreen = new HighResScreen(screen);
        
        var now = DateTime.Now.ToString("HH:mm");
        
        // Calculate width and height of the text.
        var textWidth = 0;
        var textHeight = 0;
        foreach (var ch in now)
        {
            var charLines = Enumerable.Range(0, m_font.Height)
                .Select(y => m_font.GetCharacter(ch, y))
                .ToArray();
            if (charLines.Length > 0)
                textWidth += charLines[0].Length;
            
            var blankLines = charLines
                .Reverse()
                .TakeWhile(string.IsNullOrWhiteSpace)
                .Count();
            textHeight = Math.Max(textHeight, m_font.Height - blankLines);
        }

        // Update position.
        m_posX += m_dX;
        m_posY += m_dY;

        // Bounce off walls.
        if (m_posX <= 0 || m_posX + textWidth >= highResScreen.Width)
        {
            m_dX = -m_dX;
            m_posX = Math.Max(0, Math.Min(highResScreen.Width - textWidth, m_posX));
        }
        
        if (m_posY <= 0 || m_posY + textHeight >= highResScreen.Height)
        {
            m_dY = -m_dY;
            m_posY = Math.Max(0, Math.Min(highResScreen.Height - textHeight, m_posY));
        }

        // Draw the time at the bouncing position
        for (var y = 0; y < m_font.Height; y++)
        {
            var offset = 0;
            foreach (var textRow in now.Select(ch => m_font.GetCharacter(ch, y)))
            {
                for (var x = 0; x < textRow.Length; x++)
                {
                    var ch = textRow[x];
                    if (ch == ' ')
                        continue; // Nothing to draw.

                    var screenX = m_posX + x + offset;
                    var screenY = m_posY + y;
                    highResScreen.Plot(screenX, screenY, ch == '█' ? Foreground : 0.25.Lerp(Background, Foreground));
                }
                
                offset += textRow.Length;
            }
        }
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
}