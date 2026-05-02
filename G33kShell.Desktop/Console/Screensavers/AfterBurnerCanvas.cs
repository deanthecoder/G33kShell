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
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using DTC.Core;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// Shaded ball grid that rotates like the After Burner II intro.
/// </summary>
[DebuggerDisplay("AfterBurnerCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class AfterBurnerCanvas : ScreensaverBase
{
    private const int GridSize = 8;
    private const double FlyOutSeconds = 2.5;
    private const double RotateSeconds = 8.0;
    private const double FlyInSeconds = 2.5;
    private const double CycleSeconds = FlyOutSeconds + RotateSeconds + FlyInSeconds;
    private const double Perspective = 520.0;

    private static readonly Letter[] Letters =
    [
        new(
        [
            "111100",
            "110110",
            "110011",
            "110011",
            "110110",
            "111100"
        ]),
        new(
        [
            "111111",
            "001100",
            "001100",
            "001100",
            "001100",
            "001100"
        ]),
        new(
        [
            "001110",
            "011011",
            "110000",
            "110000",
            "011011",
            "001110"
        ])
    ];

    public AfterBurnerCanvas(int width, int height) : base(width, height)
    {
        Name = "afterburner";
    }

    public override void UpdateFrame(ScreenData screen)
    {
        screen.Clear(Foreground, Background);

        var highResScreen = new HighResScreen(screen, HighResScreen.DrawMode.LightenOnly);

        var cycle = Time / CycleSeconds;
        var letter = Letters[(int)Math.Floor(cycle) % Letters.Length];
        var cycleTime = (cycle - Math.Floor(cycle)) * CycleSeconds;
        var spread = GetSpread(cycleTime);

        var center = new Vector2(highResScreen.Width / 2.0f, highResScreen.Height / 2.0f);
        var spacing = GetSpacing(highResScreen);
        var radius = Math.Max(1, (int)Math.Round(spacing * 0.38));
        var projectedBalls = ProjectBalls(letter, center, spacing, spread, cycleTime);

        projectedBalls.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        foreach (var ball in projectedBalls)
        {
            var ballRadius = Math.Max(1, (int)Math.Round(radius * ball.Scale));
            var foreground = GetBallColor(ball.Kind, ball.Depth);
            var background = 0.03.Lerp(Background, foreground);
            highResScreen.DrawSphere(
                (int)Math.Round(ball.Position.X),
                (int)Math.Round(ball.Position.Y),
                ballRadius,
                foreground,
                background,
                -0.65 + Math.Sin(Time * 0.8) * 0.25,
                -0.75);
        }
    }

    private static double GetSpread(double cycleTime)
    {
        if (cycleTime < FlyOutSeconds)
            return Smooth(cycleTime / FlyOutSeconds);
        if (cycleTime < FlyOutSeconds + RotateSeconds)
            return 1.0;
        return 1.0 - Smooth((cycleTime - FlyOutSeconds - RotateSeconds) / FlyInSeconds);
    }

    private static double Smooth(double t)
    {
        t = t.Clamp(0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }

    private static double GetSpacing(HighResScreen screen)
    {
        var horizontal = screen.Width / 12.5;
        var vertical = screen.Height / 10.85;
        return Math.Max(3.0, Math.Min(horizontal, vertical));
    }

    private static List<ProjectedBall> ProjectBalls(Letter letter, Vector2 center, double spacing, double spread, double rotateTime)
    {
        var balls = new List<ProjectedBall>(GridSize * GridSize);
        var rotationY = rotateTime * 1.35;
        var rotationX = Math.Sin(rotateTime * 0.74) * 0.55;
        var sinY = Math.Sin(rotationY);
        var cosY = Math.Cos(rotationY);
        var sinX = Math.Sin(rotationX);
        var cosX = Math.Cos(rotationX);

        for (var y = 0; y < GridSize; y++)
        {
            for (var x = 0; x < GridSize; x++)
            {
                var localX = (x - (GridSize - 1) / 2.0) * spacing * spread;
                var localY = (y - (GridSize - 1) / 2.0) * spacing * spread;
                var localZ = 0.0;

                var rotatedX = localX * cosY + localZ * sinY;
                var rotatedZ = -localX * sinY + localZ * cosY;
                var rotatedY = localY * cosX - rotatedZ * sinX;
                rotatedZ = localY * sinX + rotatedZ * cosX;

                var scale = Perspective / (Perspective + rotatedZ);
                var position = new Vector2(
                    (float)(center.X + rotatedX * scale),
                    (float)(center.Y + rotatedY * scale));

                balls.Add(new ProjectedBall(
                    position,
                    rotatedZ,
                    scale.Clamp(0.65, 1.35),
                    GetBallKind(letter, x, y)));
            }
        }

        return balls;
    }

    private static BallKind GetBallKind(Letter letter, int x, int y)
    {
        if (x == 0 || y == 0 || x == GridSize - 1 || y == GridSize - 1)
            return BallKind.Frame;
        return letter.IsSet(x - 1, y - 1) ? BallKind.Lit : BallKind.Dim;
    }

    private Rgb GetBallColor(BallKind kind, double depth)
    {
        var baseBrightness = kind switch
        {
            BallKind.Lit => 1.18,
            BallKind.Frame => 0.44,
            _ => 0.08
        };
        var depthBrightness = (-depth).InverseLerp(-170.0, 170.0);
        var depthContrast = kind == BallKind.Lit ? 0.55 : 0.35;
        var brightness = (baseBrightness * (0.62 + depthBrightness * depthContrast)).Clamp(0.04, 1.35);
        return brightness.Lerp(Background, Foreground);
    }

    private enum BallKind
    {
        Dim,
        Frame,
        Lit
    }

    private readonly record struct ProjectedBall(Vector2 Position, double Depth, double Scale, BallKind Kind);

    private readonly record struct Letter(string[] Rows)
    {
        public bool IsSet(int x, int y) => Rows[y][x] == '1';
    }
}
