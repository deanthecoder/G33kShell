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
using G33kShell.Desktop.Console.Extensions;

namespace G33kShell.Desktop.Console.Controls;

[DebuggerDisplay("Border:{X},{Y} {Width}x{Height}")]
public class Border : Visual
{
    private readonly LineStyle m_style;

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

    public Border(int width, int height, LineStyle style) : base(width, height)
    {
        m_style = style;
        Padding = new BorderThickness(1);
    }

    public override void Render(ScreenData screen) =>
        screen.DrawBox(X, Y, X + Width - 1, Y + Height - 1, LineCharacters[m_style]);
}