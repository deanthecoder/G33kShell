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
using CSharp.Core;

namespace G33kShell.Desktop.Console;

/// <summary>
/// Methods to treat a <see cref="ScreenData"/> as a 'high res' screen (with double height resolution).
/// </summary>
public class HighResScreen
{
    private readonly ScreenData m_screen;

    public HighResScreen(ScreenData screen)
    {
        m_screen = screen;
    }
    
    public void Plot(int x, int y, Rgb foreground)
    {
        if (x < 0 || x >= m_screen.Width || y < 0 || y >= m_screen.Height * 2)
            return; // Off screen.
            
        var isTopPixel = (y & 1) == 0;
        y >>= 1;

        var attr = m_screen.Chars[y][x];
        if (attr.Ch != '▀')
            m_screen.PrintAt(x, y, new Attr('▀', attr.Background, attr.Background));
        
        if (isTopPixel)
            m_screen.SetForeground(x, y, foreground);
        else
            m_screen.SetBackground(x, y, foreground);
    }
}