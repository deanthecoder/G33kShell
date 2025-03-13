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
using System.Linq;
using System.Numerics;
using CSharp.Core;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A screensaver that simulates flocking using boids.
/// </summary>
[DebuggerDisplay("BoidsCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class BoidsCanvas : ScreensaverBase
{
    private const int MaxBoids = 80;
    private readonly Random m_rand = new Random();
    private readonly List<Boid> m_boids = [];

    public BoidsCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
    {
        Name = "boids";
    }
    
    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);

        m_boids.Clear();
        while (m_boids.Count < MaxBoids)
            m_boids.Add(new Boid(m_rand.Next(0, screen.Width), m_rand.Next(0, screen.Height), m_rand));
    }
    
    public override void UpdateFrame(ScreenData screen)
    {
        screen.Clear(Foreground, Background);

        foreach (var boid in m_boids)
            boid.Move(m_boids);
        
        foreach (var boid in m_boids)
            boid.Draw(screen, Foreground, Background);
    }
    
    [DebuggerDisplay("{m_position}")]
    private class Boid
    {
        private const float MaxSpeed = 0.7f;
        private const float TargetSeparation = 5.0f; // Ideal gap between boids.
        private const float AttentionRadius = 20.0f; // Boids within this distance affect each other.
        private const float SeparationFactor = 2.0f;
        private const float AlignmentFactor = 0.05f;
        private const float CohesionFactor = 0.04f;
        private readonly char[] m_symbols = [ '►', '◄', '▲', '►', '◄', '►', '▼', '◄' ];
        private Vector2 m_position;
        private Vector2 m_velocity;
        private double m_bright;

        public Boid(double x, double y, Random random)
        {
            m_position = new Vector2((float)x, (float)y);

            // Set velocity to a random direction.
            var angle = (float)(random.NextDouble() * Math.PI * 2.0);
            m_velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * MaxSpeed;
        }
        
        private static Vector2 MaintainAlignment(Boid[] boids)
        {
            if (boids.Length == 0)
                return Vector2.Zero;
            
            // Find the average velocity.
            var acceleration = Vector2.Zero;
            foreach (var boid in boids)
                acceleration += boid.m_velocity;
            acceleration /= boids.Length;

            return acceleration * 0.2f;
        }

        private Vector2 MaintainCohesion(Boid[] boids)
        {
            if (boids.Length == 0)
                return Vector2.Zero;
            
            // Find the center of mass.
            var acceleration = Vector2.Zero;
            foreach (var boid in boids)
                acceleration += boid.m_position - m_position;
            acceleration /= boids.Length;
            
            return acceleration * 0.2f;
        }

        private Vector2 MaintainSeparation(Boid[] boids)
        {
            if (boids.Length == 0)
                return Vector2.Zero;
            
            var acceleration = Vector2.Zero;
            foreach (var boid in boids)
            {
                var dir = m_position - boid.m_position;
                var dist = dir.LengthSquared();
                if (dist < 0.01 || dist > TargetSeparation * TargetSeparation)
                    continue;

                acceleration += Vector2.Normalize(dir) * (TargetSeparation / (dist + 0.1f));
            }
            acceleration /= boids.Length;
            
            return acceleration;
        }

        public void Move(List<Boid> allBoids)
        {
            const float attentionRadius2 = AttentionRadius * AttentionRadius;
            var boids =
                allBoids
                    .Where(o => Vector2.DistanceSquared(o.m_position, m_position) < attentionRadius2 && o != this)
                    .ToArray();
            m_bright = ((double)boids.Length / MaxBoids).Clamp(0.0, 1.0).Lerp(0.2, 1.0); 
            var acceleration =
                MaintainSeparation(boids) * SeparationFactor +
                MaintainAlignment(boids) * AlignmentFactor +
                MaintainCohesion(boids) * CohesionFactor;
            
            m_velocity += acceleration;
            if (m_velocity.Length() > MaxSpeed)
                m_velocity = Vector2.Normalize(m_velocity) * MaxSpeed;
            
            m_position += m_velocity;
        }

        private int GetDirection()
        {
            var angle = MathF.Atan2(-m_velocity.Y, m_velocity.X);

            // Normalize angle to [0, 2π]
            var normalizedAngle = (angle + MathF.PI * 2) % (MathF.PI * 2);

            // Convert to an index (0 to 7)
            return (int)(normalizedAngle / (MathF.PI / 4));
        }

        public void Draw(ScreenData screen, Rgb foreground, Rgb background)
        {
            // Wrap boid position on screen.
            while (m_position.X < 0)
                m_position.X += screen.Width;
            m_position.X %= screen.Width;
            
            while (m_position.Y < 0)
                m_position.Y += screen.Height;
            m_position.Y %= screen.Height;

            var dirIndex = GetDirection();
            screen.PrintAt((int)m_position.X, (int)m_position.Y, m_symbols[dirIndex]);
            screen.SetForeground((int)m_position.X, (int)m_position.Y, m_bright.Lerp(background, foreground));
        }
    }
}