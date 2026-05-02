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
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using G33kShell.Desktop.Console.Extensions;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

[DebuggerDisplay("ChaosBallsCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class ChaosBallsCanvas : ScreensaverBase
{
    private const int MaxBallCount = 250;
    private const double Radius = 0.48;
    private const double Gravity = 18.0;
    private const double InitialSpeed = 10.0;
    private const double CappedBounceSeconds = 10.0;
    private const int PhysicsSteps = 3;

    private readonly List<Ball> m_balls = [];
    private readonly Stopwatch m_phaseStopwatch = new Stopwatch();
    private Phase m_phase;

    public ChaosBallsCanvas(int width, int height) : base(width, height, targetFps: 30)
    {
        Name = "chaosballs";
    }

    public override void BuildScreen(ScreenData screen)
    {
        ResetSimulation();
        DrawFrame(screen);
    }

    public override void UpdateFrame(ScreenData screen)
    {
        var dt = 1.0 / TargetFps / PhysicsSteps;
        for (var step = 0; step < PhysicsSteps; step++)
        {
            MoveBalls(dt);
            ResolveBallCollisions();
            DespawnDrainedBalls();
        }

        if (m_phase == Phase.Capped && m_phaseStopwatch.Elapsed.TotalSeconds >= CappedBounceSeconds)
            m_phase = Phase.Draining;

        if (m_phase == Phase.Draining && m_balls.Count == 0)
            ResetSimulation();

        DrawFrame(screen);
    }

    private void ResetSimulation()
    {
        m_balls.Clear();
        m_phase = Phase.Growing;
        m_phaseStopwatch.Reset();

        var centerX = Width / 2.0;
        var centerY = Height / 2.0;
        m_balls.Add(CreateBall(centerX - 2.0, centerY, -1));
        m_balls.Add(CreateBall(centerX + 2.0, centerY, 1));
    }

    private Ball CreateBall(double x, double y, int preferredDirection = 0)
    {
        var angle = Random.Shared.NextDouble() * Math.PI * 2.0;
        if (preferredDirection != 0)
            angle = Random.Shared.NextDouble().Lerp(-0.65, 0.65) + (preferredDirection < 0 ? Math.PI : 0.0);

        return new Ball
        {
            X = x.Clamp(1.0 + Radius, Math.Max(1.0 + Radius, Width - 2.0 - Radius)),
            Y = y.Clamp(1.0 + Radius, Math.Max(1.0 + Radius, Height - 2.0 - Radius)),
            Vx = Math.Cos(angle) * InitialSpeed,
            Vy = Math.Sin(angle) * InitialSpeed * 0.65,
            Glyph = "●Oo"[Random.Shared.Next(3)],
            Shade = Random.Shared.NextDouble().Lerp(0.55, 1.15)
        };
    }

    private void SpawnCenterBall()
    {
        if (m_phase != Phase.Growing || m_balls.Count >= MaxBallCount)
            return;

        if (!IsCenterSpawnClear())
            return;

        var spawnPoint = GetSpawnPoint();
        m_balls.Add(CreateBall(spawnPoint.X, spawnPoint.Y));
        if (m_balls.Count < MaxBallCount)
            return;

        m_phase = Phase.Capped;
        m_phaseStopwatch.Restart();
    }

    private bool IsCenterSpawnClear()
    {
        var spawnPoint = GetSpawnPoint();
        foreach (var ball in m_balls)
        {
            if (Math.Abs(ball.X - spawnPoint.X) <= 1.0 && Math.Abs(ball.Y - spawnPoint.Y) <= 1.0)
                return false;
        }

        return true;
    }

    private (double X, double Y) GetSpawnPoint() =>
        (Width / 2.0, Height * 0.33);

    private void MoveBalls(double dt)
    {
        var left = 1.0 + Radius;
        var right = Math.Max(left, Width - 2.0 - Radius);
        var top = 1.0 + Radius;
        var bottom = Math.Max(top, Height - 2.0 - Radius);

        foreach (var ball in m_balls)
        {
            ball.Vy += Gravity * dt;
            ball.X += ball.Vx * dt;
            ball.Y += ball.Vy * dt;

            if (ball.X < left)
            {
                ball.X = left;
                ball.Vx = Math.Abs(ball.Vx);
            }
            else if (ball.X > right)
            {
                ball.X = right;
                ball.Vx = -Math.Abs(ball.Vx);
            }

            if (ball.Y < top)
            {
                ball.Y = top;
                ball.Vy = Math.Abs(ball.Vy);
            }
            else if (m_phase != Phase.Draining && ball.Y > bottom)
            {
                ball.Y = bottom;
                ball.Vy = -Math.Abs(ball.Vy);
            }
        }
    }

    private void ResolveBallCollisions()
    {
        var minDistance = Radius * 2.0;
        var minDistanceSq = minDistance * minDistance;

        for (var i = 0; i < m_balls.Count; i++)
        {
            for (var j = i + 1; j < m_balls.Count; j++)
            {
                var a = m_balls[i];
                var b = m_balls[j];
                var dx = b.X - a.X;
                var dy = b.Y - a.Y;
                var distanceSq = dx * dx + dy * dy;
                if (distanceSq <= 0.0001 || distanceSq >= minDistanceSq)
                    continue;

                var distance = Math.Sqrt(distanceSq);
                var nx = dx / distance;
                var ny = dy / distance;
                var relativeVx = a.Vx - b.Vx;
                var relativeVy = a.Vy - b.Vy;
                var separatingSpeed = relativeVx * nx + relativeVy * ny;

                SeparateBalls(a, b, nx, ny, (minDistance - distance) * 0.5);
                if (separatingSpeed <= 0.0)
                    continue;

                a.Vx -= separatingSpeed * nx;
                a.Vy -= separatingSpeed * ny;
                b.Vx += separatingSpeed * nx;
                b.Vy += separatingSpeed * ny;
                SpawnCenterBall();
            }
        }
    }

    private static void SeparateBalls(Ball a, Ball b, double nx, double ny, double overlap)
    {
        a.X -= nx * overlap;
        a.Y -= ny * overlap;
        b.X += nx * overlap;
        b.Y += ny * overlap;
    }

    private void DespawnDrainedBalls() =>
        m_balls.RemoveAll(o => o.Y > Height + 2.0);

    private void DrawFrame(ScreenData screen)
    {
        screen.Clear(Foreground, Background);
        DrawBorder(screen);

        foreach (var ball in m_balls)
        {
            var x = (int)Math.Round(ball.X);
            var y = (int)Math.Round(ball.Y);
            var color = ball.Shade.Clamp(0.35, 1.2).Lerp(Background, Foreground);
            screen.PrintAt(x, y, ball.Glyph, color);
        }

        var status = m_phase switch
        {
            Phase.Growing => $"{m_balls.Count}/{MaxBallCount}",
            Phase.Capped => $"Full {(CappedBounceSeconds - m_phaseStopwatch.Elapsed.TotalSeconds).Clamp(0.0, CappedBounceSeconds),4:0.0}s",
            _ => $"Draining {m_balls.Count}"
        };
        status = $" {status} ";
        screen.PrintAt(Math.Max(2, (screen.Width - status.Length) / 2), 0, status, Foreground.WithBrightness(0.85));
    }

    private void DrawBorder(ScreenData screen)
    {
        if (screen.Width < 2 || screen.Height < 2)
            return;

        screen.DrawBox(0, 0, screen.Width - 1, screen.Height - 1);
        if (m_phase != Phase.Draining)
            return;

        for (var x = 1; x < screen.Width - 1; x++)
            screen.PrintAt(x, screen.Height - 1, ' ');
    }

    private enum Phase
    {
        Growing,
        Capped,
        Draining
    }

    private class Ball
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Vx { get; set; }
        public double Vy { get; set; }
        public char Glyph { get; init; }
        public double Shade { get; init; }
    }
}
