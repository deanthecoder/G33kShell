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
    private readonly Vector2[] m_offsets;
    private static readonly int[] Radii = [2, 4, MaxRadius];
    private static readonly int[] HitScores = [20, 10, 5];
    private static readonly int[] SizeMetrics = [1, 2, 4];
    private static readonly float[] Speeds = [1.5f, 1.0f, 0.6f];
    private int m_size = 2;
    private int m_invulnerable = 10;
    private const int MaxRadius = 7;
    
    public int SizeMetric => SizeMetrics[m_size];
    public int Radius => Radii[m_size];
    public int HitScore => HitScores[m_size];
    public bool IsInvulnerable => m_invulnerable > 0;
    
    /// <summary>
    /// Position in arena coordinates.
    /// </summary>
    public Vector2 Position { get; private set; }

    private Vector2 Velocity
    {
        get
        {
            var rotation = Matrix3x2.CreateRotation(m_direction);
            return Vector2.Transform(Vector2.UnitX, rotation);
        }
    }

    public double Shade { get; }

    public Asteroid(Vector2 position, float speed, Random rand, int arenaWidth, int arenaHeight, float direction = float.MaxValue)
    {
        m_speed = speed;
        m_rand = rand;
        m_arenaWidth = arenaWidth;
        m_arenaHeight = arenaHeight;
        
        Position = position;
        m_direction = float.IsNaN(direction) ? rand.NextFloat() * MathF.Tau : direction;
        Shade = 0.3 + 0.6 * rand.NextDouble();

        m_offsets =
        [
            new Vector2(m_arenaWidth, 0),
            new Vector2(-m_arenaWidth, 0),
            new Vector2(0, m_arenaHeight),
            new Vector2(0, -m_arenaHeight),
            new Vector2(m_arenaWidth, m_arenaHeight),
            new Vector2(-m_arenaWidth, -m_arenaHeight),
            new Vector2(m_arenaWidth, -m_arenaHeight),
            new Vector2(-m_arenaWidth, m_arenaHeight)
        ];
    }

    public void Move()
    {
        var v = Velocity * Speeds[m_size];

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
        
        m_invulnerable = Math.Max(m_invulnerable - 1, 0);
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

        // Spawn smaller asteroids evenly spaced around this one, moving away from the center.
        var countToSpawn = new[] { 2, 3, 4 }[m_size];
        var angleStep = MathF.Tau / countToSpawn;

        for (var i = 0; i < countToSpawn; i++)
        {
            var angle = i * angleStep * m_rand.NextFloat().Lerp(0.8f, 1.2f);
            var newPosition = Position + angle.ToDirection() * Radius * 0.5f;
            var newAsteroid = new Asteroid(newPosition, m_speed, m_rand, m_arenaWidth, m_arenaHeight, angle)
            {
                m_size = m_size - 1
            };

            asteroids.Add(newAsteroid);
        }
    }

    public float DistanceTo(Vector2 shipPosition)
    {
        var minDist = Vector2.Distance(shipPosition, Position);
        foreach (var offset in m_offsets)
            minDist = MathF.Min(minDist, Vector2.Distance(shipPosition, Position + offset));

        return minDist;
    }
}