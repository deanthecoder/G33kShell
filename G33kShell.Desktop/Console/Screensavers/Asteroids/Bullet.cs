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
using System.Numerics;
using CSharp.Core.Extensions;

namespace G33kShell.Desktop.Console.Screensavers.Asteroids;

public class Bullet
{
    private readonly int m_arenaWidth;
    private readonly int m_arenaHeight;
    private readonly Vector2 m_velocity;
    private double m_distanceUntilDeath;

    /// <summary>
    /// Position in arena coordinates.
    /// </summary>
    public Vector2 Position { get; private set; }

    public bool IsExpired { get; private set; }

    /// <summary>
    /// The target the bullet is nominally aiming for.
    /// </summary>
    public Asteroid Target { get; }

    public Bullet(Vector2 position, float theta, Asteroid target, int arenaWidth, int arenaHeight)
    {
        m_arenaWidth = arenaWidth;
        m_arenaHeight = arenaHeight;
        Position = position;
        Target = target;
        m_velocity = theta.ToDirection();
        m_distanceUntilDeath = new Vector2(arenaWidth, arenaHeight).Length() * 0.4;
    }

    public void Move()
    {
        var newX = (Position.X + m_velocity.X + m_arenaWidth) % m_arenaWidth;
        var newY = (Position.Y + m_velocity.Y + m_arenaHeight) % m_arenaHeight;
        Position = new Vector2(newX, newY);
        
        m_distanceUntilDeath -= m_velocity.Length();
        if (m_distanceUntilDeath < 0)
            IsExpired = true;
    }
}