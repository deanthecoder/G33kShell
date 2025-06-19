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
using System.Numerics;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console._3D;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

[DebuggerDisplay("TunnelCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class TunnelCanvas : ScreensaverBase
{
    private const int PointCount = 1800;
    private const float MaxDepth = 50.0f;
    private const float PointSpeed = 12.0f;
    private const float TunnelRadius = 2.5f;

    private Vector3[] m_points;

    public TunnelCanvas(int width, int height) : base(width, height)
    {
        Name = "tunnel";
    }

    public override void UpdateFrame(ScreenData screen)
    {
        m_points ??= CreatePoints();

        var timeSecs = (float)FrameNumber / TargetFps;
        
        var sceneBackground = new SceneBackground(Foreground, Background);
        var scene3D = new Scene3D(screen, sceneBackground);
        sceneBackground.Clear(screen, timeSecs);

        foreach (var point in m_points)
        {
            // Recycle points.
            var z = point.Z - timeSecs * PointSpeed;
            while (z < 0.0f)
                z += MaxDepth;

            // Apply bending.
            var bendX = MathF.Sin((z + timeSecs * 3.0f) * 0.3f) * 0.8f;
            var bendY = ((z + timeSecs * 30.0f).SmoothNoise() - 0.5f) * 4.0f;

            var x = point.X + bendX;
            var y = point.Y + bendY;

            var density = Math.Pow(1.0f - z / MaxDepth, 2.0f);
            var ch = ".•oO"[(int)density.Lerp(0.0, 3.9)];
            scene3D.Plot(new Vector3(x, y, z), new Attr(ch, density.Lerp(Background, Foreground)));
        }
    }

    private static Vector3[] CreatePoints()
    {
        var points = new Vector3[PointCount];
        for (var i = 0; i < PointCount; i++)
        {
            var random = Random.Shared;
            var theta = (float)random.NextDouble() * MathF.PI * 2.0f;

            var x = MathF.Cos(theta) * TunnelRadius;
            var y = MathF.Sin(theta) * TunnelRadius;
            var z = (float)random.NextDouble() * MaxDepth;
            points[i] = new Vector3(x, y, z);
        }
        
        return points;
    }
}