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
using System.Text;

namespace G33kShell.Desktop.Console;

[DebuggerDisplay("Fire:{X},{Y} {Width}x{Height}")]
public class Fire : AnimatedCanvas
{
    private readonly string m_fireChars = " ,;+ltgti!lI?/\\|)(1}{][rcvzjftJUOQocxfXhqwWB8&%$#@";
    private readonly int m_maxCharIndex;
    private readonly int[] m_firePixelsArray;
    private readonly Random m_random = new Random();

    public Fire(int width, int height, int targetFps = 30) : base(width, height, targetFps)
    {
        m_maxCharIndex = m_fireChars.Length;
        m_firePixelsArray = new int[width * (height + 1)];
    }

    /// <summary>
    /// Algorithm based on https://github.com/SnippetsDevelop/snippetsdevelop.github.io/blob/master/codes/FireChars.html
    /// </summary>
    protected override void UpdateFrame(ScreenData screen)
    {
        // Create fire at the bottom row
        for (var i = 0; i < Width; i++)
        {
            var randomCol = m_random.Next(Width);
            var index = randomCol + Width * Height;
            m_firePixelsArray[index] = m_random.Next(m_maxCharIndex);
        }

        // Reset some pixels to create the fading effect
        for (var i = 0; i < Width; i++)
        {
            var randomCol = m_random.Next(Width);
            var index = randomCol + Width * Height;
            m_firePixelsArray[index] = 0;
        }

        // Propagate fire upwards.
        for (var i = 0; i < Width * Height - 1; i++)
        {
            var averageValue = (m_firePixelsArray[i] +
                                m_firePixelsArray[i + 1] +
                                m_firePixelsArray[i + Width] +
                                m_firePixelsArray[i + Width + 1]) / 4.0;
            m_firePixelsArray[i] = (int)averageValue;
        }

        // Update the Screen data.
        var fireString = new StringBuilder();
        for (var y = 0; y < Height; y++)
        {
            fireString.Clear();
            for (var x = 0; x < Width; x++)
                fireString.Append(m_fireChars[m_firePixelsArray[y * Width + x]]);
            screen.PrintAt(0, y, fireString.ToString());
        }
    }
}