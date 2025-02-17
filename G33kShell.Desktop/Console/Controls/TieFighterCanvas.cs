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
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Controls;

/// <summary>
/// A canvas to displaying an animated 3D scene.
/// </summary>
[DebuggerDisplay("TieFighterCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class TieFighterCanvas : ScreensaverBase
{
    private SceneBackground m_background;
    private SceneObject m_wing1;
    private SceneObject m_wing2;
    private SceneObject m_cockpit;
    private LandscapeObject m_landscape;

    public TieFighterCanvas(int width, int height) : base(width, height)
    {
        Name = "tiefighter";
    }

    protected override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);

        m_background = new SceneBackground(Foreground, Background);
        Attr[] wingMaterials = [
            new Attr('.', Foreground, Background),
            new Attr(';', Foreground, Background),
            new Attr('i', Foreground, Background),
            new Attr('l', Foreground, Background),
            new Attr('\\', Foreground, Background),
            new Attr('l', Foreground, Background),
            new Attr('|', Foreground, Background),
            new Attr('\\', Foreground, Background)
        ];
        m_wing1 = new HexagonalObject(0.5f, 0.06f, wingMaterials);
        m_wing1.LocalPosition = new Vector3(0, 0.22f, 0);
        m_wing2 = new HexagonalObject(0.5f, 0.06f, wingMaterials);
        m_wing2.LocalPosition = new Vector3(0, -0.22f, 0);
        m_cockpit = new HexagonalObject(0.2f, 0.3f, [
            new Attr('o', Foreground, Background),
            new Attr('o', Foreground, Background),
            new Attr('i', Foreground, Background),
            new Attr('-', Foreground, Background),
            new Attr('/', Foreground, Background),
            new Attr('l', Foreground, Background),
            new Attr('-', Foreground, Background),
            new Attr('-', Foreground, Background)
        ]);

        m_landscape = new LandscapeObject(15, 0.2f, GetTerrainHeight, MaterialFunc);
        m_landscape.WorldPosition = new Vector3(0, 0.25f, 0);
        return;
        
        Attr MaterialFunc(Vector3 v)
        {
            var f = (1.0f - v.Y) / 0.8;
            f *= 1.0 - ((double)v.Z).InverseLerp(1.0, 1.5).Clamp(0.0, 1.0);
            var ch = f.ToAscii();
            f *= 0.75;
            return new Attr(ch, f.Lerp(Background, Foreground), (f * 0.15).Lerp(Background, Foreground));
        }
    }

    private static float GetTerrainHeight(float time, Vector3 v)
    {
        var f = (v.X * 18.32f, v.Z * 31.7465f - time * 89.2f).SmoothNoise();
        return f * 0.8f - 1.0f;
    }

    protected override void UpdateFrame(ScreenData screen)
    {
        var time = 1.0f * FrameNumber / TargetFps;

        // Prepare the scene.
        var tiePosition = new Vector3(0.5f - (time * 25.43f).SmoothNoise(), -0.35f - (time * 39.2f).SmoothNoise() * 0.3f, 1.5f * (time * 22.22f).SmoothNoise());

        const float pi = MathF.PI;
        var r = new Vector3(0.5f * (time * 10.0f).SmoothNoise(), 0.2f - 0.15f * (time * 30.0f).SmoothNoise(), 0);
        m_wing1.Rotation = new Vector3(0, 0, pi / 2).Rotate(r);
        m_wing2.Rotation = new Vector3(0, 0, pi / 2).Rotate(r);
        m_cockpit.Rotation = new Vector3(pi / 2, 0, 0).Rotate(r);

        m_wing1.WorldPosition = tiePosition;
        m_wing2.WorldPosition = tiePosition;
        m_cockpit.WorldPosition = tiePosition;
        
        m_landscape.Rotation = new Vector3(0.3f, 0, 0);
        m_landscape.Update(time);

        // Draw the scene.
        var scene = new Scene3D(screen, m_background);
        scene.AddObject(m_landscape);
        scene.AddObject(m_wing1);
        scene.AddObject(m_wing2);
        scene.AddObject(m_cockpit);
        scene.Render(time);
    }
}