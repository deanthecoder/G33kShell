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

namespace G33kShell.Desktop.Console;

[DebuggerDisplay("Border:{X},{Y} {Width}x{Height}")]
public class Border : Canvas
{
    private LineStyle m_style;

    public enum LineStyle
    {
        Single,
        Double,
        DoubleHorizontalSingleVertical,
        SingleHorizontalDoubleVertical,
        Block
    }

    private static readonly Dictionary<LineStyle, string> LineCharacters = new Dictionary<LineStyle, string>
    {
        {
            LineStyle.Single, "┌┐└┘──││"
        },
        {
            LineStyle.Double, "╔╗╚╝══║║"
        },
        {
            LineStyle.DoubleHorizontalSingleVertical, "╒╕╘╛══││"
        },
        {
            LineStyle.SingleHorizontalDoubleVertical, "╓╖╙╜──║║"
        },
        {
            LineStyle.Block, "▄▄▀▀▄▀██"
        }
    };

    public Border Init(int width, int height, LineStyle style)
    {
        m_style = style;
        base.Init(width, height);
        Padding = new BorderThickness(1);
        return this;
    }

    public override void Render()
    {
        base.Render();

        var boxChars = LineCharacters[m_style];
        var topLeft = boxChars[0];
        var topRight = boxChars[1];
        var bottomLeft = boxChars[2];
        var bottomRight = boxChars[3];
        var hTop = boxChars[4];
        var hBottom = boxChars[5];
        var vLeft = boxChars[6];
        var vRight = boxChars[7];

        // Top border
        Screen.PrintAt(0, 0, topLeft);
        Screen.PrintAt(1, 0, new string(hTop, Width - 2));
        Screen.PrintAt(Width - 1, 0, topRight);

        // Bottom border
        Screen.PrintAt(0, Height - 1, bottomLeft);
        Screen.PrintAt(1, Height - 1, new string(hBottom, Width - 2));
        Screen.PrintAt(Width - 1, Height - 1, bottomRight);

        // Vertical borders
        for (var i = 1; i < Height - 1; i++)
        {
            Screen.PrintAt(0, i, vLeft);
            Screen.PrintAt(Width - 1, i, vRight);
        }
    }
}