// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Linq;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Chooses movement and attacks from the current game state.
/// </summary>
/// <remarks>
/// This coded policy can later be replaced by a trained input policy.
/// </remarks>
internal sealed class CodedController
{
    private Fighter m_escapingHero;
    private double m_escapeZ;
    private Game m_magicGame;
    private int? m_magicWaitTick;
    private Fighter m_lightningJumpHero;
    private Fighter m_edgeWaitingHero;

    public GameInput Read(Game game)
    {
        var hero = game.Hero.Mount ?? game.Hero;
        var needsFood = game.Hero.Health < 48;
        var needsMagic = game.Potions < game.Character.PotionCapacity;
        if (!game.IsCamp && game.Boss && !game.Actors.Any(a => a.IsEnemy && a.Health > 0))
            return default;
        if (ShouldCastEmergencyMagic(game, hero))
            return new GameInput(Magic: true);
        if (m_lightningJumpHero == hero && hero.State is FighterState.Crouching or FighterState.Jumping)
            return default;
        m_lightningJumpHero = null;
        if (hero.IsFree && AvoidLightning(game, hero) is { } lightningDodge)
            return lightningDodge;
        if (hero.IsFree && AvoidCharge(game, hero) is { } sidestep)
            return sidestep;
        bool CloseThreat(Fighter a) => (a.IsEnemy || a.Rider?.IsEnemy == true) && a.Vulnerable && a.State != FighterState.Hurt &&
            Math.Abs(a.X - hero.X) < 48 && Math.Abs(a.Z - hero.Z) < 10 && Math.Abs(a.Y - hero.Y) < 12;
        var hasCloseThreat = !game.IsCamp && game.Actors.Any(CloseThreat);
        var target = game.Actors.Where(a => (a.IsEnemy || a.Rider?.IsEnemy == true || a.Kind == ActorKind.Thief && a.Items > 0) && a.Vulnerable)
            .Where(a => !game.IsCamp || game.Project(a.X, a.Z, a.Y).X is >= 24 and <= 296)
            .Where(a => !game.IsCamp || a.Kind != ActorKind.Thief || (a.FoodThief ? needsFood : needsMagic))
            .OrderBy(a => needsFood && a.FoodThief ? 0 : 1)
            .ThenBy(a => hasCloseThreat && !CloseThreat(a) ? 1 : 0)
            .ThenBy(a => Math.Abs(a.X - hero.X) + Math.Abs(a.Z - hero.Z) * 3).FirstOrDefault();
        if (hero.State == FighterState.Jumping)
        {
            if (Math.Abs(hero.Vx) < 2 && game.Ground(hero.X + hero.Facing * 16, hero.Z) >= 5248)
                return default;
            return new GameInput(hero.Facing, Attack: target != null && Math.Abs(target.X - hero.X) < 55);
        }
        if (hero == game.Hero && ShouldCastMagic(game))
            return new GameInput(Magic: true);
        if (hero.IsFree && AvoidCrowd(game, hero) is { } escape)
            return escape;
        var item = game.Actors.Where(a => !a.IsGone && (a.Kind == ActorKind.Potion && needsMagic || a.Kind == ActorKind.Food && needsFood))
            .Where(a => game.IsCamp || a.Kind == ActorKind.Food
                ? game.ProjectView(a.X, a.Z, a.Y).X is >= 12 and <= 305
                : a.X >= game.Progress && Math.Abs(a.X - hero.X) < 100)
            .OrderBy(a => a.Kind == ActorKind.Food && needsFood ? 0 : 1)
            .ThenBy(a => Math.Abs(a.X - hero.X) + Math.Abs(a.Z - hero.Z) * 3).FirstOrDefault();
        if (needsFood && target?.FoodThief == true && item?.Kind == ActorKind.Potion)
            item = null;
        var heroScreenX = game.ProjectView(hero.X, hero.Z, hero.Y).X;
        var targetScreenX = target == null ? 160 : game.ProjectView(target.X, target.Z, target.Y).X;
        if (!game.IsCamp && game.Hero.Health > 12 && hero.IsFree && item == null && target != null && target.IsFree &&
            (target.IsEnemy || target.Rider?.IsEnemy == true) && Math.Abs(target.Y - hero.Y) <= 24 &&
            !game.Actors.Any(a => game.HasImminentStrike(a) && Math.Abs(a.X - hero.X) < 65 &&
                Math.Abs(a.Z - hero.Z) < 12 && Math.Abs(a.Y - hero.Y) < 12) &&
            (targetScreenX < 16 && heroScreenX < 32 || targetScreenX > 304 && heroScreenX > 288 ||
             m_edgeWaitingHero == hero && targetScreenX is < 16 or > 304) &&
            !game.Actors.Any(a => a.IsEnemy && a.Health > 0 &&
                game.ProjectView(a.X, a.Z, a.Y).X is >= 48 and <= 272))
        {
            m_edgeWaitingHero = hero;
            var inward = heroScreenX < 48 ? 1 : heroScreenX > 272 ? -1 : 0;
            // Make room even when an arriving enemy is already within striking distance.
            // Hold the inner position until it follows us fully into the shot.
            if (inward == 0)
                return default;
            if (Enumerable.Range(1, 16).All(step =>
                    Math.Abs(game.Ground(hero.X + inward * step, hero.Z) - hero.Y) <= 8))
                return new GameInput(X: inward);
        }
        m_edgeWaitingHero = null;
        if (target == null && item == null && (game.IsCamp || !game.CanAdvance))
            return default;
        var tx = item?.X ?? target?.X ?? hero.X + 80;
        var tz = item?.Z ?? target?.Z ?? game.FindFloor(hero.X + 24, hero.Z);
        var dx = tx - hero.X;
        var dz = tz - hero.Z;
        if (game.AreaIndex == 2 && !game.IsCamp && hero == game.Hero && hero.IsFree &&
            hero.X is >= 4736 and <= 4912 && BridgeCrossing(game, hero, tx) is { } crossing)
            return crossing;
        if (target != null && item == null && Math.Abs(target.Y - hero.Y) > 24)
            return TerrainNavigator.Towards(game, hero, tx, tz);
        var targetInShot = target != null && game.ProjectView(target.X, target.Z, target.Y).X is >= 24 and <= 296;
        // A charge travels beyond its target: leave room to finish inside the shot.
        var chargeEnd = game.ProjectView(hero.X + Math.Sign(dx) * (Game.HeroBargeDistance + 4), hero.Z, hero.Y).X;
        if (item == null && targetInShot && hero == game.Hero && Math.Abs(hero.Vx) >= 3 && Math.Abs(dx) < 62 && Math.Abs(dz) < 5 &&
            chargeEnd is >= 0 and <= 320)
            return new GameInput(Math.Sign(dx), Attack: true, Run: true);
        if (item == null && target != null && Math.Abs(dx) < (hero.IsMount ? 28 : 12) && Math.Abs(dz) < 7)
        {
            var retreat = dx == 0 ? (hero.X > game.Progress + 160 ? -1 : 1) : -Math.Sign(dx);
            var retreatFloor = game.Ground(hero.X + retreat * 16, hero.Z);
            if (hero.X + retreat * 32 < game.Progress + 12 || hero.X + retreat * 32 > game.Progress + 305 ||
                retreatFloor < hero.Y - 12 || retreatFloor >= 5248)
                return hero.IsMount ? new GameInput(Dismount: true) : new GameInput(-retreat);
            return new GameInput(retreat);
        }
        if (item == null && target != null && hero == game.Hero && target.State == FighterState.Hurt &&
            target.Kind is ActorKind.Heninger or ActorKind.Longmoan && Math.Abs(dx) >= 22 && Math.Abs(dx) < 44 && Math.Abs(dz) < 5)
            return new GameInput(Math.Sign(dx), 0);
        if (item == null && target != null && Math.Abs(dx) < (hero.Kind == ActorKind.RedDragon ? 160 : hero.Kind == ActorKind.BlueDragon ? 80 : 43) && Math.Abs(dz) < 5)
        {
            if (hero == game.Hero && dx * hero.Facing < -12 && targetInShot)
                return new GameInput(Attack: true, Jump: true);
            return new GameInput(Math.Sign(dx), 0, Attack: true);
        }
        var x = Math.Abs(dx) > (item != null ? 3 : 32) ? Math.Sign(dx) : 0;
        var z = Math.Abs(dz) > 2 ? Math.Sign(dz) : 0;
        var ahead = game.Ground(hero.X + x * 16, hero.Z + z * 4);
        if (x != 0 && ahead >= 5248 && Enumerable.Range(3, 10).Any(step =>
                game.Ground(hero.X + x * step * 8, hero.Z) is > 3840 and < 5248 &&
                Math.Abs(game.Ground(hero.X + x * step * 8, hero.Z) - hero.Y) <= 32))
            return new GameInput(x, Jump: true, Run: true);
        if (x != 0 && (ahead < hero.Y - 12 || ahead >= 5248) ||
            z != 0 && (game.Ground(hero.X, hero.Z + z * 8) < hero.Y - 12 || game.Ground(hero.X, hero.Z + z * 8) >= 5248))
        {
            var route = TerrainNavigator.Towards(game, hero, tx, tz);
            return hero.IsMount && route == default ? new GameInput(Dismount: true) : route;
        }
        // Walk through cleared scenery. Sprint only for a deliberate approach to a distant visible opponent;
        // gap crossing above and an already committed barge keep their explicit running inputs.
        var run = hero == game.Hero && item == null && target != null && target.Id % 2 == 0 &&
            Math.Abs(dx) is > 90 and < 160 && Math.Abs(dz) < 5 &&
            game.ProjectView(target.X, target.Z, target.Y).X is >= 24 and <= 296 &&
            Math.Abs(game.Ground(hero.X + x * 32, hero.Z) - hero.Y) < 8;
        return new GameInput(x, z, Run: run);
    }

