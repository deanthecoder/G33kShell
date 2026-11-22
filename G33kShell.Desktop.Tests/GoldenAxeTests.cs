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
using System.IO;
using System.Linq;
using DTC.Core;
using G33kShell.Desktop.Console;
using G33kShell.Desktop.Console.Screensavers.GoldenAxe;
using NUnit.Framework;
using SkiaSharp;

namespace G33kShell.Desktop.Tests;

/// <summary>
/// Exercises a complete autonomous level and renders diagnostic frames.
/// </summary>
/// <remarks>
/// Uses the bundled arcade data to check terrain and complete autonomous runs.
/// </remarks>
[TestFixture]
public class GoldenAxeTests
{
    internal static Game ReachFirstCamp(ArcadeData data)
    {
        var game = new Game(data, random: new Random(0));
        var controller = new CodedController();
        while (game.TickCount < 18_000 && !game.Complete && !game.GameOver)
            game.Tick(controller.Read(game));
        game.StartCamp();
        Assert.That(game.IsCamp, Is.True);
        return game;
    }

    [Test]
    public void CampStaysPlayableAfterCollectingTheLastPotion()
    {
        var game = ReachFirstCamp(new ArcadeData(ArcadeData.AssetDirectory));
        var potion = game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, game.Hero.X, game.Hero.Z);
        // Keep the final drop out of reach until the thieves have left and the hero has woken.
        potion.X = game.Progress + 295;
        while (game.Camp.Ticks < 480)
        {
            game.Actors.RemoveAll(actor => actor.Kind == ActorKind.Thief);
            game.Tick(default);
        }

        Assert.That(game.Complete, Is.False);
        var potionsBefore = game.Potions;
        potion.X = game.Hero.X;
        potion.Z = game.Hero.Z;
        potion.Y = game.Hero.Y;
        game.Tick(default);
        Assert.That(game.Potions, Is.EqualTo(potionsBefore + 1));

