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
using System.Diagnostics.CodeAnalysis;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

[DebuggerDisplay("GravityWellCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class GravityWellCanvas : ScreensaverBase
{
    private const double OrbitSeconds = 18.0;
    private const double CollapseSeconds = 4.0;
    private const double BurstSeconds = 8.0;
    private const double CycleSeconds = OrbitSeconds + CollapseSeconds + BurstSeconds;
    private const double BurstCoreSeconds = 4.0;

    private readonly List<Particle> m_particles = [];
    private readonly Well[] m_wells =
    [
        new Well(0.0, 0.27, 0.24, 46.0),
        new Well(2.1, 0.32, 0.18, 38.0),
        new Well(4.2, 0.23, 0.29, 42.0)
    ];
    private Phase m_lastPhase;
    private int m_lastWidth;
    private int m_lastHeight;

    public GravityWellCanvas(int width, int height) : base(width, height, targetFps: 30)
    {
        Name = "gravitywell";
    }

    public override void BuildScreen(ScreenData screen)
    {
        ResetParticles(screen);
        DrawFrame(screen, GetCycleTime(), Phase.Orbit);
    }

    public override void UpdateFrame(ScreenData screen)
    {
        if (m_particles.Count == 0 || m_lastWidth != screen.Width || m_lastHeight != screen.Height)
            ResetParticles(screen);

        var cycleTime = GetCycleTime();
        var phase = GetPhase(cycleTime);
        if (phase == Phase.Burst && m_lastPhase != Phase.Burst)
            BurstParticles(screen);
        else if (phase == Phase.Orbit && m_lastPhase == Phase.Burst)
            ResetParticles(screen);

        var dt = 1.0 / TargetFps;
        UpdateParticles(screen, cycleTime, phase, dt);
        DrawFrame(screen, cycleTime, phase);

        m_lastPhase = phase;
    }

    private double GetCycleTime() =>
        (FrameNumber / (double)TargetFps) % CycleSeconds;

    private static Phase GetPhase(double cycleTime)
    {
        if (cycleTime < OrbitSeconds)
            return Phase.Orbit;
        return cycleTime < OrbitSeconds + CollapseSeconds ? Phase.Collapse : Phase.Burst;
    }

    private void ResetParticles(ScreenData screen)
    {
        m_particles.Clear();
        m_lastWidth = screen.Width;
        m_lastHeight = screen.Height;
        m_lastPhase = GetPhase(GetCycleTime());

        var count = (screen.Width * screen.Height / 4).Clamp(120, 420);
        for (var i = 0; i < count; i++)
        {
            var x = Random.Shared.NextDouble() * Math.Max(1, screen.Width - 1);
            var y = Random.Shared.NextDouble() * Math.Max(1, screen.Height - 1);
            var angle = Random.Shared.NextDouble() * Math.PI * 2.0;
            var speed = Random.Shared.NextDouble().Lerp(1.0, 4.5);
            m_particles.Add(new Particle(x, y, Math.Cos(angle) * speed, Math.Sin(angle) * speed));
        }
    }

    private void UpdateParticles(ScreenData screen, double cycleTime, Phase phase, double dt)
    {
        foreach (var particle in m_particles)
        {
            if (phase != Phase.Burst)
                ApplyGravity(screen, particle, cycleTime, phase, dt);
            else
                particle.Brightness = Math.Max(particle.Brightness * 0.998, 0.55);

            particle.Vx *= 0.998;
            particle.Vy *= 0.998;
            LimitSpeed(particle, phase == Phase.Burst ? 34.0 : 18.0);

            particle.X += particle.Vx * dt;
            particle.Y += particle.Vy * dt;
            WrapParticle(screen, particle);
        }
    }

    private void ApplyGravity(ScreenData screen, Particle particle, double cycleTime, Phase phase, double dt)
    {
        foreach (var well in m_wells)
        {
            var position = GetWellPosition(screen, well, cycleTime, phase);
            var dx = position.X - particle.X;
            var dy = (position.Y - particle.Y) * 1.75;
            var distanceSq = dx * dx + dy * dy + 10.0;
            var force = well.Strength / distanceSq;

            particle.Vx += dx * force * dt;
            particle.Vy += dy * force * dt / 1.75;
            particle.Brightness = Math.Max(particle.Brightness, force.Clamp(0.0, 1.0));
        }
    }

    private void BurstParticles(ScreenData screen)
    {
        var centerX = screen.Width / 2.0;
        var centerY = screen.Height / 2.0;
        foreach (var particle in m_particles)
        {
            var dx = particle.X - centerX;
            var dy = (particle.Y - centerY) * 1.7;
            var distance = Math.Sqrt(dx * dx + dy * dy).Clamp(0.75, 999.0);
            var speed = Random.Shared.NextDouble().Lerp(4.5, 7.5);
            particle.Vx = dx / distance * speed + Random.Shared.NextDouble().Lerp(-0.75, 0.75);
            particle.Vy = dy / distance * speed / 1.7 + Random.Shared.NextDouble().Lerp(-0.5, 0.5);
            particle.Brightness = 1.0;
        }
    }

    private static void LimitSpeed(Particle particle, double maxSpeed)
    {
        var speedSq = particle.Vx * particle.Vx + particle.Vy * particle.Vy;
        if (speedSq <= maxSpeed * maxSpeed)
            return;

        var scale = maxSpeed / Math.Sqrt(speedSq);
        particle.Vx *= scale;
        particle.Vy *= scale;
    }

    private static void WrapParticle(ScreenData screen, Particle particle)
    {
        if (particle.X < 0)
            particle.X += screen.Width;
        else if (particle.X >= screen.Width)
            particle.X -= screen.Width;

        if (particle.Y < 0)
            particle.Y += screen.Height;
        else if (particle.Y >= screen.Height)
            particle.Y -= screen.Height;
    }

    private void DrawFrame(ScreenData screen, double cycleTime, Phase phase)
    {
        screen.Clear(Foreground, Background);
        foreach (var particle in m_particles)
            DrawParticle(screen, particle);

        if (phase == Phase.Burst)
            DrawBurstCore(screen, cycleTime);
        else
            DrawWells(screen, cycleTime, phase);
    }

    private void DrawParticle(ScreenData screen, Particle particle)
    {
        var brightness = particle.Brightness.Clamp(0.08, 1.0);
        var x = (int)Math.Round(particle.X);
        var y = (int)Math.Round(particle.Y);
        var ch = ".·*oO"[(int)brightness.Clamp(0.0, 0.999).Lerp(0.0, 4.999)];
        screen.PrintAt(x, y, ch, brightness.Lerp(Background, Foreground));

        particle.Brightness = 0.08.Lerp(particle.Brightness, 0.16);
    }

    private void DrawWells(ScreenData screen, double cycleTime, Phase phase)
    {
        foreach (var well in m_wells)
        {
            var position = GetWellPosition(screen, well, cycleTime, phase);
            var pulse = (Math.Sin(cycleTime * 6.0 + well.Angle) + 1.0) * 0.5;
            var brightness = phase == Phase.Collapse ? pulse.Lerp(0.75, 1.2) : pulse.Lerp(0.45, 0.95);
            var x = (int)Math.Round(position.X);
            var y = (int)Math.Round(position.Y);

            screen.PrintAt(x, y, brightness > 0.9 ? '@' : 'O', brightness.Clamp(0.0, 1.2).Lerp(Background, Foreground));
            screen.PrintAt(x - 1, y, '-', 0.35.Lerp(Background, Foreground));
            screen.PrintAt(x + 1, y, '-', 0.35.Lerp(Background, Foreground));
            screen.PrintAt(x, y - 1, '|', 0.35.Lerp(Background, Foreground));
            screen.PrintAt(x, y + 1, '|', 0.35.Lerp(Background, Foreground));
        }
    }

    private void DrawBurstCore(ScreenData screen, double cycleTime)
    {
        var burstTime = cycleTime - OrbitSeconds - CollapseSeconds;
        var brightness = (1.0 - burstTime / BurstCoreSeconds).Clamp(0.0, 1.0);
        if (brightness <= 0.0)
            return;

        var x = screen.Width / 2;
        var y = screen.Height / 2;
        screen.PrintAt(x, y, '@', brightness.Lerp(Background, Foreground));
        screen.PrintAt(x - 1, y, '*', (brightness * 0.65).Lerp(Background, Foreground));
        screen.PrintAt(x + 1, y, '*', (brightness * 0.65).Lerp(Background, Foreground));
    }

    private static (double X, double Y) GetWellPosition(ScreenData screen, Well well, double cycleTime, Phase phase)
    {
        var t = cycleTime * 0.42 + well.Angle;
        var centerX = screen.Width / 2.0;
        var centerY = screen.Height / 2.0;
        var orbitX = centerX + Math.Cos(t) * screen.Width * well.RadiusX;
        var orbitY = centerY + Math.Sin(t * 1.23) * screen.Height * well.RadiusY;

        if (phase != Phase.Collapse)
            return (orbitX, orbitY);

        var f = ((cycleTime - OrbitSeconds) / CollapseSeconds).Clamp(0.0, 1.0);
        f = f * f * (3.0 - 2.0 * f);
        return (f.Lerp(orbitX, centerX), f.Lerp(orbitY, centerY));
    }

    private enum Phase
    {
        Orbit,
        Collapse,
        Burst
    }

    private record Well(double Angle, double RadiusX, double RadiusY, double Strength);

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private class Particle
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public double Brightness = 0.3;

        public Particle(double x, double y, double vx, double vy)
        {
            X = x;
            Y = y;
            Vx = vx;
            Vy = vy;
        }
    }
}
