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
using System.IO;
using System.Linq;
using SkiaSharp;

namespace G33kShell.Desktop.Console;

[DebuggerDisplay("Image:{X},{Y} {Width}x{Height}")]
public class Image : Visual
{
    private double[] m_lums;

    public Image Init(int width, int height, FileInfo imageFile)
    {
        base.Init(width, height);

        m_lums = new double[width * height];
        using (var bitmap = SKBitmap.Decode(imageFile.FullName))
        {
            var blockWidth = (double)bitmap.Width / Width;
            var blockHeight = (double)bitmap.Height / Height;

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    bitmap.GetPixel((int)(x * blockWidth), (int)(y * blockHeight)).ToHsl(out _, out _, out var lum);
                    var f = lum / 100.0;
                    m_lums[y * width + x] = f;
                }
            }
        }

        var mn = m_lums.Min();
        var mx = m_lums.Max();
        m_lums = m_lums.Select(o => (o - mn) / mx).ToArray();

        return this;
    }

    public override void Render()
    {
        var chars = new[]
        {
            ' ', '.', ':', '-', '=', '+', '*', '#', '%', '@'
        };
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var lum = m_lums[y * Width + x];
                var ch = chars[(int)(lum * (chars.Length - 1))];
                Screen.PrintAt(x, y, ch);
            }
        }

        base.Render();
    }
}