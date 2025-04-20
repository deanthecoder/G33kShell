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
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.Asteroids;

[DebuggerDisplay("Rating = {Rating}, Score = {Score}, Lives = {m_lives}")]
public class Game : AiGameBase
{
    private int m_bulletsFired;
    private int m_asteroidsHit;
    private int m_perfectHits;
    private int m_gameTicks;
    private int m_leftTurns;
    private int m_rightTurns;
    private GameState m_gameState;
    private int m_lives;
    private int m_thrustTicks;

    /// <summary>
    /// Score used for public display.
    /// </summary>
    public int Score { get; private set; }

    public override double Rating
    {
        get
        {
            if (Score == 0)
                return 0.0; // Penalize pacifists.
            if (m_gameTicks > 2000 && m_bulletsFired < 5)
                return 0.0; // Penalize idle campers.
            if (TurnEquality < 0.3)
                return 0.0; // Penalize wonky-turners.
            if (m_thrustTicks == 0)
                return 0.0; // Penalize non-thrusters.

            var thrustRatio = m_thrustTicks / (double)m_gameTicks;
            var survivalBonus = (m_gameTicks / 8000.0).Clamp(0.0, 1.0);

            return Score / 8000.0 * 10.0 +
                   HitRatio * 0.5 * 0.1 +
                   PerfectHitRatio * 1.0 * 0.1 +
                   thrustRatio * 2.0 * 0.1 +
                   TurnEquality * 0.5 * 0.1 +
                   survivalBonus * 1.0 * 0.1
                   ;
        }
    }
    
    public override IEnumerable<(string Name, string Value)> ExtraGameStats()
    {
        yield return ("Score", Score.ToString());
        yield return ("Accuracy", HitRatio.ToString("P1"));
        yield return ("Perfect", PerfectHitRatio.ToString("P1"));
        yield return ("Ticks", m_gameTicks.ToString());
        yield return ("TurnEquality", TurnEquality.ToString("P1"));
        yield return ("Thrusts", m_thrustTicks.ToString());
    }

    private double TurnEquality
    {
        get
        {
            var totalTurns = m_leftTurns + m_rightTurns;
            if (totalTurns == 0)
                return 0.0; // No turns - no bonus.
            return 1.0 - (double)Math.Abs(m_leftTurns - m_rightTurns) / totalTurns;
        }
    }

    public override bool IsGameOver => m_lives == 0 || m_gameTicks > 200_000;
    public Ship Ship { get; private set; }
    public List<Asteroid> Asteroids { get; } = [];
    public List<Bullet> Bullets { get; } = [];

    private double PerfectHitRatio => m_bulletsFired == 0 ? 0.0 : (double)m_perfectHits / m_bulletsFired;
    private double HitRatio => m_bulletsFired == 0 ? 0.0 : (double)m_asteroidsHit / m_bulletsFired;

    public Game(int arenaWidth, int arenaHeight) : base(arenaWidth, arenaHeight, new Brain())
    {
    }

    public override Game ResetGame()
    {
        Ship = new Ship(ArenaWidth, ArenaHeight);
        Score = 0;
        m_gameState = new GameState(Ship, Asteroids, Bullets, ArenaWidth, ArenaHeight);

        Asteroids.Clear();
        Bullets.Clear();
        m_bulletsFired = 0;
        m_perfectHits = 0;
        m_asteroidsHit = 0;
        m_gameTicks = 0;
        m_leftTurns = 0;
        m_rightTurns = 0;
        m_thrustTicks = 0;
        m_lives = 3;

        EnsureMinimumAsteroidCount();
        
        return this;
    }

    private void EnsureMinimumAsteroidCount()
    {
        while (Asteroids.FastSum(o => o.SizeMetric) < 12)
        {
            var pos = new Vector2(GameRand.NextFloat() * ArenaWidth, GameRand.NextFloat() * ArenaHeight);
            if (Vector2.Distance(pos, Ship.Position) < 25)
                continue; // Too close to the ship.

            Asteroids.Add(new Asteroid(pos, 0.1f, GameRand, ArenaWidth, ArenaHeight));
        }
    }

    public override void Tick()
    {
        if (IsGameOver)
            return;

        m_gameTicks++;

        // Spawn asteroids.
        EnsureMinimumAsteroidCount();

        // Move ship.
        Ship.Move();
        if (Ship.IsThrusting)
            m_thrustTicks++;

        if (Ship.Turning == Ship.Turn.Left)
            m_leftTurns++;
        else if (Ship.Turning == Ship.Turn.Right)
            m_rightTurns++;

        // Spawn new bullets.
        if (Ship.IsShooting && Bullets.Count < Ship.MaxBullets)
        {
            var target = Asteroids.FastFindMin(o => Vector2.DistanceSquared(Ship.Position, o.Position));
            Bullets.Add(new Bullet(Ship.Position, Ship.Theta, target, ArenaWidth, ArenaHeight));
            m_bulletsFired++;
        }

        // Move the asteroids.
        foreach (var asteroid in Asteroids)
            asteroid.Move();

        // Move the bullets.
        foreach (var bullet in Bullets)
            bullet.Move();

        // Remove expired bullets.
        Bullets.RemoveAll(o => o.IsExpired);

        // Check for bullet/asteroid collisions.
        var bulletsToRemove = new List<Bullet>();
        for (var index = 0; index < Bullets.Count; index++)
        {
            var bullet = Bullets[index];
            Asteroid hitAsteroid = null;
            // ReSharper disable once ForCanBeConvertedToForeach
            // ReSharper disable once LoopCanBeConvertedToQuery
            for (var i = 0; i < Asteroids.Count; i++)
            {
                if (Asteroids[i].IsInvulnerable || !Asteroids[i].Contains(bullet.Position))
                    continue;
                hitAsteroid = Asteroids[i];
                break;
            }

            if (hitAsteroid == null)
                continue; // Bullet not hitting anything.

            // Bullet hit an asteroid.
            bulletsToRemove.Add(bullet);
            hitAsteroid.Explode(Asteroids);
            Score += hitAsteroid.HitScore;

            // Bonus points if it was the asteroid we were aiming for.
            if (bullet.Target == hitAsteroid)
                m_perfectHits++;
            m_asteroidsHit++;
        }

        for (var i = 0; i < bulletsToRemove.Count; i++)
            Bullets.Remove(bulletsToRemove[i]);

        // Check for ship/asteroid collisions.
        const float shipRadius = 4.5f;
        for (var i = 0; i < Asteroids.Count; i++)
        {
            var distance = Asteroids[i].DistanceTo(Ship.Position);
            if (distance < Asteroids[i].Radius + shipRadius)
            {
                var newShield = Ship.Shield - 0.08;
                if (newShield < 0.0)
                {
                    // Ship is dead - Reset asteroids and bullets.
                    m_lives--;
                    Asteroids.Clear();
                    Bullets.Clear();
                    Ship.Reset();
                    return;
                }
                
                Ship.Shield = newShield;
                break;
            }
        }

        // Apply the AI.
        var moves = ((Brain)Brain).ChooseMoves(m_gameState);
        Ship.IsShooting = moves.IsShooting;
        Ship.Turning = moves.Turn;
        Ship.IsThrusting = moves.IsThrusting;
    }
}
