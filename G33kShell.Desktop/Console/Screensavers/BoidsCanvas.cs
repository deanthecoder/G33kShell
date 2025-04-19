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
    private const int MaxBoids = 100;
    private readonly List<Boid> m_boids = [];
    private Ball m_ball;

    public BoidsCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
    {
        Name = "boids";
    }
    
    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);
        m_boids.Clear();

        const int radius = 10;
        m_ball = new Ball(Random.Shared.Next(radius, screen.Width - radius), Random.Shared.Next(radius, screen.Height - radius), radius);
    }
    
    public override void UpdateFrame(ScreenData screen)
    {
        while (m_boids.Count < MaxBoids)
            m_boids.Add(new Boid(Random.Shared.Next(0, screen.Width), Random.Shared.Next(0, screen.Height), Random.Shared));
        
        screen.Clear(Foreground, Background);

        var highResScreen = new HighResScreen(screen);
        var lightPos = -Vector2.Normalize(new Vector2(m_ball.X - screen.Width / 2.0f, m_ball.Y));
        highResScreen.DrawSphere(m_ball.X, m_ball.Y, m_ball.Radius, Foreground, Background, lightPos.X, lightPos.Y);

        foreach (var boid in m_boids)
            boid.Move(m_boids, m_ball);
        
        foreach (var boid in m_boids)
            boid.Draw(screen);
        
        m_ball.Move(screen);
    }

    [DebuggerDisplay("{X},{Y}")]
    private class Ball
    {
        private readonly double m_speed = 0.5;
        private double m_x;
        private double m_y;
        private double m_dx;
        private double m_dy;
        
        public int X => (int)m_x;
        public int Y => (int)m_y;
        public int Radius { get; }

        public Ball(int x, int y, int radius)
        {
            m_x = x;
            m_y = y;
            Radius = radius;

            var theta = Random.Shared.NextDouble();
            m_dx = Math.Cos(theta * Math.PI * 2.0);
            m_dy = Math.Sin(theta * Math.PI * 2.0);
        }

        public void Move(ScreenData screen)
        {
            var nextX = m_x + m_dx * m_speed;
            var nextY = m_y + m_dy * m_speed;
            
            if (nextX - Radius < 0.0 || nextX + Radius >= screen.Width)
                m_dx = -m_dx;
            if (nextY - Radius < 0.0 || nextY + Radius >= screen.Height * 2.0)
                m_dy = -m_dy;
            
            m_x += m_dx * m_speed;
            m_y += m_dy * m_speed;
        }
    }
    
    [DebuggerDisplay("{m_position}")]
    private class Boid
    {
        private Vector2 m_position;
        private Vector2 m_velocity;
        private static float MaxSpeed => 0.7f;
        private static float AttentionRadius => 18.0f;
        private float LimitFov { get; } = MathF.Cos(100.0f * MathF.PI / 180.0f);
        private static float VelocitySmoothness => 0.2f;
        private static float TargetSeparation => 4.0f;
        private static float SeparationFactor => 2.0f;
        private static float AlignmentFactor => 0.3f;
        private static float CohesionFactor => 0.01f;

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

            return acceleration;
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
            
            return acceleration;
        }

        private Vector2 MaintainSeparation(Boid[] boids)
        {
            if (boids.Length == 0)
                return Vector2.Zero;
            
            var targetSeparation2 = TargetSeparation * TargetSeparation;
            var acceleration = Vector2.Zero;
            foreach (var boid in boids)
            {
                var dir = m_position - boid.m_position;
                var dist = dir.LengthSquared();
                if (dist < 0.01 || dist > targetSeparation2)
                    continue;

                acceleration += Vector2.Normalize(dir) * MathF.Exp(-dist / targetSeparation2);
            }
            acceleration /= boids.Length;
            
            return acceleration;
        }
        
        private Vector2 AvoidSphere(Ball ball)
        {
            var toWall = new Vector2(ball.X, ball.Y / 2.0f) - m_position;
            toWall.Y *= 2.0f;
            var distToWall = toWall.Length() - ball.Radius;
            if (distToWall > TargetSeparation)
                return Vector2.Zero;
            
            var f = distToWall.Clamp(0.0f, TargetSeparation) / TargetSeparation;
            return Vector2.Normalize(toWall) * (f - 1.0f) * 0.35f;
        }

        public void Move(List<Boid> allBoids, Ball ball)
        {
            var boids = GetBoidsInRegion(allBoids);
            var acceleration =
                (MaintainSeparation(boids) + AvoidSphere(ball)) * SeparationFactor +
                MaintainAlignment(boids) * AlignmentFactor +
                MaintainCohesion(boids) * CohesionFactor;
            
            var desiredVelocity = Vector2.Normalize(m_velocity + acceleration) * MaxSpeed;
            m_velocity = m_velocity * VelocitySmoothness + desiredVelocity * (1.0f - VelocitySmoothness); // Smooth steering.
            
            m_position += m_velocity;
        }

        private Boid[] GetBoidsInRegion(List<Boid> allBoids)
        {
            var attentionRadius2 = AttentionRadius * AttentionRadius;
            var velocity = Vector2.Normalize(m_velocity);
            return allBoids
                .Where(o => Vector2.DistanceSquared(o.m_position, m_position) < attentionRadius2 &&
                            Vector2.Dot(Vector2.Normalize(o.m_position - m_position), velocity) > LimitFov &&
                            o != this)
                .ToArray();
        }

        public void Draw(ScreenData screen)
        {
            // Wrap boid position on screen.
            while (m_position.X < 0)
                m_position.X += screen.Width;
            m_position.X %= screen.Width;
            
            while (m_position.Y < 0)
                m_position.Y += screen.Height;
            m_position.Y %= screen.Height;

            screen.PrintAt((int)m_position.X, (int)m_position.Y, '☻');
        }
    }
}