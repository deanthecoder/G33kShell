// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
// purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Collections.Generic;
using System.Numerics;
using DTC.Core;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A phosphor pen traces two overlapping spirographs, then lets the drawing fade.
/// </summary>
[UsedImplicitly]
public class SpirographCanvas : PixelScreensaverBase
{
    private const int PixelWidth = 320;
    private const int PixelHeight = 200;
    private const int SceneFps = 30;
    private const int HoldFrames = 4 * SceneFps;
    private const int FadeFrames = 3 * SceneFps;
    private const byte InkShade = 170;
    private const byte InnerInkShade = 100;
    private static readonly Rgb[] s_basePalette = CreateBasePalette();

    private readonly PixelScreenData m_drawing = new(PixelWidth, PixelHeight, s_basePalette);
    private readonly PixelScreenData m_innerDrawing = new(PixelWidth, PixelHeight, s_basePalette);
    private readonly List<Vector2> m_path = [];
    private int m_nextPoint;
    private int m_restFrames;
    private Vector2 m_pen;
    private double m_pixelsPerFrame;
    private Vector2 m_center;
    private double m_radius;
    private bool m_isInnerDrawing;

    public SpirographCanvas(int width, int height) : base(width, height, SceneFps)
    {
        Name = "spirograph";
    }

    public override void StartScreensaver(ScreenData shellScreen)
    {
        base.StartScreensaver(shellScreen);
        NewDrawing();
        StartPixelScreen(PixelWidth, PixelHeight);
    }

    public override void UpdateFrame(ScreenData screen)
    {
        ClearTextOverlay(screen);
        if (!IsPixelScreenActive || PixelScreen == null)
            return;

        if (m_nextPoint >= m_path.Count && !m_isInnerDrawing)
        {
            m_isInnerDrawing = true;
            CreatePath(m_radius * (0.65 + Random.Shared.NextDouble() * 0.15));
        }

        var isDrawing = m_nextPoint < m_path.Count;
        if (isDrawing)
            AdvancePen();
        else
            m_restFrames++;

        using (PixelScreen.Lock(out var pixels))
        {
            var opacity = 1.0 - Math.Clamp((m_restFrames - HoldFrames) / (double)FadeFrames, 0.0, 1.0);
            for (var i = 0; i < pixels.Pixels.Length; i++)
                pixels.Pixels[i] = (byte)(Math.Max(m_drawing.Pixels[i], m_innerDrawing.Pixels[i]) * opacity);

            // The moving nib is drawn only on the display, never baked into the ink.
            if (isDrawing)
            {
                pixels.DrawAntialiasedLine(m_pen.X - 1.5, m_pen.Y, m_pen.X + 1.5, m_pen.Y, 220);
                pixels.DrawAntialiasedLine(m_pen.X, m_pen.Y - 1.5, m_pen.X, m_pen.Y + 1.5, 220);
                pixels.SetPixel((int)Math.Round(m_pen.X), (int)Math.Round(m_pen.Y), 255);
            }
        }

        if (m_restFrames >= HoldFrames + FadeFrames)
        {
            NewDrawing();
            OnCycleCompleted();
        }
    }

    protected override Rgb[] GetPixelPalette() =>
        PixelScreenData.CreateGreyscalePalette(s_basePalette, Background ?? Rgb.Black, Foreground ?? Rgb.White);

    private void AdvancePen()
    {
        var remaining = m_pixelsPerFrame;
        while (remaining > 0 && m_nextPoint < m_path.Count)
        {
            var target = m_path[m_nextPoint];
            var distance = Vector2.Distance(m_pen, target);
            var reachesTarget = distance <= remaining;
            var next = reachesTarget ? target : Vector2.Lerp(m_pen, target, (float)(remaining / distance));
            // Separate layers keep the dimmer inner curve from erasing brighter crossings.
            var drawing = m_isInnerDrawing ? m_innerDrawing : m_drawing;
            drawing.DrawAntialiasedLine(m_pen.X, m_pen.Y, next.X, next.Y, m_isInnerDrawing ? InnerInkShade : InkShade);
            m_pen = next;
            remaining -= distance;
            if (reachesTarget)
                m_nextPoint++;
        }
    }

    private void NewDrawing()
    {
        m_drawing.Clear();
        m_innerDrawing.Clear();
        m_restFrames = 0;
        m_isInnerDrawing = false;
        m_radius = 67 + Random.Shared.NextDouble() * 23;
        m_center = new Vector2((float)(100 + Random.Shared.NextDouble() * 120), PixelHeight / 2f);
        CreatePath(m_radius);
    }

    private void CreatePath(double radius)
    {
        m_path.Clear();

        // Coprime gear sizes ensure a complete design without retracing shorter loops.
        var fixedGear = Random.Shared.Next(5, 18);
        var rollingGear = Random.Shared.Next(2, fixedGear);
        while (GreatestCommonDivisor(fixedGear, rollingGear) != 1)
            rollingGear = Random.Shared.Next(2, fixedGear);
        var outside = Random.Shared.Next(4) == 0;
        var orbit = outside ? fixedGear + rollingGear : fixedGear - rollingGear;
        var penOffset = rollingGear * (0.35 + Random.Shared.NextDouble() * 1.15);
        var scale = radius / (orbit + penOffset);
        var rotation = Random.Shared.NextDouble() * Math.Tau;
        var steps = fixedGear * rollingGear * 180;
        double length = 0;
        for (var i = 0; i <= steps; i++)
        {
            var angle = Math.Tau * rollingGear * i / steps;
            var gearAngle = angle * orbit / rollingGear;
            var x = orbit * Math.Cos(angle) + (outside ? -1 : 1) * penOffset * Math.Cos(gearAngle);
            var y = orbit * Math.Sin(angle) - penOffset * Math.Sin(gearAngle);
            var point = m_center + new Vector2(
                (float)(scale * (x * Math.Cos(rotation) - y * Math.Sin(rotation))),
                (float)(scale * (x * Math.Sin(rotation) + y * Math.Cos(rotation))));
            if (m_path.Count > 0)
                length += Vector2.Distance(m_path[^1], point);
            m_path.Add(point);
        }

        m_pen = m_path[0];
        m_nextPoint = 1;
        // Approximately constant pen speed, with longer designs capped at 35 seconds.
        m_pixelsPerFrame = Math.Max(100, length / 35) / SceneFps;
    }

    private static int GreatestCommonDivisor(int a, int b)
    {
        while (b != 0)
            (a, b) = (b, a % b);
        return a;
    }

    private static Rgb[] CreateBasePalette()
    {
        var palette = new Rgb[256];
        for (var i = 0; i < palette.Length; i++)
            palette[i] = new Rgb((byte)i, (byte)i, (byte)i);
        return palette;
    }
}
