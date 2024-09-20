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

using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace G33kShell.Desktop.Console;

[DebuggerDisplay("TextBlock:{X},{Y} {Width}x{Height} T:{Text}")]
public class TextBlock : Canvas
{
    private bool m_isFlashing;
    private Task m_flasher;
    private bool m_flashState = true;
    
    public string[] Text { get; private init; }

    public bool IsFlashing
    {
        get => m_isFlashing;
        set
        {
            if (m_isFlashing == value)
                return;
            m_isFlashing = value;

            if (m_isFlashing)
            {
                m_flasher ??= Task.Run(() =>
                {
                    while (IsFlashing)
                    {
                        m_flashState = true;
                        Thread.Sleep(800);
                        InvalidateVisual();
                        m_flashState = false;
                        Thread.Sleep(400);
                        InvalidateVisual();
                    }
                });
            }
        }
    }

    public TextBlock(params string[] lines) : base(lines.Max(o => o.Length), lines.Length)
    {
        Text = lines;
    }

    public override void Render()
    {
        base.Render();

        var show = !IsFlashing || m_flashState;
        if (!show)
        {
            Screen.ClearChars();
            return;
        }
        
        for (var i = 0; i < Text.Length; i++)
            Screen.PrintAt(0, i, Text[i]);
    }

    protected override void OnUnloaded()
    {
        IsFlashing = false;
        base.OnUnloaded();
    }
}