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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace G33kShell.Desktop.Console.Controls;

/// <summary>
/// Represents a non-editable text block control.
/// </summary>
[DebuggerDisplay("TextBlock:{X},{Y} {Width}x{Height} T:{Text}")]
public class TextBlock : Visual
{
    private readonly Dictionary<int, int> m_stringLengths = new Dictionary<int, int>();
    private int m_previousLineCount;
    private bool m_isFlashing;
    private Task m_flasher;
    private bool m_flashState = true;

    public virtual string[] Text { get; private init; } = new[] { string.Empty };

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

    protected TextBlock(int width, int height) : base(width, height)
    {
    }
    
    public TextBlock(params string[] lines) : this(lines.Max(o => o.Length), lines.Length)
    {
        Text = lines;
    }

    public override void Render(ScreenData screen)
    {
        var show = !IsFlashing || m_flashState;
        if (!show)
        {
            screen.ClearChars();
            return;
        }

        var text = Text;
        for (var i = 0; i < text.Length; i++)
        {
            // Print the text at the specified position on the screen.
            screen.PrintAt(0, i, text[i]);
            
            // Retrieve the length of the previously printed string at this line.
            m_stringLengths.TryGetValue(i, out var previousLength);
            
            // Pad the string if it is shorter than the previous one (to allow for string shrinkage).
            var charsToPad = previousLength - text[i].Length;
            if (charsToPad > 0)
                screen.PrintAt(text[i].Length, i, new string(' ', charsToPad));

            // Update the dictionary with the current string length.
            m_stringLengths[i] = text[i].Length;
        }

        for (var i = text.Length; i <= m_previousLineCount; i++)
            screen.PrintAt(0, i, new string(' ', Width));

        m_previousLineCount = text.Length;
    }

    protected override void OnUnloaded()
    {
        IsFlashing = false;
        base.OnUnloaded();
    }
}