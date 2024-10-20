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
using System.Diagnostics;

namespace G33kShell.Desktop.Console.Controls;

/// <summary>
/// A canvas to displaying an animated donut.
/// </summary>
/// <remarks>
/// The DonutCanvas class extends the AnimatedCanvas class.
/// </remarks>
[DebuggerDisplay("DonutCanvas:{X},{Y} {Width}x{Height}")]
public class DonutCanvas : AnimatedCanvas, IScreensaver
{
    private const double ThetaSpacing = 0.05f;
    private const double PhiSpacing = 0.02f;
    private const double R1 = 0.7;
    private const double R2 = 2;
    private const double K2 = 5;

    private readonly double m_k1;
    private readonly double[,] m_zBuffer;

    public DonutCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
    {
        Name = "donut";
        
        m_k1 = screenWidth * K2 * 2.0 / (8.0 * (R1 + R2));
        m_zBuffer = new double[screenWidth, screenHeight];
    }
    
    /// <summary>
    /// Modified from the original source by a1kon.
    /// https://www.a1k0n.net/2011/07/20/donut-math.html
    /// </summary>
    protected override void UpdateFrame(ScreenData screen)
    {
        var a = 0.1 + 0.05 * FrameNumber;
        var b = 0.1 + 0.02 * FrameNumber;
        
        // Precompute sines and cosines of A and B
        var cosA = Math.Cos(a);
        var sinA = Math.Sin(a);
        var cosB = Math.Cos(b);
        var sinB = Math.Sin(b);

        // Clear output and z-buffer arrays
        screen.ClearChars();
        for (var i = 0; i < m_zBuffer.GetLength(0); i++)
        {
            for (var j = 0; j < m_zBuffer.GetLength(1); j++)
                m_zBuffer[i, j] = 0;
        }

        // Theta loop
        var screenWidth = Width;
        var screenHeight = Height;
        const double tau = 2.0 * Math.PI;
        for (var theta = 0.0; theta < tau; theta += ThetaSpacing)
        {
            var cosTheta = Math.Cos(theta);
            var sinTheta = Math.Sin(theta);
            var f1 = sinB * cosTheta;
            var f2 = cosA * cosTheta;
            var f3 = sinA * sinTheta;
            var f4 = cosA * sinTheta;
            var f5 = sinA * cosTheta;

            var circleX = R2 + R1 * cosTheta;
            var circleY = R1 * sinTheta;

            // Phi loop
            for (var phi = 0.0; phi < tau; phi += PhiSpacing)
            {
                var cosPhi = Math.Cos(phi);
                var sinPhi = Math.Sin(phi);

                // Luminance calculation
                var l = cosPhi * f1 - f2 * sinPhi - f3 + cosB * (f4 - f5 * sinPhi);
                if (l < 0)
                    continue;

                // Calculate 3D coordinates
                var x = circleX * (cosB * cosPhi + sinA * sinB * sinPhi) - circleY * cosA * sinB;
                var y = circleX * (sinB * cosPhi - sinA * cosB * sinPhi) + circleY * cosA * cosB;
                var z = K2 + cosA * circleX * sinPhi + circleY * sinA;
                var ooz = 1 / z;

                // Projection
                var xp = (int)(screenWidth / 2.0 + m_k1 * ooz * x);
                var yp = (int)(screenHeight / 2.0 - m_k1 * ooz * y);
                if (xp >= 0 && xp < screenWidth && yp >= 0 && yp < screenHeight)
                {
                    // Z-buffer check
                    if (ooz > m_zBuffer[xp, yp])
                    {
                        m_zBuffer[xp, yp] = ooz;
                        var luminanceIndex = (int)(l * 8);
                        screen.PrintAt(xp, yp, ".,-~:;=!*#$@"[Math.Min(11, luminanceIndex)]);
                    }
                }
            }
        }
    }
}