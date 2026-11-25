// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
// purpose.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core;

namespace G33kShell.Desktop.Console;

/// <summary>
/// An allocation-free intermediate color for per-pixel composition and saved sprite backgrounds.
/// </summary>
internal readonly record struct PixelColor(byte R, byte G, byte B)
{
    public int Bgra => B | G << 8 | R << 16 | unchecked((int)0xff000000);

    public static implicit operator PixelColor(Rgb color) => new(color.R, color.G, color.B);
}
