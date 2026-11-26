// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Plays the forest ambush before control passes to the player.
/// Timings are relative to the soldier starting his attack in the set-6 Ax trace.
/// </summary>
internal sealed class ForestOpening
{
    private readonly Fighter m_villager, m_soldier;
    private int m_tick;
    // At four pixels per tick the soldier covers 428 pixels before the first swing.
    private const int AttackTick = 108;
    private static readonly string[][] Pages =
    [
        ["ALEX, WHAT HAS", "HAPPENED?..."],
        ["HIS MAJESTY AND THE", "PRINCESS HAVE BEEN", "TAKEN BY DEATH ADDER!"],
        ["REVENGE THEM FOR ME,", "MY FRIEND..."],
        ["ALEX..."]
    ];
    private static readonly int[] ReadingTicks = [100, 200, 140, 75];
    private int m_page, m_pageTicks;
    public bool ShowingDialogue => m_tick == AttackTick + 101 && m_page < Pages.Length;
    public string[] DialogueLines => Pages[m_page];
    public Fighter Speaker(Game game) => m_page is 1 or 2 ? m_villager : game.Hero;

    public ForestOpening(Game game, SpawnDefinition villager, SpawnDefinition soldier)
    {
        m_villager = game.Add(ActorKind.Villager, Moves.Thief, 0x42, 1, villager.X, villager.Z);
        m_soldier = game.Add(ActorKind.Villager, Moves.Heninger, 0x18, 1, soldier.X, soldier.Z);
        m_villager.Animation.Play(0xa170);
        m_soldier.Animation.Play(0xa228);
        game.Hero.X = game.Progress - 32;
        game.Hero.Z = villager.Z + 16;
        game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
    }

    public bool Tick(Game game, bool advanceDialogue)
    {
        if (ShowingDialogue)
        {
            if (++m_pageTicks < ReadingTicks[m_page] && !advanceDialogue)
                return false;
            m_pageTicks = 0;
            if (++m_page < Pages.Length)
                return false;

            // Resume at the first falling pose, even when every page was dismissed by hand.
            m_tick = AttackTick + 137;
        }

        var time = ++m_tick - AttackTick;
        if (time < 0)
        {
            m_soldier.X -= 4;
            m_soldier.Animation.Tick();
        }
        else if (time == 0)
            m_soldier.Animation.Play(0xa280, attack: true);
        else if (time < 55)
            m_soldier.Animation.Tick();
        else
        {
            m_soldier.Facing = 1;
            m_soldier.Animation.Play(0xa228);
            m_soldier.Animation.Tick();
            m_soldier.X += 4;
        }
        m_soldier.Y = game.Ground(m_soldier.X, m_soldier.Z);

        if (time <= 12)
        {
            // The pose itself travels 36 pixels per crutch stride. Carry that travel across each loop.
            if (m_villager.Animation.Tick())
                m_villager.X -= 37;
        }
        if (time == 12)
            m_villager.Animation.Play(0xa1ae, attack: true);
        else if (time == 138)
            m_villager.Animation.Seek(m_villager.Animation.Steps.Length - 1);
        else if (time > 12)
            m_villager.Animation.Tick();
        m_villager.Y = game.Ground(m_villager.X, m_villager.Z);

        var hero = game.Hero;
        if (time >= 0 && hero.X < m_villager.X - 28)
        {
            hero.Animation.Play(hero.Moves.Run);
            hero.X = Math.Min(hero.X + 2.75, m_villager.X - 28);
            hero.Y = game.Ground(hero.X, hero.Z);
        }
        else
            hero.Animation.Play(hero.Moves.Idle);
        hero.Animation.Tick();

        if (time < 146)
            return false;
        game.Actors.Remove(m_soldier);
        return true;
    }
}
