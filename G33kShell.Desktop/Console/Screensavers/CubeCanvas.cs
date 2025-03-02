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
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console._3D;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A canvas to displaying an animated 3D scene.
/// </summary>
[DebuggerDisplay("CubeCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class CubesCanvas : ScreensaverBase
{
    private SceneBackground m_background;
    private SceneObject m_cube;

    public CubesCanvas(int width, int height) : base(width, height)
    {
        Name = "cube";
    }

    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);

        m_background = new ChequeredSceneBackground(Foreground, Background, 0.08);
        m_cube = new CubeObject(0.5f, [
            new Attr('.', Foreground, Background),
            new Attr(';', Foreground, Background),
            new Attr('i', Foreground, Background),
            new Attr('S', Foreground, Background),
            new Attr('8', Foreground, Background),
            new Attr('l', Foreground, Background)
        ]);
    }

    public override void UpdateFrame(ScreenData screen)
    {
        var time = (float)Time;

        // Prepare the scene.
        var r = new Vector3(1.8f, 0.3f, 0.7f) * time;
        m_cube.Transform =
            Matrix4x4
                .CreateTranslation(0, 0, 0.75f + 8.0f * (0.5f + 0.5f * MathF.Sin(time)))
                .RotateYz(r.X)
                .RotateXz(r.Y)
                .RotateXy(r.Z);

        // Draw the scene.
        var scene = new Scene3D(screen, m_background);
        scene.AddObject(m_cube);
        scene.Render(time);
    }
}