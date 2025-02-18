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
    private SceneObject m_tie;
    private LandscapeObject m_landscape;

    public TieFighterCanvas(int width, int height) : base(width, height, 20)
    {
        Name = "tiefighter";
    }

    protected override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);

        m_background = new SceneBackground(Foreground, Background);
        
        var wing = new HexagonalObject(0.5f, 0.05f, [
            new Attr('1', Foreground, Background),
            new Attr('|', Foreground, Background),
            new Attr('/', Foreground, Background),
            new Attr('4', Foreground, Background),
            new Attr('5', Foreground, Background),
            new Attr('6', Foreground, Background),
            new Attr('-', Foreground, Background),
            new Attr('\\', Foreground, Background)
        ]);
        var cockpit = new HexagonalObject(0.25f, 0.3f, [
            new Attr('O', Foreground, Background),
            new Attr('B', Foreground, Background),
            new Attr('\\', Foreground, Background),
            new Attr('D', Foreground, Background),
            new Attr('E', Foreground, Background),
            new Attr('F', Foreground, Background),
            new Attr('o', Foreground, Background),
            new Attr('/', Foreground, Background)
        ]);
        var spar = new CubeObject(0.7f, 0.1f, 0.1f, new[]
        {
            new Attr('1', Foreground, Background),
            new Attr('2', Foreground, Background),
            new Attr('3', Foreground, Background),
            new Attr('4', Foreground, Background),
            new Attr('=', Foreground, Background),
            new Attr('6', Foreground, Background),
        });
        m_tie = new SceneObject();
        m_tie.Add(wing, Matrix4x4.Identity.Translate(-0.4f, 0, 0).RotateXy(MathF.PI / 2.0f));
        m_tie.Add(wing, Matrix4x4.Identity.Translate(0.4f, 0, 0).RotateXy(MathF.PI / 2.0f));
        m_tie.Add(cockpit);
        m_tie.Add(spar);

        var glassRgb = Foreground.WithBrightness(0.5);
        var glass = new HexagonalObject(0.15f, 0.4f, [
            new Attr('o', glassRgb, Background),
            new Attr('o', glassRgb, Background),
            new Attr('o', glassRgb, Background),
            new Attr('o', glassRgb, Background),
            new Attr('o', glassRgb, Background),
            new Attr('o', glassRgb, Background),
            new Attr('o', glassRgb, Background),
            new Attr('o', glassRgb, Background)
        ]);
        m_tie.Add(glass);
        
        m_landscape = new LandscapeObject(15, 0.5f, GetTerrainHeight, GetLandscapeMaterial)
        {
            Transform = Matrix4x4.Identity.Translate(0, -1.0f, 0.0f).RotateYz(-0.16f)
        };
        return;

        Attr GetLandscapeMaterial(Vector3 v)
        {
            var f = v.Y / 0.6;
            f *= 1.0 - ((double)v.Z).InverseLerp(3.0, 3.75).Clamp(0.0, 1.0);
            var ch = f.ToAscii();
            f *= 0.75;
            return new Attr(ch, f.Lerp(Background, Foreground), (f * 0.15).Lerp(Background, Foreground));
        }
    }

    private static float GetTerrainHeight(float time, Vector3 v)
    {
        var f = (v.X * 18.32f, v.Z * 31.7465f - time * 110.2f).SmoothNoise();
        return f * 0.6f;
    }

    private float? m_tieHeight;
    protected override void UpdateFrame(ScreenData screen)
    {
        var time = 1.0f * FrameNumber / TargetFps;

        // Prepare the scene.
        var tieX = (time * 30.0f).SmoothNoise() - 0.5f;
        var tieZ = (time * 20.0f).SmoothNoise() * 5.0f;
        var targetTieHeight = 0.4f + GetTerrainHeight(time, new Vector3(tieX, 0, tieZ - 5.0f)) * 0.7f;
        m_tieHeight = m_tieHeight == null ? targetTieHeight : 0.9f.Lerp(targetTieHeight, (float)m_tieHeight);
        m_tie.Transform =
            Matrix4x4.Identity
                .Translate(
                    tieX,
                    (float)m_tieHeight,
                    tieZ)
                .RotateXy(0.5f * tieX)
                .RotateXz(0.1f + (time * 7.0f).SmoothNoise() * 0.2f)
                .RotateYz(-MathF.PI / 2.0f);
        m_landscape.Update(time);

        // Draw the scene.
        var scene = new Scene3D(screen, m_background);
        scene.AddObject(m_landscape);
        scene.AddObject(m_tie);
        scene.Render(time);
    }
}