    private static GameInput? BridgeCrossing(Game game, Fighter hero, double targetX)
    {
        var direction = Math.Sign(targetX - hero.X);
        if (direction == 0)
            return null;
        // The break widens toward the front of the bridge. Probe this lane's actual banks.
        var edge = 1;
        while (edge <= 40 && game.Ground(hero.X + edge * direction, hero.Z) < 5248)
            edge++;
        if (edge > 40 || Math.Abs(targetX - hero.X) < edge)
            return null;
        var landing = hero.X + Game.HeroBargeDistance * direction;
        if (Math.Abs(game.Ground(landing, hero.Z) - hero.Y) > 12)
            return null;
        var speed = hero.Vx * direction;
        if (speed >= 3 && edge <= 24)
            return new GameInput(X: direction, Attack: true, Run: true);
        // From rest the six acceleration ticks consume 10.5 pixels before fire can start a barge.
        var ticks = Math.Max(0, Math.Ceiling((3 - Math.Max(0, speed)) * 2));
        var runup = ticks * Math.Max(0, speed) + ticks * (ticks + 1) / 4;
        return edge <= runup + 1 ? new GameInput(X: -direction) : new GameInput(X: direction, Run: true);
    }

    private GameInput? AvoidLightning(Game game, Fighter hero)
    {
        var jumpDistance = hero.IsMount ? 48 : 80;
        // The bolt homes in on depth as it travels, even when it starts in another lane.
        var bolt = game.Actors.FirstOrDefault(a => a.Kind == ActorKind.Lightning && !a.IsGone &&
            (hero.X - a.X) * a.Facing > -24 && (hero.X - a.X) * a.Facing < jumpDistance);
        var charge = game.Actors.FirstOrDefault(a => a.Kind == ActorKind.LightningCharge && !a.IsGone && a.Owner.Health > 0);
        var chargeTicks = charge == null ? int.MaxValue :
            charge.Animation.Current.Duration - charge.Animation.StepTicks +
            charge.Animation.Steps.Skip(charge.Animation.Index + 1).Sum(step => step.Duration);
        // On foot, jumping takes twelve crouch ticks; mounts take off immediately.
        // At close range the travelling bolt is already too late a warning for an unmounted hero.
        if (bolt != null || chargeTicks <= (hero.IsMount ? 6 : 20) && Math.Abs(charge.X - hero.X) < 100)
        {
            m_lightningJumpHero = hero;
            return new GameInput(Jump: true);
        }
        var caster = charge?.Owner ?? game.Actors.FirstOrDefault(a => a.Kind == ActorKind.DeathAdder && a.Health > 0 &&
            (a.State == FighterState.GettingUp || a.State == FighterState.Attacking && a.Animation.Address == Moves.DeathAdderCast));
        if (caster == null)
            return null;
        // Stop committing to attacks during the tell. Make room for the bolt and the jump startup.
        var direction = hero.X < caster.X ? -1 : 1;
        var nextX = hero.X + direction * 12;
        if (Math.Abs(hero.X - caster.X) < 112 && nextX >= game.Progress + 12 && nextX <= game.Progress + 305 &&
            Math.Abs(game.Ground(nextX, hero.Z) - hero.Y) <= 8)
            return new GameInput(X: direction);
        return default(GameInput);
    }

