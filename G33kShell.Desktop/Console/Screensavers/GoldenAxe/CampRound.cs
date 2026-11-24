// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// If you modify the code, please retain this copyright header.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Linq;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Runs the night-time rest and thief chase between main areas.
/// </summary>
/// <remarks>
/// Uses the recorded camp placements and preserves resources for the following area.
/// </remarks>
internal sealed class CampRound
{
    internal const int TimeLimit = 1050;
    internal const int ExitRoutingTicks = 90;
    internal const int ClearHoldTicks = 150;
    private readonly Game m_game;
    private readonly ArcadeData m_data;
    private readonly int m_startingPotions;
    private int m_wakeTick;
    private int? m_clearedAt;
    public int Ticks { get; private set; }
    public bool TimeExpired => Ticks >= TimeLimit;
    public int DepthOffset { get; }
    public bool Complete { get; private set; }

    public CampRound(Game game, ArcadeData data)
    {
        m_game = game;
        m_data = data;
        m_startingPotions = game.Potions;
        var hero = game.Hero;
        game.Actors.RemoveAll(a => a != hero);
        hero.Mount = hero.Victim = hero.HeldBy = null;
        hero.Vx = hero.Vy = 0;
        hero.Invulnerable = 0;
        hero.Facing = 1;
        var spot = data.CampSpawns.First(s => s.Routine == 0x2b0ae);
        hero.X = spot.X;
        // Keep the camp group above the HUD within the captured camera bounds.
        hero.Z = game.FindFloor(hero.X, spot.Z - 24, keepVisible: true);
        DepthOffset = (int)hero.Z - spot.Z;
        hero.Y = game.Ground(hero.X, hero.Z);
        hero.SetState(FighterState.Sleeping);
        hero.Animation.HoldFrame(game.Character.SleepFrame);
        var fireX = hero.X + 24;
        var fire = game.Add(ActorKind.Campfire, Moves.Thief, 0x45, 1, fireX, game.FindFloor(fireX, hero.Z + 2));
        fire.Animation.Play(0x18aa6);
        // These jars represent carried potions; only a successful theft removes one.
        for (var i = 0; i < Math.Min(2, game.Potions); i++)
        {
            var jar = game.Add(ActorKind.CampPotion, Moves.Thief, 0x4a, 1, hero.X - 28 + i * 12, hero.Z + 4);
            jar.Animation.Play(0x1870a);
        }
    }

    public void BeforeTick()
    {
        Ticks++;
        var hero = m_game.Hero;
        if (Ticks == 150)
        {
            foreach (var spawn in m_data.CampSpawns.Where(s => s.Routine is 0x17c7c or 0x17d00 or 0x17d24))
                m_game.Spawn(spawn);
        }
        if (hero.State == FighterState.Sleeping && m_wakeTick == 0 &&
            (m_game.Potions < m_startingPotions || m_startingPotions == 0 && m_game.Actors.Any(a =>
                a.Kind == ActorKind.Thief && Math.Abs(a.X - hero.X) < 40 && Math.Abs(a.Z - hero.Z) < 12)))
            m_wakeTick = Ticks + 20;
        // Wake shortly after the theft, or when a thief approaches an empty-handed hero.
        // Keep a deadline in case terrain prevents the thieves from reaching camp.
        if (hero.State == FighterState.Sleeping && (m_wakeTick > 0 && Ticks >= m_wakeTick || Ticks >= 450))
            hero.SetState(FighterState.GettingUp, hero.Moves.GetUp);
    }

    public void AfterTick()
    {
        if (m_clearedAt == null && Ticks > 400 && !m_game.Actors.Any(a => a.Kind == ActorKind.Thief && !a.IsGone) &&
            (Ticks >= 1300 || !m_game.Actors.Any(a => !a.IsGone && a.Kind is ActorKind.Potion or ActorKind.Food)))
            m_clearedAt = Ticks;

        Complete = Ticks - m_clearedAt >= ClearHoldTicks;
    }
}
