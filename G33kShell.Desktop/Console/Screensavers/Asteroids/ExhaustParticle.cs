using System;
using System.Numerics;

namespace G33kShell.Desktop.Console.Screensavers.Asteroids;

public class ExhaustParticle
{
    private readonly int m_arenaWidth;
    private readonly int m_arenaHeight;
    private int m_ticksRemaining;
    private Vector2 m_velocity;

    public Vector2 Position { get; private set; }
    public double Brightness => Math.Clamp(m_ticksRemaining / 14.0, 0.0, 1.0);
    public bool IsExpired => m_ticksRemaining <= 0;

    public ExhaustParticle(Vector2 position, Vector2 velocity, int arenaWidth, int arenaHeight, int lifetimeTicks)
    {
        Position = position;
        m_velocity = velocity;
        m_arenaWidth = arenaWidth;
        m_arenaHeight = arenaHeight;
        m_ticksRemaining = lifetimeTicks;
    }

    public void Move()
    {
        Position += m_velocity;
        m_velocity *= 0.92f;

        var newX = (Position.X + m_arenaWidth) % m_arenaWidth;
        var newY = (Position.Y + m_arenaHeight) % m_arenaHeight;
        Position = new Vector2(newX, newY);
        m_ticksRemaining--;
    }
}
