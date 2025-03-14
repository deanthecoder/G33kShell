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
using System;
using System.Numerics;
using CSharp.Core;
using CSharp.Core.Extensions;

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
    
    public void Plot(int x, int y, Rgb color)
    {
        if (x < 0 || x >= m_screen.Width || y < 0 || y >= m_screen.Height * 2)
            return; // Off screen.
            
        var isTopPixel = (y & 1) == 0;
        y >>= 1;
    
        var attr = m_screen.Chars[y][x];
        if (attr.Ch != '▀')
            m_screen.PrintAt(x, y, new Attr('▀', attr.Background, attr.Background));
        
        if (isTopPixel)
            m_screen.SetForeground(x, y, color);
        else
            m_screen.SetBackground(x, y, color);
    }

    
    /// <summary>
    /// Draws a filled ellipse (circle) on the screen using the specified shading function to determine pixel colors.
    /// </summary>
    /// <param name="cx">The x-coordinate of the circle's center.</param>
    /// <param name="cy">The y-coordinate of the circle's center.</param>
    /// <param name="xRadius">The horizontal radius of the circle.</param>
    /// <param name="yRadius">The vertical radius of the circle.</param>
    /// <param name="colorFunc">
    /// A function that takes normalized x and y values (in the range [-1,1]) and returns the color for each pixel.
    /// </param>
    public void DrawFilledCircle(int cx, int cy, int xRadius, int yRadius, Func<double, double, Rgb> colorFunc)
    {
        var xRadius2 = 1.0 / (xRadius * xRadius);
        var yRadius2 = 1.0 / (yRadius * yRadius);
    
        for (var y = -yRadius; y <= yRadius; y++)
        {
            var yNorm = (double)y / yRadius; // Normalize Y to range [-1,1]
            var yLookup = y * y * yRadius2;
    
            for (var x = -xRadius; x <= xRadius; x++)
            {
                var xNorm = (double)x / xRadius; // Normalize X to range [-1,1]
                if (x * x * xRadius2 + yLookup <= 1.0)
                {
                    var color = colorFunc(xNorm, yNorm); // Get color from shading function
                    Plot(cx + x, cy + y, color);
                }
            }
        }
    }

    /// <summary>
    /// Draws a flat-shaded ellipse on the screen.
    /// </summary>
    /// <param name="cx">The x-coordinate of the circle's center.</param>
    /// <param name="cy">The y-coordinate of the circle's center.</param>
    /// <param name="xRadius">The horizontal radius of the circle.</param>
    /// <param name="yRadius">The vertical radius of the circle.</param>
    /// <param name="color">The color to use for filling the circle.</param>
    public void DrawShadedCircle(int cx, int cy, int xRadius, int yRadius, Rgb color) =>
        DrawFilledCircle(cx, cy, xRadius, yRadius, (_, _) => color);

    public void DrawSphere(int cx, int cy, int radius, Rgb foreground, Rgb background, double lightX = -0.5, double lightY = -0.5)
    {
        DrawFilledCircle(cx, cy, radius, radius, SphereShading);
        return;
        
        Rgb SphereShading(double x, double y)
        {
            // Sphere equation: x^2 + y^2 + z^2 = 1
            var zSquared = 1.0 - (x * x + y * y);
            if (zSquared < 0) return background; // Outside the sphere

            var z = Math.Sqrt(zSquared); // Compute z coordinate

            // Light direction (normalized) from top-left.
            var lightDir = Vector3.Normalize(new Vector3((float)lightX, (float)lightY, 1.0f));

            // Surface normal at this point.
            var normal = new Vector3((float)x, (float)y, (float)z);

            // Lambertian reflection: dot(normal, lightDir)
            var brightness = Math.Max(0, Vector3.Dot(normal, lightDir)); // No negative light

            // Convert brightness to color.
            brightness = 0.05f + 0.95f * brightness;
            return ((double)brightness).Lerp(background, foreground);
        }
    }
}