    private static bool ShouldCastEmergencyMagic(Game game, Fighter hero)
    {
        if (game.IsCamp || game.MagicTick >= 0 || game.Potions == 0 || !hero.IsFree || game.Hero.Health <= 0 || hero.Invulnerable > 12)
            return false;

        var imminentStrikes = 0;
        var enemyOnLeft = false;
        var enemyOnRight = false;
        foreach (var enemy in game.Actors)
        {
            var dx = enemy.X - hero.X;
            if (enemy.Mount != null || Math.Abs(dx) >= 65 || Math.Abs(enemy.Z - hero.Z) >= 12 ||
                Math.Abs(enemy.Y - hero.Y) >= 12 || enemy.State is not (FighterState.Idle or FighterState.Walking or FighterState.Attacking) ||
                !game.CanHitWithMagic(enemy.Rider ?? enemy))
                continue;

            enemyOnLeft |= dx < 0;
            enemyOnRight |= dx > 0;
            if (game.HasImminentStrikeAgainst(enemy, hero))
                imminentStrikes++;
        }

        // An active hit chain means the hero is already taking damage in this exchange.
        return imminentStrikes >= 2 || imminentStrikes == 1 && game.Hero.Hits > 0 && enemyOnLeft && enemyOnRight;
    }

    private bool ShouldCastMagic(Game game)
    {
        if (m_magicGame != game || game.IsCamp || game.MagicTick >= 0 || game.Potions == 0)
        {
            m_magicGame = game;
            m_magicWaitTick = null;
        }
        if (game.IsCamp || game.MagicTick >= 0 || game.Potions == 0 || !game.Hero.IsFree)
            return false;

        var enemies = game.Actors.Where(a => a.IsEnemy && a.Health > 0).ToArray();
        var targets = enemies.Where(a => game.CanHitWithMagic(a) &&
            game.ProjectView(a.X, a.Z, a.Y).X is >= 16 and <= 304).ToArray();
        if (targets.Length == 0)
        {
            m_magicWaitTick = null;
            return false;
        }
        if (game.Hero.Health <= 12 && targets.Any(a => game.HasImminentStrike(a) && Math.Abs(a.X - game.Hero.X) < 65 &&
                Math.Abs(a.Z - game.Hero.Z) < 12 && Math.Abs(a.Y - game.Hero.Y) < 12))
            return true;
        // The formation sequence can outlast a whole wave. Save the cast until it can hit Death Adder too.
        if (enemies.Any(a => a.Kind == ActorKind.DeathAdder && a.State == FighterState.Entering))
            return false;
        // Tyris needs five potions for level two; three still buys only her weakest spell.
        var worthwhile = game.Potions >= 2 && (game.Character.MagicKey == "tyris"
            ? game.Boss || game.Character.GetMagicLevel(game.Potions) >= 2 && targets.Length >= 3
            : targets.Length >= 2);
        if (!worthwhile)
        {
            m_magicWaitTick = null;
            return false;
        }
        m_magicWaitTick ??= game.TickCount;
        // Include approaching actors, skeleton eruptions, delayed door entrances and nearby scripted waves.
        // Keep fighting while waiting, and cap the delay so an unreachable straggler cannot hoard magic forever.
        var assembling = targets.Length < enemies.Length || game.HasUpcomingEnemies;
        return !assembling || game.TickCount - m_magicWaitTick.Value >= 120;
    }

