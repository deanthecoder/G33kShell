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
using System.Diagnostics;

namespace G33kShell.Desktop.Console;

[DebuggerDisplay("ProgressBar:{X},{Y} {Width}x{Height} P:{Progress}")]
public class ProgressBar : Canvas
{
    private int m_progress;
    
    /// <summary>
    /// Progress value, in the range of 0-100.
    /// </summary>
    public int Progress
    {
        get => m_progress;
        set
        {
            if (m_progress == value)
                return;
            m_progress = value;
            InvalidateVisual();
        }
    }

    public ProgressBar(int width, int height) : base(width, height)
    {
    }

    public override void Render()
    {
        base.Render();

        var progressWidth = (int)(Width * Progress / 100.0);
        var bar = new string('█', progressWidth) + new string('░', Width - progressWidth);
        for (var y = 0; y < Height; y++)
            Screen.PrintAt(0, y, bar);
    }
}