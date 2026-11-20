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
    public GameInput Read(Game game)
    {
        var hero = game.Hero.Mount ?? game.Hero;
        var needsFood = game.Hero.Health < 48;
        var needsMagic = game.Potions < game.Character.MagicLevels;
        if (!game.IsCamp && game.Boss && !game.Actors.Any(a => a.IsEnemy && a.Health > 0))
            return default;
        if (hero.IsFree && game.Actors.Any(a => a.Kind == ActorKind.Lightning && !a.IsGone &&
            (hero.X - a.X) * a.Facing is > 0 and < 80 && Math.Abs(a.Z - hero.Z) < 14))
            return new GameInput(Jump: true);
        var target = game.Actors.Where(a => (a.IsEnemy || a.Rider?.IsEnemy == true || a.Kind == ActorKind.Thief && a.Items > 0) && a.Vulnerable)
            .Where(a => !game.IsCamp || game.Project(a.X, a.Z, a.Y).X is >= 24 and <= 296)
            .Where(a => !game.IsCamp || a.Kind != ActorKind.Thief || (a.FoodThief ? needsFood : needsMagic))
            .OrderBy(a => game.IsCamp && needsFood && a.FoodThief ? 0 : 1)
            .ThenBy(a => Math.Abs(a.X - hero.X) + Math.Abs(a.Z - hero.Z) * 3).FirstOrDefault();
        if (hero.State == FighterState.Jumping)
        {
            if (Math.Abs(hero.Vx) < 2 && game.Ground(hero.X + hero.Facing * 16, hero.Z) >= 5248)
                return default;
            return new GameInput(hero.Facing, Attack: target != null && Math.Abs(target.X - hero.X) < 55);
        }
        if (!game.IsCamp && game.Potions >= 2 && game.Actors.Count(a => a.IsEnemy && a.Health > 0) >= 2 && hero == game.Hero)
            return new GameInput(Magic: true);
        var item = game.Actors.Where(a => !a.IsGone && a.Kind is ActorKind.Potion or ActorKind.Food)
            .Where(a => game.IsCamp
                ? game.Project(a.X, a.Z, a.Y).X is >= 12 and <= 305 && (a.Kind == ActorKind.Food ? needsFood : needsMagic)
                : Math.Abs(a.X - hero.X) < 100)
            .OrderBy(a => a.Kind == ActorKind.Food && needsFood ? 0 : 1)
            .ThenBy(a => Math.Abs(a.X - hero.X) + Math.Abs(a.Z - hero.Z) * 3).FirstOrDefault();
        if (game.IsCamp && needsFood && target?.FoodThief == true && item?.Kind == ActorKind.Potion)
            item = null;
        if (target == null && item == null && (game.IsCamp || !game.CanAdvance && hero.X >= game.Progress + 296))
            return default;
        var tx = item?.X ?? target?.X ?? hero.X + 80;
        var tz = item?.Z ?? target?.Z ?? game.FindFloor(hero.X + 24, hero.Z);
        var dx = tx - hero.X;
        var dz = tz - hero.Z;
        if (target != null && item == null && Math.Abs(target.Y - hero.Y) > 24)
            return TerrainNavigator.Towards(game, hero, tx, tz);
        if (item == null && target != null && hero == game.Hero && Math.Abs(hero.Vx) >= 3 && Math.Abs(dx) < 62 && Math.Abs(dz) < 5)
            return new GameInput(Math.Sign(dx), Attack: true, Run: true);
        if (item == null && target != null && Math.Abs(dx) < (hero.IsMount ? 28 : 12) && Math.Abs(dz) < 7)
        {
            var retreat = dx == 0 ? (hero.X > game.Progress + 160 ? -1 : 1) : -Math.Sign(dx);
            var retreatFloor = game.Ground(hero.X + retreat * 16, hero.Z);
            if (hero.X + retreat * 32 < game.Progress + 12 || hero.X + retreat * 32 > game.Progress + 305 ||
                retreatFloor < hero.Y - 12 || retreatFloor >= 5248)
                return hero.IsMount ? new GameInput(Jump: true) : new GameInput(-retreat);
            return new GameInput(retreat);
        }
        if (item == null && target != null && hero == game.Hero && target.State == FighterState.Hurt &&
            target.Kind is ActorKind.Heninger or ActorKind.Longmoan && Math.Abs(dx) >= 22 && Math.Abs(dx) < 44 && Math.Abs(dz) < 5)
            return new GameInput(Math.Sign(dx), 0);
        if (item == null && target != null && Math.Abs(dx) < (hero.Kind == ActorKind.RedDragon ? 160 : hero.Kind == ActorKind.BlueDragon ? 80 : 43) && Math.Abs(dz) < 5)
        {
            if (hero == game.Hero && dx * hero.Facing < -12)
                return new GameInput(Attack: true, Jump: true);
            return new GameInput(Math.Sign(dx), 0, Attack: true);
        }
        var x = Math.Abs(dx) > (item != null ? 3 : 32) ? Math.Sign(dx) : 0;
        var z = Math.Abs(dz) > 2 ? Math.Sign(dz) : 0;
        var ahead = game.Ground(hero.X + x * 16, hero.Z + z * 4);
        if (x != 0 && ahead >= 5248 && Enumerable.Range(3, 10).Any(step =>
                game.Ground(hero.X + x * step * 8, hero.Z) is > 3840 and < 5248 &&
                Math.Abs(game.Ground(hero.X + x * step * 8, hero.Z) - hero.Y) <= 32))
            return hero.IsMount ? new GameInput(Jump: true) : new GameInput(x, Jump: true, Run: true);
        if (x != 0 && (ahead < hero.Y - 12 || ahead >= 5248) ||
            z != 0 && (game.Ground(hero.X, hero.Z + z * 8) < hero.Y - 12 || game.Ground(hero.X, hero.Z + z * 8) >= 5248))
        {
            var route = TerrainNavigator.Towards(game, hero, tx, tz);
            // A beast cannot jump ledges; continue on foot if it has no walking route.
            return hero.IsMount && route == default ? new GameInput(Jump: true) : route;
        }
        var run = hero == game.Hero && (target == null || target.Id % 2 == 0) && Math.Abs(dx) > 55 && Math.Abs(dz) < 5 &&
            Math.Abs(game.Ground(hero.X + x * 32, hero.Z) - hero.Y) < 8;
        return new GameInput(x, z, Run: run);
    }
}
