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
using G33kShell.Desktop.Skins;
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
    [Test]
    public void MagicFlickerFillsOnlyBriefDropoutsAtTheSamePosition()
    {
        int[] sprite = [643, 38, 0, 40, 0, -20];
        int[] moved = [643, 38, 0, 42, 0, -20];
        int[][][] recording = [[sprite, moved], [], [], [(int[])sprite.Clone()], [], []];

        Assert.That(ArcadeRenderer.DimMagicSprites(recording, 0), Is.Empty);
        Assert.That(ArcadeRenderer.DimMagicSprites(recording, 1).Single(), Is.SameAs(sprite));
        Assert.That(ArcadeRenderer.DimMagicSprites(recording, 2).Single(), Is.SameAs(sprite));
        Assert.That(ArcadeRenderer.DimMagicSprites(recording, 3), Is.Empty);
        Assert.That(ArcadeRenderer.DimMagicSprites(recording, 4), Is.Empty);
    }

    [TestCase("goldenaxe_play_color_fx", true, true)]
    [TestCase("goldenaxe_color_fx", false, true)]
    [TestCase("goldenaxe_color", false, false)]
    public void ContactShadowsAreOptInForBothControllers(string activationName, bool interactive, bool shadows)
    {
        var manager = new WindowManager(80, 25, new RetroMonoDos());
        var canvas = new GoldenAxeCanvas(80, 25) { ActivationName = activationName };
        canvas.OnLoaded(manager);
        canvas.Stop();
        try
        {
            var shellScreen = new ScreenData(80, 25);
            canvas.StartScreensaver(shellScreen);
            canvas.UpdateFrame(shellScreen);

            using var screenLock = manager.PixelScreen.Lock(out var frame);
            // Use a white palette to isolate coverage from the opening fade.
            frame.SetPalette(Enumerable.Repeat(Rgb.White, frame.Palette.Length).ToArray());
            var colors = new int[frame.Pixels.Length];
            CopyWithoutFog(frame, colors);
            Assert.That(colors.Any(IsGroundShadow), Is.EqualTo(shadows));
            Assert.That(canvas.IsInteractive, Is.EqualTo(interactive));
        }
        finally
        {
            canvas.StopScreensaver();
        }
    }

    [Test]
    public void GroundShadowsDarkenSceneryWhileSpritesCoverThem()
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = new Game(data);
        var renderer = new ArcadeRenderer(data);
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        frame.SetPalette(Enumerable.Repeat(Rgb.White, frame.Palette.Length).ToArray());

        game.Hero.SetState(FighterState.Gone);
        renderer.Draw(frame, game);
        var scenery = frame.Pixels.ToArray();

        game.Hero.SetState(FighterState.Idle);
        renderer.Draw(frame, game, effects: true);
        var colors = new int[frame.Pixels.Length];
        CopyWithoutFog(frame, colors);
        Assert.That(colors.Any(IsGroundShadow), Is.True);
        Assert.That(Enumerable.Range(0, colors.Length)
            .Where(i => frame.Pixels[i] != scenery[i])
            .All(i => colors[i] == -1), Is.True);

        renderer.Draw(frame, game);
        frame.CopyToBgra(colors);
        Assert.That(colors.All(color => color == -1), Is.True);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void DroppedItemShadowFadesAsTheItemRises(int item)
    {
        var kind = item switch { 0 => ActorKind.Potion, 1 => ActorKind.CampPotion, _ => ActorKind.Food };
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = new Game(data);
        var hero = game.Hero;
        hero.SetState(FighterState.Gone);
        var potion = game.Add(kind, Moves.Thief, kind == ActorKind.Food ? 0x7f : 0x4a, 1, hero.X, hero.Z);
        potion.Animation.Play(kind == ActorKind.Food ? 0x18722 : 0x1870a);
        var foot = game.Project(potion.X, potion.Z, potion.Y);
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, Enumerable.Repeat(Rgb.White, data.Palette.Length).ToArray());
        var renderer = new ArcadeRenderer(data);
        var colors = new int[frame.Pixels.Length];
        var sample = ((int)foot.Y + 1) * frame.Width + (int)foot.X;

        renderer.Draw(frame, game, effects: true);
        CopyWithoutFog(frame, colors);
        var grounded = colors[sample];
        Assert.That(IsGroundShadow(grounded), Is.True);
        if (kind != ActorKind.Food)
            Assert.That(IsGroundShadow(colors[sample + 9]), Is.True);

        potion.Y -= 28;
        renderer.Draw(frame, game, effects: true);
        CopyWithoutFog(frame, colors);
        Assert.That(colors[sample], Is.GreaterThan(grounded).And.LessThan(-1));

        renderer.Draw(frame, game);
        frame.CopyToBgra(colors);
        Assert.That(colors[sample], Is.EqualTo(-1));
    }

    [Test]
    public void HighKickChangesTheCastSilhouetteAndTheFarEndFades()
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = new Game(data);
        var hero = game.Hero;
        var foot = game.Project(hero.X, hero.Z, hero.Y);
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, Enumerable.Repeat(Rgb.White, data.Palette.Length).ToArray());
        var renderer = new ArcadeRenderer(data);
        var colors = new int[frame.Pixels.Length];
        var raisedLeg = ((int)foot.Y - 12) * frame.Width + (int)foot.X + 22;
        var farTip = ((int)foot.Y - 20) * frame.Width + (int)foot.X - 47;

        renderer.Draw(frame, game, effects: true);
        CopyWithoutFog(frame, colors);
        Assert.That(colors[raisedLeg], Is.EqualTo(-1));
        Assert.That(colors[farTip], Is.EqualTo(-1));

        hero.SetState(FighterState.Attacking, hero.Moves.Attacks[^1]);
        var kick = Array.FindIndex(hero.Animation.Steps, step => step.Frame == 31 && step.Duration > 3);
        Assert.That(kick, Is.GreaterThanOrEqualTo(0));
        hero.Animation.Seek(kick);
        for (var tick = 0; tick < 3; tick++)
            hero.Animation.Tick();
        renderer.Draw(frame, game, effects: true);
        CopyWithoutFog(frame, colors);
        Assert.That(IsGroundShadow(colors[raisedLeg]), Is.True);
        Assert.That(IsGroundShadow(colors[farTip]), Is.True);
        Assert.That(colors[farTip], Is.GreaterThan(colors[raisedLeg]));
    }

    [Test]
    public void KnockedDownEnemyCastsAWiderShadow()
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = new Game(data);
        var renderer = new ArcadeRenderer(data);
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        frame.SetPalette(Enumerable.Repeat(Rgb.White, frame.Palette.Length).ToArray());
        var colors = new int[frame.Pixels.Length];
        var hero = game.Hero;
        hero.SetState(FighterState.Gone);
        var enemy = game.Add(ActorKind.BadBrother, Moves.Brother, 0x38, 20, hero.X, hero.Z);

        renderer.Draw(frame, game, effects: true);
        var standingWidth = MeasureShadowWidth(frame, colors);

        enemy.SetState(FighterState.Flying, enemy.Moves.Fly);
        enemy.Y -= 1;
        enemy.Vy = 1;
        game.Tick(default);
        Assert.That(enemy.State, Is.EqualTo(FighterState.Down));
        Assert.That(enemy.Animation.Current, Is.EqualTo(data.Animations[enemy.Moves.GetUp][1]));
        Assert.That(game.LandingDust.Single().Heavy, Is.True);
        renderer.Draw(frame, game, effects: true);
        Assert.That(standingWidth, Is.Positive);
        Assert.That(MeasureShadowWidth(frame, colors), Is.GreaterThan(standingWidth));
    }

    [Test]
    public void WiderIdleSpriteCastsAWiderShadowOnTheSameGround()
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = new Game(data);
        var renderer = new ArcadeRenderer(data);
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        frame.SetPalette(Enumerable.Repeat(Rgb.White, frame.Palette.Length).ToArray());
        var colors = new int[frame.Pixels.Length];

        // Raise both actors so their sprites expose the whole shadow.
        var hero = game.Hero;
        hero.Y -= 24;
        renderer.Draw(frame, game, effects: true);
        var heroWidth = MeasureShadowWidth(frame, colors);

        hero.SetState(FighterState.Gone);
        var mount = game.Add(ActorKind.Chicken, Moves.Chicken, 0x23, 99, hero.X, hero.Z);
        mount.Y = hero.Y;
        renderer.Draw(frame, game, effects: true);
        Assert.That(heroWidth, Is.Positive);
        Assert.That(MeasureShadowWidth(frame, colors), Is.GreaterThan(heroWidth));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DefeatedEnemyShadowHugsTheProneBody(bool longmoan)
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = new Game(data);
        var hero = game.Hero;
        hero.SetState(FighterState.Gone);
        var enemy = game.Add(longmoan ? ActorKind.Longmoan : ActorKind.Heninger,
            longmoan ? Moves.Longmoan : Moves.Heninger, longmoan ? 0x78 : 0x18, 0, hero.X, hero.Z);
        enemy.SetState(FighterState.Dying, enemy.Moves.GetUp);
        enemy.Animation.Seek(1);

        // Draw on the hidden phase of the death blink to isolate the ground shadow.
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, Enumerable.Repeat(Rgb.White, data.Palette.Length).ToArray());
        new ArcadeRenderer(data).Draw(frame, game, effects: true);
        var colors = new int[frame.Pixels.Length];
        CopyWithoutFog(frame, colors);
        var point = game.Project(enemy.X, enemy.Z, enemy.Y);
        var x = (int)point.X;
        var y = (int)point.Y;
        var underBody = colors[(y - (longmoan ? 3 : 1)) * frame.Width + x];
        var belowBody = colors[(y + 5) * frame.Width + x];
        Assert.That(IsGroundShadow(underBody), Is.True);
        Assert.That(underBody, Is.LessThan(belowBody));
    }

    private static int MeasureShadowWidth(PixelScreenData frame, int[] colors)
    {
        CopyWithoutFog(frame, colors);
        return Enumerable.Range(0, colors.Length)
            .Where(i => IsGroundShadow(colors[i]))
            .Select(i => i % frame.Width)
            .Distinct()
            .Count();
    }

    // In these white-palette frames, ground shadows are lighter than translucent black outlines.
    private static bool IsGroundShadow(int color) => (color & 255) is >= 155 and < 255;

    private static void CopyWithoutFog(PixelScreenData frame, int[] colors)
    {
        frame.FogOpacity = 0;
        frame.CopyToBgra(colors);
    }

    [Test]
    public void CampfireFlickersAndSmokeRisesAboveItsLightInEffectsMode()
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = ReachFirstCamp(data);

        var fire = game.Actors.Single(actor => actor.Kind == ActorKind.Campfire);
        var foot = game.Project(fire.X, fire.Z, fire.Y);
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        frame.SetPalette(Enumerable.Repeat(new Rgb(40, 40, 40), frame.Palette.Length).ToArray());
        var renderer = new ArcadeRenderer(data);
        var colors = new int[frame.Pixels.Length];
        int GetPixel(double x, double y) => colors[(int)y * frame.Width + (int)x];

        renderer.Draw(frame, game, effects: true);
        CopyWithoutFog(frame, colors);
        var warm = GetPixel(foot.X, foot.Y + 10);
        Assert.That((warm >> 16) & 255, Is.GreaterThan((warm >> 8) & 255));
        var farX = foot.X > frame.Width / 2 ? 0 : frame.Width - 1;
        const int unlit = unchecked((int)0xff282828);
        Assert.That(GetPixel(farX, foot.Y + 10), Is.EqualTo(unlit));
        var plumeStart = (int)(foot.Y - 53) * frame.Width + (int)foot.X;
        Assert.That(colors.Skip(plumeStart).Take(40).All(color => color == unlit), Is.True);

        for (var tick = 0; tick < 4; tick++)
            game.Tick(default);
        renderer.Draw(frame, game, effects: true);
        CopyWithoutFog(frame, colors);
        Assert.That(GetPixel(foot.X, foot.Y + 10), Is.Not.EqualTo(warm));

        frame.SetPalette(Enumerable.Repeat(new Rgb(230, 230, 230), frame.Palette.Length).ToArray());
        frame.CopyToBgra(colors);
        Assert.That((GetPixel(foot.X, foot.Y + 10) >> 16) & 255, Is.InRange(231, 254));

        frame.SetPalette(Enumerable.Repeat(new Rgb(40, 40, 40), frame.Palette.Length).ToArray());
        renderer.Draw(frame, game);
        frame.CopyToBgra(colors);
        Assert.That(colors.All(color => color == unlit), Is.True);

        for (var tick = 0; tick < 100; tick++)
            game.Tick(default);
        renderer.Draw(frame, game, effects: true);
        CopyWithoutFog(frame, colors);
        var plume = colors.Skip(plumeStart).Take(40).ToArray();
        Assert.That(plume.Any(color => color != unlit), Is.True);

        for (var tick = 0; tick < 8; tick++)
            game.Tick(default);
        renderer.Draw(frame, game, effects: true);
        CopyWithoutFog(frame, colors);
        Assert.That(colors.Skip(plumeStart).Take(40), Is.Not.EqualTo(plume));
    }

    [Test]
    public void CampfireWoodStaysDarkBrownUnderTheFireGlow()
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = ReachFirstCamp(data);
        var fire = game.Actors.Single(actor => actor.Kind == ActorKind.Campfire);
        var point = game.Project(fire.X, fire.Z, fire.Y);
        var step = fire.Animation.Current;
        var sprite = data.Frames[step.Frame];
        var flip = ((step.Flags & 1) != 0) ^ fire.Facing < 0;
        var left = (int)Math.Round(point.X + step.Dx * fire.Facing + (flip ? -sprite.Ox - sprite.Width : sprite.Ox));
        var top = (int)Math.Round(point.Y + step.Dy + sprite.Oy);
        var wood = (top + sprite.Height - 2) * ArcadeData.Width + left + (flip ? sprite.Width - 11 : 10);
        var screen = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        var renderer = new ArcadeRenderer(data);
        var colors = new int[screen.Pixels.Length];

        renderer.Draw(screen, game);
        screen.CopyToBgra(colors);
        var originalRed = colors[wood] >> 16 & 255;

        renderer.Draw(screen, game, effects: true);
        screen.CopyToBgra(colors);
        var (red, green, blue) = (colors[wood] >> 16 & 255, colors[wood] >> 8 & 255, colors[wood] & 255);
        Assert.That(red, Is.LessThan(originalRed));
        Assert.That(green, Is.LessThan(red).And.GreaterThan(blue));
        Assert.That(blue, Is.Positive);
    }

    [Test]
    public void FighterInFrontOfCampfireOccludesSmokeAndEmbers()
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = ReachFirstCamp(data);
        for (var tick = 0; tick < 108; tick++)
            game.Tick(default);

        var hero = game.Hero;
        var fire = game.Actors.Single(actor => actor.Kind == ActorKind.Campfire);
        game.Actors.RemoveAll(actor => actor != hero && actor != fire);
        hero.X = fire.X + 6;
        hero.Z = fire.Z + 12;
        hero.Y = game.Ground(hero.X, hero.Z);
        hero.Invulnerable = 0;

        var renderer = new ArcadeRenderer(data);
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        var withSmoke = new int[frame.Pixels.Length];
        var withoutSmoke = new int[frame.Pixels.Length];
        (byte[] Pixels, bool[] Smoke) Render(bool showHero, bool showFire, bool effects = true)
        {
            hero.SetState(showHero ? FighterState.Idle : FighterState.Gone, showHero ? hero.Moves.Idle : 0);
            fire.SetState(showFire ? FighterState.Idle : FighterState.Gone);
            renderer.Draw(frame, game, effects);
            frame.CopyToBgra(withSmoke);
            frame.SmokeOpacity = 0;
            frame.CopyToBgra(withoutSmoke);
            return ((byte[])frame.Pixels.Clone(), withSmoke.Select((pixel, i) => pixel != withoutSmoke[i]).ToArray());
        }

        var background = Render(showHero: false, showFire: false);
        var fighter = Render(showHero: true, showFire: false);
        var fireOnly = Render(showHero: false, showFire: true);
        var fireClassic = Render(showHero: false, showFire: true, effects: false);
        var fighterInFront = Render(showHero: true, showFire: true);
        var fighterPixels = Enumerable.Range(0, frame.Pixels.Length)
            .Where(i => fighter.Pixels[i] != background.Pixels[i]).ToArray();
        var overlappingSmoke = fighterPixels.Where(i => fireOnly.Smoke[i]).ToArray();
        var overlappingEmbers = fighterPixels.Where(i => fireOnly.Pixels[i] != fireClassic.Pixels[i]).ToArray();

        Assert.That(overlappingSmoke, Is.Not.Empty);
        Assert.That(overlappingEmbers, Is.Not.Empty);
        Assert.That(overlappingSmoke.Any(i => fighterInFront.Smoke[i]), Is.False);
        Assert.That(overlappingEmbers.All(i => fighterInFront.Pixels[i] == fighter.Pixels[i]), Is.True);
    }

    [Test]
    public void GroundFogDriftsAndLeavesAThinnerBandNearTheCampfire()
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var forest = new Game(data);
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, [Rgb.Black]);
        var first = new int[frame.Pixels.Length];
        var colors = new int[first.Length];
        const int black = unchecked((int)0xff000000);

        GroundFog.Draw(frame, forest, data.CameraY);
        frame.CopyToBgra(first);
        Assert.That(first.Take(50 * frame.Width).All(color => color == black), Is.True);
        Assert.That(first.Max(), Is.GreaterThan(black));

        for (var tick = 0; tick < 60; tick++)
            forest.Tick(default);
        frame.Clear();
        GroundFog.Draw(frame, forest, data.CameraY);
        frame.CopyToBgra(colors);
        Assert.That(colors, Is.Not.EqualTo(first));

        var fogPixel = Array.FindIndex(colors, color => color != black);
        frame.SetPixel(fogPixel % frame.Width, fogPixel / frame.Width, 0);
        frame.CopyToBgra(colors);
        Assert.That(colors[fogPixel], Is.EqualTo(black), "Opaque sprites and foreground cover the mist.");

        var camp = ReachFirstCamp(data);
        var fire = camp.Actors.Single(actor => actor.Kind == ActorKind.Campfire);
        var foot = camp.Project(fire.X, fire.Z, fire.Y);
        frame.Clear();
        GroundFog.Draw(frame, camp, data.CameraY);
        frame.CopyToBgra(colors);
        Assert.That(colors.Max(), Is.InRange(black + 1, first.Max() - 1));
        var fogRow = ((int)foot.Y - 38) * frame.Width;
        Assert.That(colors[fogRow + (int)foot.X], Is.LessThan(colors.Skip(fogRow).Take(frame.Width).Max()));
    }

    [Test]
    public void CampfireShadowSwitchesSidesWhenFighterCrossesTheFire()
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = ReachFirstCamp(data);
        var fire = game.Actors.Single(actor => actor.Kind == ActorKind.Campfire);
        var foot = game.Project(fire.X, fire.Z, fire.Y);
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        frame.SetPalette(Enumerable.Repeat(new Rgb(40, 40, 40), frame.Palette.Length).ToArray());
        var renderer = new ArcadeRenderer(data);
        var colors = new int[frame.Pixels.Length];
        var row = ((int)foot.Y + 4) * frame.Width;
        var left = row + (int)foot.X - 44;
        var right = row + (int)foot.X + 44;

        game.Hero.SetState(FighterState.Gone);
        renderer.Draw(frame, game, effects: true);
        frame.CopyToBgra(colors);
        var litLeft = colors[left];
        var litRight = colors[right];

        game.Hero.SetState(FighterState.Idle, game.Hero.Moves.Idle);
        game.Hero.Z = fire.Z;
        void PutHeroNearFire(int distance)
        {
            game.Hero.X = fire.X + distance;
            game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
            renderer.Draw(frame, game, effects: true);
            frame.CopyToBgra(colors);
        }

        PutHeroNearFire(-20);
        Assert.That(colors[left], Is.LessThan(litLeft));
        var leftShadow = litLeft - colors[left];
        var rightSpill = litRight - colors[right];
        Assert.That(leftShadow, Is.GreaterThan(rightSpill));

        PutHeroNearFire(20);
        Assert.That(colors[right], Is.LessThan(litRight));
        Assert.That(litRight - colors[right], Is.GreaterThan(litLeft - colors[left]));
    }

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

    [TestCase(1)]
    [TestCase(-1)]
    public void MountedDragonBreathGlowsWhereItFaces(int facing)
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = new Game(data);
        var hero = game.Hero;
        var x = hero.X + 90;
        var z = game.FindFloor(x, hero.Z, keepVisible: true);
        var dragon = game.Add(ActorKind.BlueDragon, game.Character.DragonMoves, 0x43, 99, x, z);
        dragon.Facing = facing;
        dragon.Rider = hero;
        hero.Mount = dragon;
        game.Tick(new GameInput(Attack: true));
        for (var tick = 0; tick < 60 && !game.Actors.Any(a => a.Kind == ActorKind.Flame); tick++)
            game.Tick(default);
        var flame = game.Actors.Single(actor => actor.Kind == ActorKind.Flame);
        for (var tick = 0; tick < 12 && data.Frames[flame.Animation.Current.Frame].Width < 40; tick++)
            game.Tick(default);

        var point = game.Project(flame.X, flame.Z, flame.Y);
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        frame.SetPalette(Enumerable.Repeat(new Rgb(40, 40, 40), frame.Palette.Length).ToArray());
        var renderer = new ArcadeRenderer(data);
        var colors = new int[frame.Pixels.Length];
        var row = ((int)point.Y - 20) * frame.Width;
        var lit = row + (int)point.X + 75 * facing;
        var opposite = row + (int)point.X - 75 * facing;
        const int unlit = unchecked((int)0xff282828);

        renderer.Draw(frame, game, effects: true);
        CopyWithoutFog(frame, colors);
        Assert.That(colors[lit], Is.GreaterThan(unlit));
        Assert.That(colors[opposite], Is.EqualTo(unlit));

        renderer.Draw(frame, game);
        frame.CopyToBgra(colors);
        Assert.That(colors[lit], Is.EqualTo(unlit));
    }

    [Test]
    public void EffectsModeLetsSceneryShowThroughBlackSilhouetteEdges()
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = new Game(data, character: 2);
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        var renderer = new ArcadeRenderer(data);
        game.Hero.SetState(FighterState.Gone);
        renderer.Draw(frame, game);
        var scenery = frame.Pixels.ToArray();

        game.Hero.SetState(FighterState.Idle);
        renderer.Draw(frame, game);
        var classic = frame.Pixels.ToArray();

        renderer.Draw(frame, game, effects: true);
        var colors = new int[frame.Pixels.Length];
        frame.CopyToBgra(colors);
        var blackSpritePixels = Enumerable.Range(frame.Width, frame.Pixels.Length - 2 * frame.Width)
            .Where(i => classic[i] == data.Black && scenery[i] != data.Black).ToArray();
        bool HasSpritePixel(int i) => classic[i] != scenery[i];
        var inside = blackSpritePixels.Where(i =>
            HasSpritePixel(i - 1) && HasSpritePixel(i + 1) &&
            HasSpritePixel(i - frame.Width) && HasSpritePixel(i + frame.Width)).ToArray();

        Assert.That(blackSpritePixels.Any(i => frame.Pixels[i] == scenery[i] && (colors[i] & 0xffffff) != 0), Is.True);
        Assert.That(inside, Is.Not.Empty);
        Assert.That(inside.All(i => frame.Pixels[i] == data.Black), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InvulnerabilityBlendsTheFighterWithScenery(bool effects)
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = new Game(data);
        var hero = game.Hero;
        var renderer = new ArcadeRenderer(data);
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);

        int[] Render()
        {
            renderer.Draw(frame, game, effects);
            var colors = new int[frame.Pixels.Length];
            frame.CopyToBgra(colors);
            return colors;
        }

        var opaque = Render();
        hero.Invulnerable = 12;
        Assert.That(Render(), Is.EqualTo(opaque));
        hero.Invulnerable = 6;
        var translucent = Render();
        hero.SetState(FighterState.Gone);
        var scenery = Render();

        const int alpha = 147;
        static int MixChannel(int foreground, int background, int shift) =>
            (((foreground >> shift & 255) * alpha + (background >> shift & 255) * (255 - alpha)) / 255) << shift;

        var blendedPixels = Enumerable.Range(0, translucent.Length).Count(i =>
            translucent[i] != opaque[i] && translucent[i] != scenery[i] &&
            (translucent[i] & 0xffffff) == (MixChannel(opaque[i], scenery[i], 16) |
                MixChannel(opaque[i], scenery[i], 8) | MixChannel(opaque[i], scenery[i], 0)));
        Assert.That(blendedPixels, Is.GreaterThan(30));
    }

    [Test]
    public void EffectsModeFlashKeepsTheFightersPalette()
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = new Game(data);
        game.Hero.SetState(FighterState.Gone);
        var enemy = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, game.Hero.X, game.Hero.Z);
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        var renderer = new ArcadeRenderer(data);
        var colors = new int[frame.Pixels.Length];
        var before = new int[frame.Pixels.Length];
        renderer.Draw(frame, game, effects: true);
        var paletteIndices = frame.Pixels.ToArray();
        frame.CopyToBgra(before);

        enemy.Flash = 1;
        renderer.Draw(frame, game, effects: true);
        Assert.That(frame.Pixels, Is.EqualTo(paletteIndices));
        frame.CopyToBgra(colors);
        var changedColors = colors.Where((color, offset) => color != before[offset]).ToArray();
        Assert.That(changedColors, Is.Not.Empty);
        Assert.That(changedColors.All(color => color != -1), Is.True);

        renderer.Draw(frame, game);
        Assert.That(frame.Pixels, Is.Not.EqualTo(paletteIndices));
    }

    [Test]
    public void FireballHazeRefractsSceneryAboveTheProjectile()
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = new Game(data);
        game.Hero.SetState(FighterState.Gone);
        var fireball = game.Add(ActorKind.Fireball, Moves.BlueDragon, 0x45, 1, game.Hero.X + 40, game.Hero.Z);
        fireball.Y -= 28;
        fireball.Animation.Play(0x32b00);
        var ball = game.Project(fireball.X, fireball.Z, fireball.Y);
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        var renderer = new ArcadeRenderer(data);
        var colors = new int[frame.Pixels.Length];
        var before = new int[colors.Length];
        var firstRow = ((int)ball.Y - 35) * frame.Width;
        var rowLength = 9 * frame.Width;

        fireball.SetState(FighterState.Gone);
        renderer.Draw(frame, game, effects: true);
        frame.CopyToBgra(before);

        fireball.SetState(FighterState.Idle);
        renderer.Draw(frame, game, effects: true);
        frame.CopyToBgra(colors);
        Assert.That(colors.Skip(firstRow).Take(rowLength), Is.Not.EqualTo(before.Skip(firstRow).Take(rowLength)));

        fireball.SetState(FighterState.Dying, 0x32b90);
        fireball.Animation.Seek(1);
        renderer.Draw(frame, game, effects: true);
        frame.CopyToBgra(colors);
        Assert.That(colors.Skip(firstRow).Take(rowLength), Is.EqualTo(before.Skip(firstRow).Take(rowLength)));
    }

    [Test]
    public void FireballGlowReplacesPaintedShadowAndFadesAtItsNewPosition()
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory);
        var game = new Game(data);
        var hero = game.Hero;
        hero.SetState(FighterState.Gone);
        var fireball = game.Add(ActorKind.Fireball, Moves.BlueDragon, 0x45, 1, hero.X, hero.Z);
        fireball.Y -= 28;
        fireball.Animation.Play(0x32b00);

        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        frame.SetPalette(Enumerable.Repeat(new Rgb(40, 40, 40), frame.Palette.Length).ToArray());
        var renderer = new ArcadeRenderer(data);
        var colors = new int[frame.Pixels.Length];
        var ground = game.Project(fireball.X, fireball.Z, game.Ground(fireball.X, fireball.Z));
        var start = (int)(ground.Y + 9) * frame.Width + (int)ground.X;
        const int travel = 84;
        int GroundRed(int offset) => (colors[offset] >> 16) & 255;

        renderer.Draw(frame, game);
        var classic = frame.Pixels.ToArray();
        renderer.Draw(frame, game, effects: true);
        CopyWithoutFog(frame, colors);
        Assert.That(frame.Pixels, Is.Not.EqualTo(classic), "The classic frame includes the painted projectile shadow.");
        Assert.That(GroundRed(start), Is.GreaterThan(40));

        fireball.X += travel;
        renderer.Draw(frame, game, effects: true);
        CopyWithoutFog(frame, colors);
        Assert.That(GroundRed(start), Is.EqualTo(40));
        Assert.That(GroundRed(start + travel), Is.GreaterThan(40));

        fireball.SetState(FighterState.Dying, 0x32b90);
        fireball.Animation.Seek(1);
        renderer.Draw(frame, game, effects: true);
        CopyWithoutFog(frame, colors);
        var impact = GroundRed(start + travel);
        fireball.StateTicks = Game.FireballImpactTicks / 2;
        renderer.Draw(frame, game, effects: true);
        CopyWithoutFog(frame, colors);
        Assert.That(GroundRed(start + travel), Is.InRange(41, impact - 1));

        fireball.SetState(FighterState.Gone);
        renderer.Draw(frame, game, effects: true);
        CopyWithoutFog(frame, colors);
        Assert.That(GroundRed(start + travel), Is.EqualTo(40));
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
