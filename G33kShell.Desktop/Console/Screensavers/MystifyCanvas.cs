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
using DTC.Core;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// Greyscale recreation of the classic Windows 95 Mystify screensaver.
/// </summary>
[UsedImplicitly]
public class MystifyCanvas : PixelScreensaverBase
{
    private const int PixelWidth = 320;
    private const int PixelHeight = 240;
    private const int WireCount = 2;
    private const int CornerCount = 4;
    private const int HistoryLength = 8;
    private const double MinSpeed = 1.0;
    private const double MaxSpeed = 2.0;

    private static readonly Rgb[] s_basePalette =
    [
        new Rgb(0, 0, 0),
        new Rgb(28, 28, 28),
        new Rgb(56, 56, 56),
        new Rgb(86, 86, 86),
        new Rgb(120, 120, 120),
        new Rgb(164, 164, 164),
        new Rgb(210, 210, 210),
        new Rgb(255, 255, 255)
    ];

    private readonly List<Wire> m_wires = new List<Wire>(WireCount);

    public MystifyCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
    {
        Name = "mystify";
    }

    public override void StartScreensaver(ScreenData shellScreen)
    {
        base.StartScreensaver(shellScreen);
        ResetField();
        StartPixelScreen(PixelWidth, PixelHeight);
        if (PixelScreen == null)
            return;

        using (PixelScreen.Lock(out var pixels))
            pixels.Clear();
    }

    public override void UpdateFrame(ScreenData screen)
    {
        ClearTextOverlay(screen);
        if (!IsPixelScreenActive || PixelScreen == null)
            return;

        using (PixelScreen.Lock(out var pixels))
        {
            pixels.Clear();
            foreach (var wire in m_wires)
            {
                wire.Advance();
                foreach (var frame in wire.History)
                    DrawFrame(pixels, frame);
                DrawFrame(pixels, wire.CurrentFrame);
            }
        }
    }

    protected override Rgb[] GetPixelPalette()
    {
        var foreground = Foreground ?? Rgb.White;
        var background = Background ?? Rgb.Black;
        return PixelScreenData.CreateGreyscalePalette(s_basePalette, background, foreground);
    }

    private void ResetField()
    {
        m_wires.Clear();
        for (var i = 0; i < WireCount; i++)
            m_wires.Add(i == 0 ? new Wire(2, 4) : new Wire(5, 7));
    }

    private static void DrawFrame(PixelScreenData screen, WireFrame frame)
    {
        for (var i = 0; i < frame.Points.Length; i++)
        {
            var a = frame.Points[i];
            var b = frame.Points[(i + 1) % frame.Points.Length];
            screen.DrawAntialiasedLine(a.X, a.Y, b.X, b.Y, frame.Shade);
        }
    }

    private sealed class Wire
    {
        private readonly Corner[] m_corners = new Corner[CornerCount];
        private readonly Queue<WireFrame> m_history = new Queue<WireFrame>(HistoryLength + 1);
        private readonly byte m_minShade;
        private readonly byte m_maxShade;
        private int m_shadePhase;

        public Wire(byte minShade, byte maxShade)
        {
            m_minShade = minShade;
            m_maxShade = maxShade;
            for (var i = 0; i < m_corners.Length; i++)
                m_corners[i] = new Corner();

            CurrentFrame = CreateFrame();
        }

        public IEnumerable<WireFrame> History => m_history;
        public WireFrame CurrentFrame { get; private set; }

        public void Advance()
        {
            m_history.Enqueue(CurrentFrame);
            if (m_history.Count > HistoryLength)
                m_history.Dequeue();

            foreach (var corner in m_corners)
                corner.Advance();

            m_shadePhase++;
            CurrentFrame = CreateFrame();
        }

        private WireFrame CreateFrame()
        {
            var points = new Point[m_corners.Length];
            for (var i = 0; i < points.Length; i++)
                points[i] = new Point(m_corners[i].X, m_corners[i].Y);
            return new WireFrame(points, GetShade());
        }

        private byte GetShade()
        {
            var shadeCount = m_maxShade - m_minShade + 1;
            return (byte)(m_minShade + m_shadePhase / 8 % shadeCount);
        }
    }

    private sealed class Corner
    {
        private double m_velocityX = RandomVelocity();
        private double m_velocityY = RandomVelocity();

        public double X { get; private set; } = Random.Shared.NextDouble() * (PixelWidth - 1);
        public double Y { get; private set; } = Random.Shared.NextDouble() * (PixelHeight - 1);

        public void Advance()
        {
            X += m_velocityX;
            Y += m_velocityY;

            if (X < 0)
            {
                X = 0;
                m_velocityX = MutateBounceSpeed(Math.Abs(m_velocityX));
            }
            else if (X >= PixelWidth)
            {
                X = PixelWidth - 1;
                m_velocityX = -MutateBounceSpeed(Math.Abs(m_velocityX));
            }

            if (Y < 0)
            {
                Y = 0;
                m_velocityY = MutateBounceSpeed(Math.Abs(m_velocityY));
            }
            else if (Y >= PixelHeight)
            {
                Y = PixelHeight - 1;
                m_velocityY = -MutateBounceSpeed(Math.Abs(m_velocityY));
            }
        }

        private static double RandomVelocity()
        {
            var speed = MinSpeed + Random.Shared.NextDouble() * (MaxSpeed - MinSpeed);
            return Random.Shared.Next(2) == 0 ? speed : -speed;
        }

        private static double MutateBounceSpeed(double speed)
        {
            var weighted = (Random.Shared.NextDouble() + Random.Shared.NextDouble()) * 0.5;
            var multiplier = 0.72 + weighted * 0.56;
            return Math.Clamp(speed * multiplier, MinSpeed, MaxSpeed);
        }
    }

    private readonly record struct Point(double X, double Y);
    private readonly record struct WireFrame(Point[] Points, byte Shade);
}
