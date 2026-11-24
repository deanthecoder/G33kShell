// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Collections.Generic;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Stores the position, animation and combat state of one actor.
/// </summary>
/// <remarks>
/// Game owns mutations; the coded controller only reads this state.
/// </remarks>
internal sealed class Fighter
{
    public int Id { get; }
    public ActorKind Kind { get; }
    public Moves Moves { get; set; }
    public Animation Animation { get; }
    public double X, Z, Y, Vx, Vy;
    public int Facing = 1, Palette, Health, Age, StateTicks, Invulnerable, Cooldown, Hits, Combo, Items, Flash, Knees, HitTimer, ComboTimer, MountCooldown, MountWaitTicks;
    public bool Connected, Entered, FoodThief;
    public int UnmountedTicks, EscapeDirection, EscapeBlockedTicks;
    public int KnockdownVariation;
    public int PetrifiedTick;
    public int PickupTick = -1;
    public Fighter Mount, Rider, Victim, HeldBy, Owner, PendingMount;
    public bool IsMount => Kind is ActorKind.Chicken or ActorKind.BlueDragon or ActorKind.RedDragon;
    public Moves FreeMoves => Kind is ActorKind.BlueDragon or ActorKind.RedDragon ? Moves.BlueDragon : Moves.Chicken;
    public bool FlameStarted;
    public bool PotionBounced;
    public FighterState State { get; private set; }
    public HashSet<int> Struck { get; } = [];
    public bool IsEnemy => Kind is ActorKind.Heninger or ActorKind.Longmoan or ActorKind.BadBrother or ActorKind.Amazon or ActorKind.Knight or ActorKind.Skeleton or ActorKind.DeathAdder;
    public bool IsGone => State is FighterState.Gone or FighterState.Petrified;
    public bool Vulnerable => Kind is not (ActorKind.Flame or ActorKind.Fireball or ActorKind.Lightning or ActorKind.Campfire or ActorKind.CampPotion or ActorKind.Food or ActorKind.Potion) && Health > 0 && Invulnerable == 0 && Mount == null && State is not (FighterState.Emerging or FighterState.Sleeping or FighterState.Collapsing or FighterState.Flying or FighterState.Down or FighterState.GettingUp or FighterState.Dying or FighterState.Held or FighterState.Respawning or FighterState.Gone or FighterState.Magic);
    public bool IsFree => State is FighterState.Idle or FighterState.Walking;

    public Fighter(int id, ArcadeData data, ActorKind kind, Moves moves, int palette, int health)
    {
        Id = id;
        Kind = kind;
        Moves = moves;
        Palette = palette;
        Health = health;
        Animation = new Animation(data);
        SetState(FighterState.Idle, moves.Idle);
    }

    public void SetState(FighterState state, int address = 0, bool attack = false)
    {
        State = state;
        StateTicks = 0;
        if (!IsFree)
        {
            PendingMount = null;
            MountWaitTicks = 0;
        }
        Struck.Clear();
        if (address != 0)
            Animation.Play(address, true, attack);
    }
}