        var fire = game.Actors.Single(actor => actor.Kind == ActorKind.Campfire);
        var fireFrame = fire.Animation.Index;
        var fireAnimated = false;
        for (var tick = 0; tick < CampRound.ClearHoldTicks - 1; tick++)
        {
            game.Tick(default);
            fireAnimated |= fire.Animation.Index != fireFrame;
        }
        Assert.That(game.Complete, Is.False);
        Assert.That(fireAnimated, Is.True);
        game.Tick(default);
        Assert.That(game.Complete, Is.True);
    }

    [Test]
    public void CampTimeoutLetsLoadedThievesRunOffBothEdges()
    {
        var game = ReachFirstCamp(new ArcadeData(ArcadeData.AssetDirectory));
        var fire = game.Actors.Single(actor => actor.Kind == ActorKind.Campfire);
        var left = game.Add(ActorKind.Thief, Moves.Thief, 0x3d, 99, fire.X - 34, fire.Z);
        var right = game.Add(ActorKind.Thief, Moves.Thief, 0x3c, 99, fire.X + 34, fire.Z);
        left.Items = right.Items = 3;
        right.FoodThief = true;
        var leftStart = left.X;
        var rightStart = right.X;

        while (game.Camp.Ticks < CampRound.TimeLimit - 1 && !game.Complete)
        {
            game.Actors.RemoveAll(actor => actor.Kind == ActorKind.Thief && actor != left && actor != right);
            left.X = leftStart;
            right.X = rightStart;
            game.Tick(default);
        }

        game.Tick(default);
        Assert.That(game.Camp.TimeExpired, Is.True);
        Assert.That(game.Complete, Is.False);
        Assert.That(game.Actors, Does.Contain(left).And.Contain(right));
        Assert.That(left.State, Is.EqualTo(FighterState.Fleeing));
        Assert.That(right.State, Is.EqualTo(FighterState.Fleeing));
        Assert.That(left.Animation.Current.Frame, Is.InRange(489, 493));
        Assert.That(right.Animation.Current.Frame, Is.InRange(489, 493));

        while (!game.Complete && game.Camp.Ticks < CampRound.TimeLimit + 250 + CampRound.ClearHoldTicks)
            game.Tick(default);

        Assert.That(game.Project(left.X, left.Z, left.Y).X, Is.LessThan(-60));
        Assert.That(game.Project(right.X, right.Z, right.Y).X, Is.GreaterThan(ArcadeData.Width + 60));
        Assert.That(left.IsGone && right.IsGone && game.Complete, Is.True);
    }

    [TestCase(4824, 1)]
    [TestCase(5208, -1)]
    public void PotionThiefJumpsOntoRockWithoutWalkingThroughIt(int startX, int facing)
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = new Game(data);
        var thief = new Fighter(1000, data, ActorKind.Thief, Moves.Thief, 0x3d, 99)
        {
            X = startX, Z = 4168, Facing = facing, Items = 3
        };
        thief.Y = game.Ground(thief.X, thief.Z);
        var originalHeight = thief.Y;
        game.Actors.Add(thief);
        var jumped = false;
        for (var tick = 0; tick < 60; tick++)
        {
            game.Tick(default);
            jumped |= thief.Vy < 0;
            Assert.That(thief.Y, Is.LessThanOrEqualTo(game.Ground(thief.X, thief.Z) + 12), "The thief must stay above the rock surface.");
        }
        Assert.That(jumped, Is.True);
        Assert.That((thief.X - startX) * facing, Is.GreaterThan(30));
        Assert.That(thief.Y, Is.EqualTo(originalHeight - 48));
        Assert.That(thief.Vy, Is.Zero);
    }

    [Test]
    public void CharacterChangePreservesHealthAndPositionAndWaitsForCurrentAttack()
    {
        if (!ArcadeData.IsAvailable)
            Assert.Ignore("Bundled Golden Axe data is missing.");
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = new Game(data);
        game.Hero.Health = 23;
        var x = game.Hero.X;
        var z = game.Hero.Z;
        game.Tick(new GameInput(Attack: true));
        game.SelectCharacter(2);
        game.Tick(default);
        Assert.That(game.Character.MagicKey, Is.EqualTo("ax"));
        for (var tick = 0; tick < 120 && game.Character.MagicKey != "gilius"; tick++)
            game.Tick(default);
        Assert.That(game.Character.MagicKey, Is.EqualTo("gilius"));
        Assert.That(game.Hero.Palette, Is.EqualTo(0x15));
        Assert.That(game.Hero.Health, Is.EqualTo(23));
        Assert.That(game.Hero.X, Is.EqualTo(x));
        Assert.That(game.Hero.Z, Is.EqualTo(z));
        Assert.That(game.Kills, Is.Zero);
        Assert.That(game.MagicEffects.Length, Is.GreaterThanOrEqualTo(game.Character.MagicLevels));
    }
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void CloseAttackKneesAndThrowsEnemyWithoutLeavingItHeld(int character)
    {
        if (!ArcadeData.IsAvailable)
            Assert.Ignore("Bundled Golden Axe data is missing.");
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = new Game(data, character);
        var hero = game.Hero;
        var enemy = new Fighter(1000, data, ActorKind.Heninger, Moves.Heninger, 0x18, 16)
        {
            X = hero.X + 16, Z = hero.Z, Y = hero.Y, Cooldown = 300
        };
        game.Actors.Add(enemy);
        game.Tick(new GameInput(1, Attack: true));
        Assert.That(enemy.State, Is.EqualTo(FighterState.Held));
        for (var tick = 0; tick < 300 && game.Throws == 0; tick++)
            game.Tick(default);
        Assert.That(game.Throws, Is.EqualTo(1));
        Assert.That(enemy.Health, Is.LessThan(16));
        Assert.That(enemy.State, Is.EqualTo(FighterState.Flying));
        Assert.That(enemy.HeldBy, Is.Null);
        Assert.That(hero.Victim, Is.Null);
    }
    [TestCase(0, 0)]
    [TestCase(1, 0)]
    [TestCase(2, 0)]
    [TestCase(0, 1)]
    [TestCase(1, 1)]
    [TestCase(2, 1)]
    [TestCase(0, 2)]
    [TestCase(1, 2)]
    [TestCase(2, 2)]
    [TestCase(0, 3)]
    [TestCase(1, 3)]
    [TestCase(2, 3)]
    [TestCase(0, 4)]
    [TestCase(1, 4)]
    [TestCase(2, 4)]
    public void CodedControllerCompletesArea(int character, int area)
    {
        if (!ArcadeData.IsAvailable)
            Assert.Ignore("Bundled Golden Axe data is missing.");
        var data = new ArcadeData(ArcadeData.AssetDirectory, area);
        var game = new Game(data, character, new Random(0));
        var controller = new CodedController();
        var renderer = new ArcadeRenderer(data);
        var screen = new G33kShell.Desktop.Console.PixelScreenData(320, 224, data.Palette);
        var sawDeathAdder = false;
        for (var tick = 0; tick < 60 * 300 && !game.Complete && !game.GameOver; tick++)
        {
            game.Tick(controller.Read(game));
            sawDeathAdder |= game.Actors.Any(a => a.Kind == ActorKind.DeathAdder);
            if (tick % 1800 == 0)
            {
                TestContext.Progress.WriteLine($"Tick {tick}: progress {game.Progress}, hero {game.Hero.X},{game.Hero.Z}, HP {game.Hero.Health}, kills {game.Kills}, throws {game.Throws}, state {game.Hero.State}");
                renderer.Draw(screen, game);
                using var bitmap = new SKBitmap(320, 224);
                bitmap.Pixels = screen.Pixels.Select(p => new SKColor(data.Palette[p].R, data.Palette[p].G, data.Palette[p].B)).ToArray();
                using var image = SKImage.FromBitmap(bitmap);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                using var file = File.Create(Path.Combine(TestContext.CurrentContext.WorkDirectory, $"goldenaxe-{area}-{character}-{tick}.png"));
                encoded.SaveTo(file);
            }
        }
        TestContext.Progress.WriteLine(string.Join("; ", game.Actors.Select(a => $"{a.Kind}:{a.X:F1},{a.Z:F1} {a.State} Y={a.Y} HP={a.Health} frame={a.Animation.Current.Frame}")));
        screen.SetPalette(G33kShell.Desktop.Console.PixelScreenData.CreateGreyscalePalette(data.Palette, Rgb.Black, new Rgb(100, 255, 140)));
        renderer.Draw(screen, game);
        using var mono = new SKBitmap(320, 224);
        mono.Pixels = screen.Pixels.Select(p => new SKColor(screen.Palette[p].R, screen.Palette[p].G, screen.Palette[p].B)).ToArray();
        using var monoImage = SKImage.FromBitmap(mono);
        using var monoData = monoImage.Encode(SKEncodedImageFormat.Png, 100);
        using var monoFile = File.Create(Path.Combine(TestContext.CurrentContext.WorkDirectory, $"goldenaxe-{area}-{character}-mono.png"));
        monoData.SaveTo(monoFile);
        Assert.That(game.Complete, Is.True, "The coded player must clear the final wave without stalling.");
        if (area == ArcadeData.AreaCount - 1)
            Assert.That(sawDeathAdder, Is.True, "The final area must include Death Adder before it can clear.");
        if (area == 0)
        for (var nextArea = 1; nextArea < ArcadeData.AreaCount; nextArea++)
        {
            TestContext.Progress.WriteLine($"Before camp {nextArea}: HP {game.Hero.Health}, lives {game.Lives}, potions {game.Potions}");
            game.StartCamp();
            Assert.That(game.IsCamp, Is.True);
            Assert.That(game.Complete, Is.False);
            for (var tick = 0; tick < 1400 + CampRound.ClearHoldTicks && !game.Complete; tick++)
            {
                game.Tick(controller.Read(game));
                if (tick is 0 or 150 or 450 or 900)
                    SaveSnapshot(data, game, $"goldenaxe-camp-{nextArea}-{character}-{tick}.png");
            }
            Assert.That(game.Complete, Is.True, "The camp must finish before the next area.");
            TestContext.Progress.WriteLine($"After camp {nextArea}: HP {game.Hero.Health}, lives {game.Lives}, potions {game.Potions}");
            data = new ArcadeData(ArcadeData.AssetDirectory, nextArea);
            var next = game.ContinueIn(data);
            Assert.That(next.Character, Is.SameAs(game.Character));
            Assert.That(next.Hero.Health, Is.EqualTo(game.Hero.Health));
            Assert.That(next.Lives, Is.EqualTo(game.Lives));
            Assert.That(next.Potions, Is.EqualTo(game.Potions));
            for (var tick = 0; tick < 60 * 300 && !next.Complete && !next.GameOver; tick++)
                next.Tick(controller.Read(next));
            TestContext.Progress.WriteLine($"Area {nextArea + 1}: complete {next.Complete}, HP {next.Hero.Health}, lives {next.Lives}, potions {next.Potions}, progress {next.Progress}");
            Assert.That(next.Complete, Is.True, $"The player must also clear area {nextArea + 1} with carried health and lives.");
            game = next;
        }
    }
    private static void SaveSnapshot(ArcadeData data, Game game, string name)
    {
        var screen = new G33kShell.Desktop.Console.PixelScreenData(320, 224, data.Palette);
        new ArcadeRenderer(data).Draw(screen, game);
        using var bitmap = new SKBitmap(320, 224);
        bitmap.Pixels = screen.Pixels.Select(p => new SKColor(data.Palette[p].R, data.Palette[p].G, data.Palette[p].B)).ToArray();
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(Path.Combine(TestContext.CurrentContext.WorkDirectory, name));
        encoded.SaveTo(file);
    }

}
