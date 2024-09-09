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

namespace G33kShell.Desktop.Console;

[DebuggerDisplay("{Left},{Top},{Right},{Bottom}")]
public class BorderThickness
{
    public int Left { get; }
    public int Top { get; }
    public int Right { get; }
    public int Bottom { get; }
    public int LeftRight => Left + Right;
    public int TopBottom => Top + Bottom;

    public static BorderThickness Zero { get; } = new BorderThickness(0);

    public BorderThickness(int thickness) : this(thickness, thickness, thickness, thickness)
    {
    }
    
    public BorderThickness(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
}