    private static GameInput? AvoidCharge(Game game, Fighter hero)
    {
        var attacker = game.Actors.FirstOrDefault(a => a.IsEnemy &&
            (hero.X - a.X) * a.Facing is > -4 and < 200 && Math.Abs(hero.Z - a.Z) < 20 &&
            (a.State == FighterState.Walking && a.Animation.Address == a.Moves.Run && Math.Abs(a.Vx) > 0 ||
             a.State == FighterState.Dashing && a.StateTicks < 18 ||
             a.State is FighterState.Crouching or FighterState.Jumping && a.Animation.Address == a.Moves.JumpAttack));
        if (attacker == null)
            return null;

        var depth = hero.Z - attacker.Z;
        if (Math.Abs(depth) >= 9)
            return default(GameInput);

        var direction = depth > 0 ? 1 : -1;
        if (Math.Abs(game.Ground(hero.X, attacker.Z + direction * 10) - hero.Y) <= 8)
            return new GameInput(Z: direction);
        if (Math.Abs(game.Ground(hero.X, attacker.Z - direction * 10) - hero.Y) <= 8)
            return new GameInput(Z: -direction);
        return null;
    }

    private GameInput? AvoidCrowd(Game game, Fighter hero)
    {
        bool ClearLane(double targetZ)
        {
            var distance = targetZ - hero.Z;
            for (var step = 1; step <= Math.Ceiling(Math.Abs(distance)); step++)
            {
                var z = hero.Z + Math.Sign(distance) * step;
                var ground = game.Ground(hero.X, z);
                if (Math.Abs(ground - hero.Y) > 8 ||
                    game.ProjectView(hero.X, z, ground).Y is < 16 or > 208)
                    return false;
            }
            return true;
        }

        // Finish leaving the attack lane instead of changing targets halfway out.
        if (m_escapingHero == hero && Math.Abs(m_escapeZ - hero.Z) > 1 && ClearLane(m_escapeZ))
            return new GameInput(Z: Math.Sign(m_escapeZ - hero.Z));
        m_escapingHero = null;
        var threats = game.Actors.Where(a => (a.IsEnemy && a.Mount == null || a.Rider?.IsEnemy == true) && a.Health > 0 &&
            Math.Abs(a.X - hero.X) < 65 && Math.Abs(a.Z - hero.Z) < 8 && Math.Abs(a.Y - hero.Y) < 12 &&
            a.State is FighterState.Idle or FighterState.Walking or FighterState.Attacking).ToArray();
        var sandwiched = threats.Any(a => a.X < hero.X) && threats.Any(a => a.X > hero.X);
        if (!sandwiched && !threats.Any(a => (threats.Length >= 3 || a.Kind == ActorKind.DeathAdder) && game.HasImminentStrike(a)))
            return null;
        foreach (var direction in new[] { -1, 1 }.OrderBy(d => threats.Count(a => (a.Z - hero.Z) * d > 0)))
        {
            var targetZ = hero.Z + direction * 20;
            if (!ClearLane(targetZ))
                continue;
            m_escapingHero = hero;
            m_escapeZ = targetZ;
            return new GameInput(Z: direction);
        }
        return null;
    }
}
