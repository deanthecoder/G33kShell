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
    private const int MaxBoids = 25;
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
        {
            var newBoid = new Boid(m_rand.Next(0, screen.Width), m_rand.Next(0, screen.Height), m_rand.NextDouble() * 2.0 - 1.0, m_rand.NextDouble() * 2.0 - 1.0, m_rand.NextDouble().Lerp(0.7, 1.0));
            m_boids.Add(newBoid);
        }
        
        m_boids[0].IgnoreCohesion = true;
    }
    
    public override void UpdateFrame(ScreenData screen)
    {
        screen.Clear(Foreground, Background);

        foreach (var boid in m_boids)
        {
            boid.MaintainSeparation(m_boids);
            boid.MaintainAlignment(m_boids);
            boid.MaintainCohesion(m_boids);
            boid.Move();
        }
        
        foreach (var boid in m_boids)
            boid.Draw(screen);
    }
    
    private class Boid
    {
        private double m_maxSpeed = 0.10;
        private const double MinSpeed = 0.07;
        private const double DivertSpeed = 0.05;
        private const double TargetSeparation = 3.0; // Ideal gap between boids.
        private const double AttractionDist = 20.0;  // Boids within this distance align velocity.
        
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Vx { get; private set; }
        public double Vy { get; private set; }
        public bool IgnoreCohesion { get; set; }

        public Boid(double x, double y, double vx, double vy, double speedTweak)
        {
            var v = Math.Sqrt(vx * vx + vy * vy);
            vx *= m_maxSpeed * speedTweak / v;
            vy *= m_maxSpeed * speedTweak / v;

            X = x;
            Y = y;
            Vx = vx;
            Vy = vy;
        }

        public void MaintainSeparation(List<Boid> boids)
        {
            var dx = 0.0;
            var dy = 0.0;
            var count = 0;

            // Don't let boid get too close to any other.
            foreach (var other in boids)
            {
                if (other == this)
                    continue;

                var dx2 = X - other.X;
                var dy2 = Y - other.Y;
                var dist = Math.Sqrt(dx2 * dx2 + dy2 * dy2);

                if (dist < TargetSeparation)
                {
                    dx += dx2;
                    dy += dy2;
                    count++;
                }
            }
            
            // Nudge it away.
            if (count > 0)
            {
                Vx += dx / count * DivertSpeed;
                Vy += dy / count * DivertSpeed;
            }
        }

        public void MaintainAlignment(List<Boid> boids)
        {
            var avgVx = 0.0;
            var avgVy = 0.0;
            var count = 0;
            
            // Don't let boid get too close to any other.
            foreach (var other in boids)
            {
                if (other == this)
                    continue;
                
                if (DistanceToBoid(other) < AttractionDist)
                {
                    avgVx += other.Vx;
                    avgVy += other.Vy;
                    count++;
                }
            }
            
            // Align velocities.
            if (count > 0)
            {
                avgVx /= count;
                avgVy /= count;
                
                var l = Math.Sqrt(avgVx * avgVx + avgVy * avgVy);
                if (l > 0)
                {
                    avgVx /= l;
                    avgVy /= l;
                    Vx += (avgVx - Vx) * 0.02;
                    Vy += (avgVy - Vy) * 0.02;
                }
            }
        }

        public void MaintainCohesion(List<Boid> boids)
        {
            var centerX = 0.0;
            var centerY = 0.0;
            var count = 0;
            
            // Move towards center of mass.
            foreach (var other in boids)
            {
                if (other == this)
                    continue;

                if (DistanceToBoid(other) < AttractionDist)
                {
                    centerX += other.X;
                    centerY += other.Y;
                    count++;
                }
            }

            if (count > 0)
            {
                centerX /= count;
                centerY /= count;

                var dx = centerX - X;
                var dy = centerY - Y;
                var l = Math.Sqrt(dx * dx + dy * dy);
                if (l > 0)
                {
                    dx /= l;
                    dy /= l;
                    
                    var cohesion = IgnoreCohesion ? 0.005 : 0.02;
                    Vx += dx * cohesion;
                    Vy += dy * cohesion;
                }
            }
        }

        public void Move()
        {
            X += Vx;
            Y += Vy;

            var speed = Math.Sqrt(Vx * Vx + Vy * Vy);
            if (speed > MinSpeed)
            {
                Vx *= 0.95;
                Vy *= 0.95;
            }
        }

        public void Draw(ScreenData screen)
        {
            X = (X + screen.Width) % screen.Width;
            Y = (Y + screen.Height) % screen.Height;

            // Detect prominent north/south/east/west direction.
            var dx = Math.Sign(Vx);
            var ch = "◄ ►"[dx + 1];

            var dy = Math.Sign(Vy);
            if (Math.Abs(Vy) > Math.Abs(Vx))
                ch = "▲ ▼"[dy + 1];

            screen.PrintAt((int)X, (int)Y, ch);
        }

        private double DistanceToBoid(Boid other) =>
            Math.Sqrt(Math.Pow(other.X - X, 2.0) + Math.Pow(other.Y - Y, 2.0));
    }
}