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
using System.Numerics;
using CSharp.Core.Extensions;

namespace G33kShell.Desktop.Console.Screensavers.Asteroids;

public class Ship
{
    private readonly int m_arenaWidth;
    private readonly int m_arenaHeight;
    private const float MaxSpeed = 0.2f;
    private const double MaxShield = 1.0;
    
    private Vector2 m_acceleration = Vector2.Zero;
    
    public enum Turn { None, Left, Right }
    public double Shield { get; set; }
    public float Theta { get; private set; }
    public static int MaxBullets => 8;

    /// <summary>
    /// Position in arena coordinates.
    /// </summary>
    public Vector2 Position { get; private set; }

    public Vector2 Velocity { get; private set; } = Vector2.Zero;

    public double DistanceTravelled { get; private set; }
    
    public bool IsThrusting { get; set; }
    public Turn Turning { get; set; }
    public bool IsShooting { get; set; }

    public Ship(int arenaWidth, int arenaHeight)
    {
        m_arenaWidth = arenaWidth;
        m_arenaHeight = arenaHeight;
        
        Reset();
    }

    public void Move()
    {
        // Turning.
        Theta += 0.03f * Turning switch
        {
            Turn.Left => -1,
            Turn.Right => 1,
            _ => 0
        };
        if (Theta < -MathF.PI) Theta += MathF.Tau;
        if (Theta > MathF.PI) Theta -= MathF.Tau;

        // Thrusting.
        m_acceleration = IsThrusting ? Theta.ToDirection() * 0.01f : Vector2.Zero;
        Velocity = (Velocity + m_acceleration) * 0.99f;
        if (Velocity.Length() > MaxSpeed)
            Velocity = Vector2.Normalize(Velocity) * MaxSpeed;
        
        // Position.
        Position += Velocity;
        DistanceTravelled += Velocity.Length();
        
        // Wrap to the arena.
        var newX = (Position.X + m_arenaWidth) % m_arenaWidth;
        var newY = (Position.Y + m_arenaHeight) % m_arenaHeight;
        Position = new Vector2(newX, newY);
    }

    /// <summary>
    /// Called when the ship is destroyed. Reset to the center of the arena.
    /// </summary>
    public void Reset()
    {
        Position = new Vector2(m_arenaWidth / 2.0f, m_arenaHeight / 2.0f);
        Shield = MaxShield;
        IsShooting = false;
        Turning = Turn.None;
        IsThrusting = false;
        Velocity = Vector2.Zero;
    }
}