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
using CSharp.Core.Extensions;

namespace G33kShell.Desktop.Console.Screensavers.Asteroids;

[DebuggerDisplay("Radius: {Radius}")]
public class Asteroid
{
    private readonly float m_speed;
    private readonly Random m_rand;
    private readonly int m_arenaWidth;
    private readonly int m_arenaHeight;
    private readonly float m_direction;
    private static readonly int[] Radii = [2, 4, MaxRadius];
    private static readonly int[] HitScores = [20, 10, 5];
    private static readonly int[] SizeMetrics = [1, 2, 4];
    private int m_size = 2;

    public const int MaxRadius = 7;
    public int SizeMetric => SizeMetrics[m_size];
    public int Radius => Radii[m_size];
    public int HitScore => HitScores[m_size];
    
    /// <summary>
    /// Position in arena coordinates.
    /// </summary>
    public Vector2 Position { get; private set; }

    public Vector2 Velocity
    {
        get
        {
            var rotation = Matrix3x2.CreateRotation(m_direction);
            return Vector2.Transform(Vector2.UnitX, rotation);
        }
    }

    public double Shade { get; }

    public Asteroid(Vector2 position, float speed, Random rand, int arenaWidth, int arenaHeight)
    {
        m_speed = speed;
        m_rand = rand;
        m_arenaWidth = arenaWidth;
        m_arenaHeight = arenaHeight;
        
        Position = position;
        m_direction = rand.NextFloat() * MathF.PI * 2.0f;
        Shade = 0.3 + 0.6 * rand.NextDouble();
    }

    public void Move()
    {
        var v = Velocity;

        var maxX = m_arenaWidth + Radius;
        var maxY = m_arenaHeight + Radius;
        var minX = -Radius;
        var minY = -Radius;

        var nextX = Position.X + v.X * m_speed;
        var nextY = Position.Y + v.Y * m_speed;

        if (nextX > maxX) nextX = minX;
        if (nextX < minX) nextX = maxX;
        if (nextY > maxY) nextY = minY;
        if (nextY < minY) nextY = maxY;

        Position = new Vector2(nextX, nextY);
    }

    /// <summary>
    /// Used to detect if a bullet has collided with this asteroid.
    /// </summary>
    public bool Contains(Vector2 pt) =>
        Vector2.Distance(pt, Position) <= Radius;

    /// <summary>
    /// Remove this asteroid and (conditionally) spawn smaller ones.
    /// </summary>
    public void Explode(List<Asteroid> asteroids)
    {
        asteroids.Remove(this);
        if (m_size == 0)
            return; // This is the smallest an asteroid can be.
        
        // Spawn smaller asteroids.
        var countToSpawn = new[] { 2, 3, 4 }[m_size];
        for (var i = 0; i < countToSpawn; i++)
            asteroids.Add(new Asteroid(Position, m_speed, m_rand, m_arenaWidth, m_arenaHeight) { m_size = m_size - 1 });
    }
}