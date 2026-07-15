using System;
using System.IO;
using NUnit.Framework;
using SkiaSharp;

namespace G33kShell.Desktop.Console.Views;

[TestFixture]
public class ConsoleGlyphRasterizerTests
{
    [Test]
    public void SolidBlock_HasCoverageOnEveryCellRow()
    {
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "VGA", "PxPlus_IBM_VGA8.ttf");
        using var typeface = SKTypeface.FromFile(fontPath);
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            Typeface = typeface,
            TextSize = 16,
            IsAntialias = true,
            LcdRenderText = false,
            SubpixelText = false
        };

        var coverage = ConsoleGlyphRasterizer.Rasterize(paint, '█');

        for (var y = 0; y < 16; y++)
            Assert.That(coverage.AsSpan(y * 8, 8).IndexOfAnyExcept((byte)0), Is.GreaterThanOrEqualTo(0), $"Row {y} is blank.");
    }
}
