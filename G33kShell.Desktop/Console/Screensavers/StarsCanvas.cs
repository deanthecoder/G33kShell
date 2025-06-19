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

[DebuggerDisplay("StarsCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class StarsCanvas : ScreensaverBase
{
    private const int StarCount = 250;
    private const float MaxDepth = 10.0f;
    private const float StarSpeed = 2.0f;

    private Vector3[] m_stars;

    public StarsCanvas(int width, int height) : base(width, height)
    {
        Name = "stars";
    }

    /// <summary>
    /// Algorithm based on https://github.com/SnippetsDevelop/snippetsdevelop.github.io/blob/master/codes/FireChars.html
    /// </summary>
    public override void UpdateFrame(ScreenData screen)
    {
        m_stars ??= CreateStars();

        var timeSecs = (float)FrameNumber / TargetFps;
        
        var sceneBackground = new SceneBackground(Foreground, Background);
        var scene3D = new Scene3D(screen, sceneBackground);
        sceneBackground.Clear(screen, timeSecs);

        foreach (var star in m_stars)
        {
            var z = star.Z - timeSecs * StarSpeed;
            while (z < 0.0f)
                z += MaxDepth;

            var density = 1.0 - z / MaxDepth;
            var ch = ".•oO"[(int)density.Lerp(0.0, 3.9)];
            scene3D.Plot(star with { Z = z }, new Attr(ch, density.Lerp(Background, Foreground)));
        }
    }

    private static Vector3[] CreateStars()
    {
        var stars = new Vector3[StarCount];
        for (var i = 0; i < StarCount; i++)
        {
            var random = Random.Shared;
            var theta = (float)random.NextDouble() * MathF.PI * 2.0f;
            var dist = 0.25f + 2.25f * (float)random.NextDouble();
            
            var x = MathF.Cos(theta) * dist;
            var y = MathF.Sin(theta) * dist;
            var z = (float)random.NextDouble() * MaxDepth;
            stars[i] = new Vector3(x, y, z);
        }
        
        return stars;
    }
}