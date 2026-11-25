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
using System.Collections.Generic;
using System.Linq;
using DTC.Core;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Draws arcade tiles, priority layers and anchored sprites into the indexed pixel surface.
/// </summary>
/// <remarks>
/// All graphics use the original 320 by 224 coordinate space and a stable reduced palette.
/// </remarks>
internal sealed class ArcadeRenderer
{
    public static readonly Rgb FireLightColor = new(255, 148, 55);
    public static readonly Rgb CampSmokeColor = new(157, 159, 164);
    private static readonly byte[] s_outputColorCurve = Enumerable.Range(0, 256)
        .Select(value => (byte)Math.Round(255 * Math.Pow(value / 255.0, 1.5))).ToArray();
    private readonly ArcadeData m_data;
    private readonly PoseFootprint?[] m_footprints;
    private readonly SunShadow[,] m_sunShadows;
    private readonly byte m_orange, m_darkRed, m_filledRed, m_hudRed, m_hudPink, m_blue, m_lightBlue, m_yellow, m_livesYellow;
    private readonly byte m_emberCore;
    private readonly byte[] m_campfireWood;
    private readonly byte[] m_redTint, m_whiteTint;
    private TerrainShading m_terrainShading;
    internal const int MagicMeterTop = 12;
    private const byte MinimumMagicOpacity = 64;
    private const double ShadowSkew = 0.55;
    private const double ShadowSquash = 0.28;

    internal Action<string> ProfileStage { get; set; }

    private enum EllipseEffect { Fire, Smoke, Magic, Dust }

    public ArcadeRenderer(ArcadeData data)
    {
        m_data = data;
        m_footprints = new PoseFootprint?[data.Frames.Length];
        m_sunShadows = new SunShadow[data.Frames.Length, 2];
        m_orange = Color(255, 136, 0);
        m_darkRed = Color(160, 0, 0);
        m_filledRed = Color(255, 32, 32);
        m_hudRed = Color(255, 0, 0);
        m_hudPink = Color(255, 165, 189);
        m_redTint = Enumerable.Repeat(m_hudRed, 16).ToArray();
        m_whiteTint = Enumerable.Repeat(data.White, 16).ToArray();
        m_blue = Color(34, 34, 255);
        m_lightBlue = Color(120, 160, 255);
        m_yellow = Color(255, 255, 0);
        m_livesYellow = Color(255, 203, 0);
        m_emberCore = Color(255, 202, 94);
        m_campfireWood = [Color(53, 31, 18), Color(79, 46, 24), Color(110, 64, 31)];
    }

    public void Draw(PixelScreenData screen, Game game, bool effects = true, Rgb tint = null, byte effectOpacity = 255,
        bool showTerrainShade = false)
    {
        ProfileStage?.Invoke("begin");
        screen.Clear(m_data.Black);
        if (game.AreaIndex == 3 && !game.IsCamp)
        {
            var black = screen.Palette[m_data.Black];
            var white = screen.Palette[m_data.White];
            for (var pen = 0; pen < 8; pen++)
            {
                var color = EagleSky.Color(EagleSky.Blue[pen], m_data.TileColors[m_data.BossPalette][114 * 8 + pen], game.Progress);
                // Keep scene fades and monochrome rendering, without changing reusable asset palettes.
                if (tint != null)
                {
                    var brightness = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255;
                    color = new Rgb((byte)(black.R + (white.R - black.R) * brightness),
                        (byte)(black.G + (white.G - black.G) * brightness), (byte)(black.B + (white.B - black.B) * brightness));
                }
                else
                    color = new Rgb((byte)(color.R * white.R / 255), (byte)(color.G * white.G / 255), (byte)(color.B * white.B / 255));
                screen.Palette[EagleSky.PaletteStart + pen] = color;
            }
        }
        var u = -7 - game.ViewCamera.X;
        var v = (int)Math.Round(-512 - game.ViewCamera.Y);
        var palette = m_data.TilePalettes[game.IsCamp ? "0" : game.Boss ? m_data.BossPalette : m_data.DayPalette];
        var sunset = game.AreaIndex == 0 && !game.IsCamp && game.Boss
            ? Math.Clamp((game.BossPaletteTick < 0 ? 0 : game.TickCount - game.BossPaletteTick) / 150.0, 0, 1) : 1;
        var previousPalette = sunset < 1 ? m_data.TilePalettes[m_data.DayPalette] : null;
        var sunsetOpacity = (byte)Math.Round(sunset * 255);
        var backgroundX = (int)Math.Floor(u * m_data.Parallax[0][0] + m_data.Parallax[0][1]);
        var backgroundY = (int)(v * m_data.Parallax[1][0] + m_data.Parallax[1][1]);
        if (m_data.Sky != null && !game.IsCamp)
        {
            var scroll = EagleSky.Scroll(game.EagleSkyTick, m_data.SkyScrollX);
            Layer(screen, m_data.Sky, scroll.X, scroll.Y, palette, false, true, wrap: true);
            Layer(screen, m_data.Background, backgroundX, backgroundY, palette, false, true, skipSky: true);
        }
        else
            Layer(screen, m_data.Background, backgroundX, backgroundY, palette, false, true, previousPalette: previousPalette, opacity: sunsetOpacity);
        Layer(screen, m_data.Foreground, (int)u, v, palette, false, false, previousPalette: previousPalette, opacity: sunsetOpacity);
        if (SceneryEyes.Sample(game.AreaIndex, game.IsCamp, game.TickCount) is { } eye)
        {
            var frame = m_data.Frames[eye.Frame];
            Sprite(screen, eye.Frame, eye.Palette, eye.X - (int)u - frame.Ox, eye.Y - v - frame.Oy, false);
        }
        ProfileStage?.Invoke("scenery");
        if (showTerrainShade || effects && !game.IsCamp && effectOpacity != 0)
        {
            m_terrainShading ??= new TerrainShading(m_data);
            m_terrainShading.Draw(screen, (int)u - 1, v + 512, backgroundX, backgroundY, palette, effectOpacity,
                showTerrainShade, game.IsCamp);
            if (showTerrainShade)
                return;
        }
        ProfileStage?.Invoke("terrain shade");
        byte[] dustTints = [];
        if (effects)
        {
            screen.LightColor = ScaleLight(tint ?? FireLightColor, effectOpacity);
            screen.SmokeColor = tint ?? CampSmokeColor;
            screen.SmokeOpacity = effectOpacity;
            dustTints = SampleLandingDustTints(screen, game);
            DrawFireballHeatHaze(screen, game);
            GroundFog.Draw(screen, game, m_data.CameraY, tint, effectOpacity);
            DrawContactShadows(screen, game);
            DrawMagicGroundGlow(screen, game, tint, effectOpacity);
            if (game.IsCamp)
                DrawCampfireShadows(screen, game);
        }
        ProfileStage?.Invoke("atmosphere and shadows");
        foreach (var mound in game.SkeletonMounds)
        {
            var point = game.ProjectView(mound.X, mound.Z, game.Ground(mound.X, mound.Z));
            Sprite(screen, SkeletonEmergence.MoundFrame, SkeletonEmergence.GroundPalette, point.X, point.Y, false, keepTerrainShade: true);
        }
        foreach (var mound in game.OpenedSkeletonMounds)
        {
            var point = game.ProjectView(mound.X, mound.Z, game.Ground(mound.X, mound.Z));
            Sprite(screen, SkeletonEmergence.OpenFrame, SkeletonEmergence.GroundPalette, point.X, point.Y, false, keepTerrainShade: true);
        }
        DrawFloorLightningTrail(screen, game);
        if (effects)
            DrawFloorLightningSmoke(screen, game);
        // Flames cover fresh smoke; the lingering plume is revealed as they move away.
        if (effects)
            DrawDragonSmoke(screen, game);
        ProfileStage?.Invoke("ground effects and smoke");
        foreach (var actor in game.Actors.Concat(game.StoneBodies).Concat(game.FadingPickups).OrderBy(a => a.Z).ThenByDescending(a => a.Y))
        {
            if (actor.Mount != null || actor.State == FighterState.Gone && actor.PickupTick < 0 || ActorFade(game, actor) == 0 ||
                actor.State == FighterState.Dying && actor.Kind is not (ActorKind.Fireball or ActorKind.Flame) && game.TickCount % 4 < 2)
                continue;
            if (actor.State == FighterState.Entering && actor.StateTicks < Game.BrotherDoorClosingTick)
            {
                var door = game.ProjectView(actor.X - 1, actor.Z, actor.Y);
                var open = actor.StateTicks is >= Game.BrotherDoorOpeningTicks and < 24;
                Sprite(screen, open ? Game.BrotherDoorOpenFrame : Game.BrotherDoorAjarFrame,
                    Game.BrotherDoorPalette, door.X, door.Y + (open ? 2 : -3), false);
                if (actor.StateTicks < Game.BrotherDoorOpeningTicks)
                    continue;
            }
            if (actor.State == FighterState.Emerging)
            {
                var ground = game.ProjectView(actor.X, actor.Z, actor.Y);
                Sprite(screen, SkeletonEmergence.GroundFrame(actor.StateTicks), SkeletonEmergence.GroundPalette,
                    ground.X, ground.Y, false, keepTerrainShade: true);
            }
            var step = actor.Animation.Current;
            var flip = IsFlipped(actor, step);
            if (!effects && actor.Kind == ActorKind.Fireball && actor.State != FighterState.Dying)
            {
                var shadow = game.ProjectView(actor.X, actor.Z + 8, game.Ground(actor.X, actor.Z));
                Sprite(screen, step.Frame, 0, shadow.X + step.Dx * actor.Facing, shadow.Y, flip);
            }
            var point = game.ProjectView(actor.X, actor.Z, actor.Y);
            var pulse = actor.Invulnerable % 12;
            var opacity = actor.PickupTick >= 0
                ? (byte)(255 * Math.Clamp(1 - (game.TickCount - actor.PickupTick) / (double)Game.PickupFadeTicks, 0, 1))
                : (byte)(255 - 18 * Math.Min(pulse, 12 - pulse));
            opacity = (byte)(opacity * ActorFade(game, actor));
            var translucent = opacity < 255;
            if (translucent)
                screen.BeginTranslucentSprite(opacity);
            var lightningTint = LightningCage.SampleTick(game) >= 66 && game.MagicTargets.Contains(actor);
            Sprite(screen, step.Frame, actor.Palette,
                point.X + step.Dx * actor.Facing, point.Y + step.Dy, flip,
                flash: actor.Flash > 0 && !lightningTint, softFlash: effects, softenOutline: effects && IsCharacter(actor),
                campfireWood: effects && actor.Kind == ActorKind.Campfire);
            if (lightningTint)
                DrawLightningTint(screen, game, step.Frame, actor.Palette, point.X + step.Dx * actor.Facing, point.Y + step.Dy, flip);
            if (actor.State == FighterState.Petrified)
            {
                var stoneOpacity = (byte)(255 * Math.Clamp((game.TickCount - actor.PetrifiedTick) / 120.0, 0, 1));
                if (stoneOpacity > 0)
                {
                    screen.BeginTranslucentSprite(stoneOpacity);
                    Sprite(screen, step.Frame, actor.Palette, point.X + step.Dx * actor.Facing, point.Y + step.Dy,
                        flip, paletteOverride: m_data.SpritePalette[actor.Kind == ActorKind.Skeleton ? 0x72 : 0x97]);
                    screen.EndTranslucentSprite();
                }
            }
            if (WeaponSwipe.Sample(actor, m_data) is { } swipe)
                Sprite(screen, swipe.Frame, swipe.Palette, point.X + swipe.Dx, point.Y + swipe.Dy, swipe.Flip);
            if (actor.Rider != null && m_data.Riders.TryGetValue(actor.Animation.Address, out var riders))
            {
                var rider = riders[Math.Min(actor.Animation.Index, riders.Length - 1)];
                // Rider offsets are relative to the displayed beast, including its current pose offset.
                Sprite(screen, rider[0], actor.Rider.Palette,
                    point.X + (step.Dx + rider[2]) * actor.Facing, point.Y + step.Dy + rider[3],
                    (rider[1] != 0) ^ actor.Facing < 0, softenOutline: effects);
                if (LightningCage.SampleTick(game) >= 66 && game.MagicTargets.Contains(actor.Rider))
                    DrawLightningTint(screen, game, rider[0], actor.Rider.Palette,
                        point.X + (step.Dx + rider[2]) * actor.Facing, point.Y + step.Dy + rider[3],
                        (rider[1] != 0) ^ actor.Facing < 0);
            }
            if (translucent)
                screen.EndTranslucentSprite();
            if (effects && game.MagicTick < 0 && actor.PickupTick < 0 && actor.Kind is ActorKind.Potion or ActorKind.CampPotion)
            {
                var bottle = m_data.Frames[step.Frame];
                PotionSparkles.DrawIdle(screen, point.X, point.Y + step.Dy + bottle.Oy + bottle.Height * 0.25,
                    actor.Age, actor.Id, m_lightBlue, m_data.White, effectOpacity);
            }
            if (effects && actor.Kind == ActorKind.Campfire)
            {
                DrawCampfireSmoke(screen, point.X, point.Y, game.TickCount);
                DrawCampfireEmbers(screen, point.X, point.Y, game.TickCount);
            }
        }
        ProfileStage?.Invoke("actors");
        if (game.MagicTick >= 0 && game.MagicTick < game.ActiveMagicEffects.Length)
            DrawMagic(screen, game, effects, tint, effectOpacity);
        ProfileStage?.Invoke("magic");
        if (effects)
        {
            DrawFireLighting(screen, game);
            if (game.MagicTick < 0)
                DrawFloorLightningGlow(screen, game, tint, effectOpacity);
            DrawLandingDust(screen, game, dustTints);
            if (game.MagicTick < 0)
                PotionSparkles.DrawBursts(screen, game, m_lightBlue, m_data.White, effectOpacity);
        }
        ProfileStage?.Invoke("lighting and dust");
        Layer(screen, m_data.Foreground, (int)u, v, palette, true, false, previousPalette: previousPalette, opacity: sunsetOpacity);
        ProfileStage?.Invoke("priority scenery");
        DrawSurf(screen, game);
        ProfileStage?.Invoke("surf");
        var shake = MagicScreenShake.Sample(game.MagicTick - game.MagicShakeStart,
            game.MagicDuration - game.MagicShakeStart, game.MagicLevel);
        if (shake != default)
            screen.OffsetScene(shake.X, shake.Y);
        ProfileStage?.Invoke("screen shake");
        DrawHud(screen, game);
        ProfileStage?.Invoke("HUD");
        if (effects && game.MagicTick < 0)
            PotionSparkles.DrawTrails(screen, game, m_lightBlue, m_data.White, effectOpacity);
        if (game.CanAdvance && game.MagicTick < 0 && game.TickCount % 60 < 30)
            DrawGoPrompt(screen);
        if (game.GameOver || game.Complete && !game.IsCamp)
        {
            var message = game.GameOver ? "GAME OVER" : game.AreaIndex == ArcadeData.AreaCount - 1 ? "YOU WIN" : "STAGE CLEAR";
            var fade = Math.Clamp(game.EndStateTicks / 30.0, 0, 1);
            screen.BeginTranslucentSprite((byte)Math.Round(255 * fade));
            Text(screen, message, (ArcadeData.Width - message.Length * 8) / 2, 96);
            screen.EndTranslucentSprite();
        }
        DrawFeathers(screen, game);
        // Apply the approved contrast after lighting, translucency, screen shake and HUD composition.
        screen.OutputColorCurve = s_outputColorCurve;
        ProfileStage?.Invoke("final overlays");
    }

    private void DrawFloorLightningTrail(PixelScreenData screen, Game game)
    {
        foreach (var mark in game.LightningTrail)
        {
            var age = game.TickCount - mark.Tick;
            if (age >= 28)
                continue;
            var point = game.ProjectView(mark.X, mark.Z, mark.Y);
            screen.BeginTranslucentSprite((byte)(255 - age * 9));
            Sprite(screen, 575 + age / 4, Game.LightningTrailPalette, point.X, point.Y, mark.Facing > 0);
            screen.EndTranslucentSprite();
        }
    }

    private void DrawFloorLightningGlow(PixelScreenData screen, Game game, Rgb tint, byte opacity)
    {
        if (game.LightningTrail.Count == 0 && !game.Actors.Any(a => a.Kind is ActorKind.Lightning or ActorKind.LightningCharge))
            return;

        screen.MagicLightColor = ScaleLight(tint ?? new Rgb(114, 175, 255), opacity);
        foreach (var mark in game.LightningTrail)
        {
            var fade = 1 - (game.TickCount - mark.Tick) / (double)Game.LightningTrailTicks;
            var point = game.ProjectView(mark.X, mark.Z, mark.Y);
            DrawSoftEllipse(screen, point.X, point.Y - 2, 21, 7, 104 * fade, EllipseEffect.Magic);
            DrawSoftEllipse(screen, point.X, point.Y - 2, 18, 2.5, 145 * fade, EllipseEffect.Magic);
        }
        foreach (var bolt in game.Actors.Where(a => a.Kind is ActorKind.Lightning or ActorKind.LightningCharge))
        {
            var pose = bolt.Animation.Current;
            var point = game.ProjectView(bolt.X, bolt.Z, bolt.Y);
            var frame = m_data.Frames[pose.Frame];
            var offsetX = (frame.Ox + frame.Width / 2.0) * (IsFlipped(bolt, pose) ? -1 : 1);
            DrawSoftEllipse(screen, point.X + pose.Dx * bolt.Facing + offsetX,
                point.Y + pose.Dy + frame.Oy + frame.Height / 2.0, frame.Width / 2.0 + 11, frame.Height / 2.0 + 7,
                115, EllipseEffect.Magic);
        }
    }

    private static void DrawFloorLightningSmoke(PixelScreenData screen, Game game)
    {
        foreach (var mark in game.LightningTrail)
        {
            var age = game.TickCount - mark.Tick;
            var life = age / (double)Game.LightningTrailTicks;
            var opacity = 145 * Math.Min(age / 12.0, 1) * (1 - life);
            var point = game.ProjectView(mark.X, mark.Z, mark.Y);
            var x = point.X - mark.Facing * 9 * life + Math.Sin(mark.X + life * 5) * life * 5;
            var y = point.Y - 4 - 36 * life;
            DrawSoftEllipse(screen, x, y, 5 + 8 * life, 6 + 10 * life, opacity, EllipseEffect.Smoke, soot: 16);
            DrawSoftEllipse(screen, x + 4, y + 3, 4 + 6 * life, 4 + 7 * life, opacity * 0.65, EllipseEffect.Smoke, soot: 16);
        }
    }

    private static void DrawFireballHeatHaze(PixelScreenData screen, Game game)
    {
        const double radiusX = 36;
        const double radiusY = 23;
        foreach (var fireball in game.Actors)
        {
            if (fireball.Kind != ActorKind.Fireball || fireball.State is FighterState.Dying or FighterState.Gone)
                continue;

            var point = game.ProjectView(fireball.X, fireball.Z, fireball.Y);
            var centerX = point.X + (fireball.Animation.Current.Dx - 11) * fireball.Facing;
            var centerY = point.Y - 16;
            var left = Math.Max(1, (int)Math.Floor(centerX - radiusX));
            var right = Math.Min(screen.Width - 2, (int)Math.Ceiling(centerX + radiusX));
            var top = Math.Max(0, (int)Math.Floor(centerY - radiusY));
            var bottom = Math.Min(screen.Height - 1, (int)Math.Ceiling(centerY + radiusY));
            var time = game.TickCount * 0.36;

            for (var y = top; y <= bottom; y++)
            {
                var dy = (y + 0.5 - centerY) / radiusY;
                var ripple = Math.Sin(y * 0.57 + time);
                for (var x = left; x <= right; x++)
                {
                    var dx = (x + 0.5 - centerX) / radiusX;
                    var falloff = 1 - dx * dx - dy * dy;
                    if (falloff <= 0)
                        continue;

                    var wave = ripple * 0.62 + 0.30 * Math.Sin(x * 0.26 + y * 0.29 + time * 1.6);
                    var weight = (byte)(255 * Math.Abs(wave) * falloff * falloff);
                    if (weight != 0)
                    {
                        var offset = y * screen.Width + x;
                        screen.RefractPixel(offset, offset + Math.Sign(wave), weight);
                    }
                }
            }
        }
    }

    private void DrawFireLighting(PixelScreenData screen, Game game)
    {
        foreach (var actor in game.Actors)
        {
            if (actor.IsGone)
                continue;

            if (actor.Kind == ActorKind.Campfire)
            {
                var foot = game.ProjectView(actor.X, actor.Z, actor.Y);
                var time = game.TickCount / 60.0;
                var flicker = 1 + 0.10 * Math.Sin(time * 11) + 0.06 * Math.Sin(time * 29);
                DrawSoftEllipse(screen, foot.X, foot.Y + 1, 78, 33, 82 * flicker, occludedByGround: true);
                DrawSoftEllipse(screen, foot.X, foot.Y - 13, 27, 25, 64 * flicker);
            }
            else if (actor.Kind == ActorKind.Fireball)
            {
                DrawFireballLighting(screen, game, actor);
            }
            else if (actor.Kind == ActorKind.Flame)
            {
                DrawFlameBreathLighting(screen, game, actor);
            }
        }
    }

    private void DrawFlameBreathLighting(PixelScreenData screen, Game game, Fighter flame)
    {
        var step = flame.Animation.Current;
        if ((uint)step.Frame >= (uint)m_data.Frames.Length)
            return;

        var frame = m_data.Frames[step.Frame];
        var point = game.ProjectView(flame.X, flame.Z, flame.Y);
        var direction = IsFlipped(flame, step) ? -1 : 1;
        var offsetX = step.Dx * flame.Facing + (frame.Ox + frame.Width / 2.0) * direction;
        var centerX = point.X + offsetX;
        var centerY = point.Y + step.Dy + frame.Oy + frame.Height / 2.0;
        var intensity = Math.Min(1, frame.Width / 42.0) * ActorFade(game, flame);
        DrawSoftEllipse(screen, centerX, centerY, frame.Width * 0.7, frame.Height * 0.75, 75 * intensity);

        var ground = game.Ground(flame.X + offsetX, flame.Z);
        if (ground is <= 3840 or >= 5248)
            return;
        var foot = game.ProjectView(flame.X, flame.Z, ground);
        DrawSoftEllipse(screen, centerX, foot.Y + 1, frame.Width * 0.95, 21, 86 * intensity);
    }

    internal static double ActorFade(Game game, Fighter actor)
    {
        if (actor.Kind == ActorKind.Hero)
        {
            if (actor.State == FighterState.Respawning)
                return Math.Clamp(1 - actor.StateTicks / (double)Game.HeroFadeTicks, 0, 1);
            if (actor.RespawnTick >= 0)
                return Math.Clamp((game.TickCount - actor.RespawnTick) / (double)Game.HeroFadeTicks, 0, 1);
        }
        if (actor.State == FighterState.Dying && actor.Kind is ActorKind.Fireball or ActorKind.Flame)
            return Math.Clamp(1 - actor.StateTicks / (double)(actor.Kind == ActorKind.Fireball
                ? Game.FireballImpactTicks : Game.FlameFadeTicks), 0, 1);
        return 1;
    }

    private static void DrawFireballLighting(PixelScreenData screen, Game game, Fighter fireball)
    {
        var fade = fireball.State == FighterState.Dying
            ? Math.Max(0, 1 - fireball.StateTicks / (double)Game.FireballImpactTicks)
            : 1;
        var step = fireball.Animation.Current;
        var ball = game.ProjectView(fireball.X, fireball.Z, fireball.Y);
        var x = ball.X + step.Dx * fireball.Facing;
        DrawSoftEllipse(screen, x, ball.Y - 6, 23, 15, 100 * fade);

        var ground = game.Ground(fireball.X, fireball.Z);
        if (ground is <= 3840 or >= 5248)
            return;

        var foot = game.ProjectView(fireball.X, fireball.Z, ground);
        var height = Math.Max(0, ground - fireball.Y);
        DrawSoftEllipse(screen, x, foot.Y + 2, 51, 20, 120 * fade / (1 + height / 65));
    }

    private static (double X, double Y) ProjectMagic(Game game, int[] effect) =>
        effect.Length > 6 && effect[6] == 1 ? (effect[3], effect[5]) :
            game.ProjectView(game.Hero.X + effect[3], game.Hero.Z + effect[4], game.Hero.Y + effect[5]);

    private void DrawMagic(PixelScreenData screen, Game game, bool effects, Rgb tint, byte effectOpacity)
    {
        var dwarf = game.Character.MagicKey == "gilius";
        var sprites = game.ActiveMagicEffects[game.MagicTick].Where(e => !dwarf || !LightningCage.IsSprite(e)).ToArray();
        var dim = DimMagicSprites(game.ActiveMagicEffects, game.MagicTick).Where(e => !dwarf || !LightningCage.IsSprite(e)).ToArray();
        if (effects)
            DrawMagicLighting(screen, game, sprites, dim, tint, effectOpacity);

        foreach (var effect in dim)
        {
            screen.BeginTranslucentSprite((byte)(MagicGapOpacity(game.ActiveMagicEffects, game.MagicTick, effect) *
                MagicEffectOpacity(game.ActiveMagicEffects, game.MagicTick, effect) / 255));
            DrawMagicSprite(screen, game, effect);
            screen.EndTranslucentSprite();
        }
        foreach (var effect in sprites)
        {
            screen.BeginTranslucentSprite(MagicEffectOpacity(game.ActiveMagicEffects, game.MagicTick, effect));
            DrawMagicSprite(screen, game, effect);
            screen.EndTranslucentSprite();
        }
        DrawLightningCages(screen, game, effects);
    }

    private void DrawLightningTint(PixelScreenData screen, Game game, int frame, int palette, double x, double y, bool flip)
    {
        screen.BeginTranslucentSprite(180);
        Sprite(screen, frame, palette, x, y, flip, paletteOverride: m_redTint);
        screen.EndTranslucentSprite();
        screen.BeginTranslucentSprite(LightningCage.WhiteOpacity(game.MagicTick - game.MagicShakeStart));
        Sprite(screen, frame, palette, x, y, flip, paletteOverride: m_whiteTint);
        screen.EndTranslucentSprite();
    }

    internal (double X, double Y) LightningAnchor(Game game, Fighter target)
    {
        var point = game.ProjectView(target.X, target.Z, target.Y);
        var step = target.Animation.Current;
        if (target.Mount is { } mount && m_data.Riders.TryGetValue(mount.Animation.Address, out var riders))
        {
            var rider = riders[Math.Min(mount.Animation.Index, riders.Length - 1)];
            var pose = mount.Animation.Current;
            var footprint = m_footprints[rider[0]] ??= ScanFootprint(m_data.Frames[rider[0]]);
            var flip = (rider[1] != 0) ^ mount.Facing < 0;
            var feet = footprint.Feet.Project((pose.Dx + rider[2]) * mount.Facing, flip);
            return (point.X + feet.Center, point.Y + pose.Dy + rider[3] + footprint.BodyBottom);
        }
        var current = ProjectFootprint(target, step);
        return (point.X + current.Feet.Center, point.Y + step.Dy + current.BodyBottom);
    }

    private void DrawLightningCages(PixelScreenData screen, Game game, bool effects)
    {
        var tick = LightningCage.SampleTick(game);
        if (tick < 0)
            return;
        var source = game.MagicEffects[0];
        var sprites = source[tick].Where(LightningCage.IsSprite).Select(e => (Effect: e, Opacity: (byte)255))
            .Concat(DimMagicSprites(source, tick).Where(LightningCage.IsSprite)
                .Select(e => (Effect: e, Opacity: MagicGapOpacity(source, tick, e)))).ToArray();
        foreach (var target in game.MagicTargets.Prepend(game.Hero).OrderBy(a => a.Z))
        foreach (var (effect, opacity) in sprites)
        {
            var renderOpacity = (byte)(opacity * MagicEffectOpacity(source, tick, effect) / 255);
            var point = LightningAnchor(game, target);
            // The captured cage was six pixels left and one depth unit forward of the caster.
            // Remove that caster-specific offset before attaching it to the displayed victim pose.
            point = (point.X + effect[3] + 6, point.Y + effect[5] + (effect[4] - 1) * 0.75);
            if (effects)
            {
                var frame = m_data.Frames[effect[0]];
                DrawSoftEllipse(screen, point.X, point.Y + frame.Oy + frame.Height / 2.0,
                    frame.Width * 0.65 + 12, frame.Height * 0.55 + 8, renderOpacity * 0.22, EllipseEffect.Magic);
            }
            screen.BeginTranslucentSprite(renderOpacity);
            Sprite(screen, effect[0], effect[1], point.X, point.Y, effect[2] == 1);
            screen.EndTranslucentSprite();
        }
    }

    private void DrawMagicSprite(PixelScreenData screen, Game game, int[] effect)
    {
        var point = ProjectMagic(game, effect);
        Sprite(screen, effect[0], effect[1], point.X, point.Y, effect[2] == 1);
    }

    internal static byte MagicEffectOpacity(int[][][] recording, int tick, int[] effect)
    {
        // Potion choreography and physical props stay solid; spell energy peaks at 80%.
        if (effect[1] is 0x4a or 0x51 or 0x47 || effect[0] == 518)
            return 255;
        const int fadeTicks = 12;
        var envelope = Math.Min(Extent(-1), Extent(1));
        return (byte)Math.Round(204 * envelope);

        double Extent(int direction)
        {
            var anchor = effect;
            var last = 0;
            var gap = 0;
            for (var distance = 1; distance < fadeTicks + 6; distance++)
            {
                var sample = tick + distance * direction;
                if (sample < 0 || sample >= recording.Length)
                    break;
                // Screen-space spells are assembled from moving pieces. Keep their shared
                // palette envelope intact while tracking recorded world-space instances separately.
                var screenSpace = effect.Length > 6 && effect[6] == 1;
                int[] candidate = null;
                var nearest = int.MaxValue;
                foreach (var sprite in recording[sample])
                {
                    if (sprite[1] != anchor[1])
                        continue;
                    var separation = Math.Abs(sprite[3] - anchor[3]) + Math.Abs(sprite[4] - anchor[4]) +
                        Math.Abs(sprite[5] - anchor[5]);
                    if ((!screenSpace && (sprite[2] != anchor[2] || separation > 24)) || separation >= nearest)
                        continue;
                    candidate = sprite;
                    nearest = separation;
                }
                if (candidate == null)
                {
                    if (++gap > 6)
                        break;
                    continue;
                }
                anchor = candidate;
                last = distance;
                gap = 0;
                if (last >= fadeTicks - 1)
                    break;
            }
            return Math.Min(1, (last + 1) / (double)fadeTicks);
        }
    }

    internal static IEnumerable<int[]> DimMagicSprites(int[][][] recording, int tick)
    {
        const int maxGap = 6;
        for (var previous = tick - 1; previous >= Math.Max(0, tick - maxGap); previous--)
        foreach (var sprite in recording[previous])
        {
            // Fill a brief dropout only when the effect returns at the same anchor.
            var next = previous + 1;
            var end = Math.Min(recording.Length - 1, previous + maxGap + 1);
            while (next <= end && !ContainsMagicAnchor(recording[next], sprite))
                next++;
            if (next > tick && next <= end)
                yield return sprite;
        }
    }

    private static bool ContainsMagicAnchor(int[][] sprites, int[] match) =>
        sprites.Any(sprite => sprite.AsSpan(1).SequenceEqual(match.AsSpan(1)));

    internal static byte MagicGapOpacity(int[][][] recording, int tick, int[] effect)
    {
        for (var previous = tick - 1; previous >= Math.Max(0, tick - 6); previous--)
        {
            if (!ContainsMagicAnchor(recording[previous], effect))
                continue;
            for (var next = tick + 1; next <= Math.Min(recording.Length - 1, previous + 7); next++)
            {
                if (!ContainsMagicAnchor(recording[next], effect))
                    continue;
                var phase = (tick - previous) / (double)(next - previous);
                return (byte)Math.Round(255 - (255 - MinimumMagicOpacity) * Math.Sin(Math.PI * phase));
            }
            break;
        }
        return 255;
    }

    private void DrawMagicLighting(PixelScreenData screen, Game game, int[][] sprites, int[][] dim, Rgb tint, byte effectOpacity)
    {
        var spellColor = game.Character.MagicKey switch
        {
            "gilius" => new Rgb(80, 178, 255),
            "tyris" => FireLightColor,
            _ => new Rgb(255, 182, 113)
        };
        screen.MagicLightColor = ScaleLight(tint ?? spellColor, effectOpacity);
        foreach (var effect in sprites)
            DrawMagicGlow(screen, game, effect, 1);
        foreach (var effect in dim)
            DrawMagicGlow(screen, game, effect, MagicGapOpacity(game.ActiveMagicEffects, game.MagicTick, effect) / 255.0);
    }

    private void DrawMagicGlow(PixelScreenData screen, Game game, int[] effect, double opacity)
    {
        // Potion icons, the dragon's silver head and solid meteor pieces do not emit flame light.
        if (effect[1] is 0x4a or 0x51 or 0x47 || effect[0] == 518)
            return;

        var frame = m_data.Frames[effect[0]];
        var point = ProjectMagic(game, effect);
        var direction = effect[2] == 1 ? -1 : 1;
        var centerX = point.X + (frame.Ox + frame.Width / 2.0) * direction;
        var centerY = point.Y + frame.Oy + frame.Height / 2.0;
        opacity *= MagicEffectOpacity(game.ActiveMagicEffects, game.MagicTick, effect) / 204.0;
        var strength = Math.Clamp(36 + frame.Width * 0.85, 46, 91) * opacity;
        DrawSoftEllipse(screen, centerX, centerY,
            24 + frame.Width * 0.64, 16 + frame.Height * 0.66, strength, EllipseEffect.Magic);
    }

    private static void DrawMagicGroundGlow(PixelScreenData screen, Game game, Rgb tint, byte effectOpacity)
    {
        foreach (var glow in game.MagicGroundGlows)
        {
            var color = glow.MagicKey switch
            {
                "gilius" => new Rgb(80, 178, 255),
                "tyris" => FireLightColor,
                _ => new Rgb(255, 182, 113)
            };
            screen.MagicLightColor = ScaleLight(tint ?? color, effectOpacity);
            var point = game.ProjectView(glow.X, glow.Z, glow.Y);
            var fade = 1 - (game.TickCount - glow.Tick) / (double)MagicGroundGlow.FadeTicks;
            // Heating stops when the flame leaves; cooling starts from the accumulated brightness.
            var heat = Math.Clamp((glow.Tick - glow.StartTick + 1) / (double)MagicGroundGlow.HeatTicks, 0, 1);
            DrawSoftEllipse(screen, point.X, point.Y - 2, 48, 16,
                125 * glow.Strength * heat * fade, EllipseEffect.Magic);
        }
    }

    private static Rgb ScaleLight(Rgb color, byte opacity) =>
        new((byte)(color.R * opacity / 255), (byte)(color.G * opacity / 255), (byte)(color.B * opacity / 255));

    private void DrawCampfireEmbers(PixelScreenData screen, double x, double y, int tick)
    {
        const int interval = 7;
        const int lifetime = 49;
        for (var age = tick % interval; age < lifetime && age <= tick; age += interval)
        {
            var phase = (tick - age) / interval * 2.399963;
            var progress = age / (double)lifetime;
            var sparkX = x + 7 * Math.Sin(phase) + progress * (7 + 17 * Math.Cos(phase)) +
                2.5 * Math.Sin(phase + progress * 6);
            var sparkY = y - 14 - progress * (29 + 17 * Math.Sin(phase + 1));
            var glow = 110 * (1 - progress) * (1 - progress);
            if (progress < 0.73)
                screen.SetPixel((int)Math.Round(sparkX), (int)Math.Round(sparkY), progress < 0.37 ? m_emberCore : m_orange);
            DrawSoftEllipse(screen, sparkX, sparkY, 2.6, 2.6, glow);
        }
    }

    private byte[] SampleLandingDustTints(PixelScreenData screen, Game game)
    {
        if (game.LandingDust.Count == 0)
            return [];

        var tints = new byte[game.LandingDust.Count];
        for (var i = 0; i < tints.Length; i++)
        {
            var puff = game.LandingDust[i];
            var ground = game.Ground(puff.X, puff.Z);
            var point = game.ProjectView(puff.X, puff.Z, ground is > 3840 and < 5248 ? ground : puff.Y);
            var centerX = Math.Clamp((int)Math.Round(point.X), 0, screen.Width - 1);
            var centerY = Math.Clamp((int)Math.Round(point.Y), 0, screen.Height - 1);
            var red = 0;
            var green = 0;
            var blue = 0;
            var count = 0;
            for (var y = Math.Max(0, centerY - 2); y <= Math.Min(screen.Height - 1, centerY + 2); y++)
            for (var x = Math.Max(0, centerX - 5); x <= Math.Min(screen.Width - 1, centerX + 5); x++)
            {
                var color = m_data.Palette[screen.Pixels[y * screen.Width + x]];
                red += color.R;
                green += color.G;
                blue += color.B;
                count++;
            }

            // Suspended dust is lighter than the ground it came from.
            tints[i] = Color(76 + red * 179 / (255 * count), 76 + green * 179 / (255 * count), 76 + blue * 179 / (255 * count));
        }
        return tints;
    }

    private static void DrawLandingDust(PixelScreenData screen, Game game, byte[] tints)
    {
        // The feather-filled stretch carries suspended dust gently to the left (pixels per second).
        var wind = EagleFeathers.IsVisible(game) ? -6.0 : 0;
        for (var i = 0; i < tints.Length; i++)
        {
            var puff = game.LandingDust[i];
            var foot = game.ProjectView(puff.X, puff.Z, puff.Y);
            var age = game.TickCount - puff.Tick;
            var count = puff.Running ? 3 : puff.Heavy ? 14 : 9;
            for (var particle = 0; particle < count; particle++)
            {
                // Each particle has stable launch properties, independent of rendering frequency.
                var seed = unchecked(puff.Tick * 397 ^ (int)puff.X * 31 ^ (int)puff.Z * 17 ^ particle * 7919);
                var delay = DustRandom(seed) * 6;
                var lifetime = (puff.Running ? 55 : 120) + DustRandom(seed + 1) * 24;
                var elapsed = age - delay;
                if (elapsed <= 0 || elapsed >= lifetime)
                    continue;

                var progress = elapsed / lifetime;
                var remaining = 1 - progress;
                var direction = DustRandom(seed + 2) * 2 - 1;
                var launchSpeed = (puff.Running ? 6 : puff.Heavy ? 28 : 19) * direction;
                var drift = (DustRandom(seed + 3) * 2 - 1) * 11;
                var rise = puff.Running ? 7 + DustRandom(seed + 4) * 9 : 22 + DustRandom(seed + 4) * 25;
                var phase = DustRandom(seed + 5) * Math.PI * 2;
                var expansion = 1 - Math.Exp(-progress * 4);
                var x = foot.X + direction * puff.Spread + launchSpeed * expansion + drift * progress +
                    Math.Sin(phase + progress * 5) * progress * 3 + wind * elapsed / 60;
                var y = foot.Y + 2 - rise * progress - 4 * expansion;
                var size = (puff.Running ? 1.5 : puff.Heavy ? 4.5 : 3) + DustRandom(seed + 6) * 2;
                var radiusX = size + progress * 7;
                var radiusY = size * 0.7 + progress * 6;
                var opacity = (puff.Running ? 90 : puff.Heavy ? 175 : 135) * (0.7 + DustRandom(seed + 7) * 0.3) *
                    Math.Min(1, elapsed / 3) * remaining;
                DrawSoftEllipse(screen, x, y, radiusX, radiusY, opacity, EllipseEffect.Dust, tint: tints[i]);
            }
        }
    }

    private static void DrawDragonSmoke(PixelScreenData screen, Game game)
    {
        foreach (var puff in game.DragonSmoke)
        {
            var age = game.TickCount - puff.Tick;
            var progress = age / (double)puff.Duration;
            var point = game.ProjectView(puff.X, puff.Z, puff.Y);
            var phase = DustRandom(puff.Tick + (int)puff.X) * Math.PI * 2;
            var x = point.X + Math.Sin(phase + progress * 4) * progress * 7;
            var y = point.Y - age / 120.0 * 44 * Math.Sqrt(puff.Scale);
            var opacity = 180 * puff.Opacity * Math.Min(1, age / 5.0) * Math.Pow(1 - progress, 0.8);
            DrawSoftEllipse(screen, x, y, (5 + progress * 12) * puff.Scale, (6 + progress * 15) * puff.Scale, opacity, EllipseEffect.Smoke,
                soot: (byte)(255 * puff.Soot * Math.Pow(1 - progress, 1.5)));
        }
    }

    private static double DustRandom(int seed)
    {
        var value = unchecked((uint)seed);
        value ^= value >> 16;
        value = unchecked(value * 0x7feb352d);
        value ^= value >> 15;
        value = unchecked(value * 0x846ca68b);
        value ^= value >> 16;
        return (value & 0xffffff) / 16777216.0;
    }

    private static void DrawCampfireSmoke(PixelScreenData screen, double x, double y, int tick)
    {
        const int interval = 12;
        const int lifetime = 108;
        for (var age = tick % interval; age < lifetime && age <= tick; age += interval)
        {
            var progress = age / (double)lifetime;
            var phase = (tick - age) / interval * 1.7;
            var wind = 25 * progress + Math.Sin(progress * 7 + phase) * (1 + progress * 6);
            var opacity = 115 * Math.Min(1, progress * 11) * Math.Pow(1 - progress, 1.3);
            var centerX = x + wind;
            var centerY = y - 12 - 60 * progress;
            var soot = (byte)(255 * Math.Pow(1 - progress, 3));
            DrawSoftEllipse(screen, centerX, centerY, 4 + 12 * progress, 6 + 9 * progress, opacity, EllipseEffect.Smoke, soot: soot);
            DrawSoftEllipse(screen, centerX - 3, centerY + 2, 3 + 9 * progress, 5 + 7 * progress, opacity * 0.45, EllipseEffect.Smoke, soot: soot);
        }
    }

    private static void DrawSoftEllipse(PixelScreenData screen, double centerX, double centerY,
        double radiusX, double radiusY, double strength, EllipseEffect effect = EllipseEffect.Fire, bool occludedByGround = false, byte tint = 0,
        byte soot = 0)
    {
        var left = Math.Max(0, (int)Math.Floor(centerX - radiusX));
        var right = Math.Min(screen.Width - 1, (int)Math.Ceiling(centerX + radiusX));
        var top = Math.Max(0, (int)Math.Floor(centerY - radiusY));
        var bottom = Math.Min(screen.Height - 1, (int)Math.Ceiling(centerY + radiusY));
        for (var y = top; y <= bottom; y++)
        {
            var dy = (y + 0.5 - centerY) / radiusY;
            for (var x = left; x <= right; x++)
            {
                var dx = (x + 0.5 - centerX) / radiusX;
                var coverage = 1 - dx * dx - dy * dy;
                if (coverage <= 0)
                    continue;

                var opacity = (byte)(strength * coverage * coverage);
                if (opacity == 0)
                    continue;
                var offset = y * screen.Width + x;
                switch (effect)
                {
                    case EllipseEffect.Smoke:
                        screen.SmokePixel(offset, opacity, soot);
                        break;
                    case EllipseEffect.Magic:
                        screen.MagicLightPixel(offset, opacity);
                        break;
                    case EllipseEffect.Dust:
                        screen.DustPixel(offset, tint, opacity);
                        break;
                    default:
                        screen.LightPixel(offset, opacity, occludedByGround);
                        break;
                }
            }
        }
    }

    private void DrawContactShadows(PixelScreenData screen, Game game)
    {
        foreach (var actor in game.Actors.Concat(game.StoneBodies))
        {
            var potion = actor.Kind is ActorKind.Potion or ActorKind.CampPotion or ActorKind.Food;
            if (actor.Mount != null || actor.State == FighterState.Gone && actor.PickupTick < 0 || ActorFade(game, actor) == 0 ||
                actor.State == FighterState.Entering && actor.StateTicks < Game.BrotherDoorOpeningTicks ||
                (!potion && !IsCharacter(actor)))
                continue;

            var ground = game.Ground(actor.X, actor.Z);
            if (ground is <= 3840 or >= 5248 || actor.Y > ground + 12)
                continue;

            var point = game.ProjectView(actor.X, actor.Z, ground);
            var step = actor.Animation.Current;
            if (potion && (uint)step.Frame >= (uint)m_data.Frames.Length)
                continue;

            var sprite = potion ? m_data.Frames[step.Frame] : GetIdleSprite(actor);
            var radiusX = sprite.Width * (potion ? 1.12 : 0.72);
            var radiusY = sprite.Width * (potion ? 0.29 : 0.22);
            var centerX = point.X;
            var centerY = point.Y + (potion ? 1.5 : 0.5);
            var height = Math.Max(0, ground - actor.Y);
            var strength = 100 / (1 + height / 36) * ActorFade(game, actor);
            var validPose = (uint)step.Frame < (uint)m_data.Frames.Length;
            if (!potion && actor.State is FighterState.Down or FighterState.Dying or FighterState.Petrified or FighterState.Respawning && validPose)
            {
                var prone = m_data.Frames[step.Frame];
                var pose = ProjectFootprint(actor, step);
                centerX += pose.LowerBody.Center;
                centerY = point.Y + actor.Y - ground + step.Dy + pose.BodyBottom - 2;
                radiusX = Math.Max(radiusX * 1.12, prone.Width * 0.72);
                radiusY *= 0.8;
            }
            else if (!potion && !game.IsCamp && validPose)
            {
                DrawSunShadow(screen, game, actor, ground, point, height, strength);
                var pose = GetAnimatedFootprint(actor);
                centerX += pose.Feet.Center;
                radiusX = pose.Feet.Width * 0.5 + 4;
                radiusY = 4;
                strength *= 0.65;
            }
            else if (!potion && !actor.IsMount && validPose)
            {
                var pose = GetAnimatedFootprint(actor);
                centerX += pose.Feet.Center;
                radiusX = pose.Feet.Width * 0.65 + 12;
                DrawGroundShadow(screen, game, actor, ground, point, point.X + pose.LowerBody.Center, centerY,
                    pose.LowerBody.Width * 0.7 + 14, radiusY, strength * 0.8);
            }

            DrawGroundShadow(screen, game, actor, ground, point, centerX, centerY, radiusX, radiusY, strength);
        }
    }

    private void DrawSunShadow(PixelScreenData screen, Game game, Fighter actor, double ground,
        (double X, double Y) point, double height, double strength)
    {
        var animation = actor.Animation;
        var step = animation.Current;
        DrawPose(step.Frame, step.Dx * actor.Facing, step.Dy, IsFlipped(actor, step));
        if (actor.Rider != null && m_data.Riders.TryGetValue(animation.Address, out var riders))
        {
            var rider = riders[Math.Min(animation.Index, riders.Length - 1)];
            DrawPose(rider[0], (step.Dx + rider[2]) * actor.Facing, step.Dy + rider[3], (rider[1] != 0) ^ actor.Facing < 0);
        }

        void DrawPose(int frame, int offsetX, int offsetY, bool flip)
        {
            if ((uint)frame >= (uint)m_data.Frames.Length)
                return;
            var orientation = flip ? 1 : 0;
            var shadow = m_sunShadows[frame, orientation] ??= CreateSunShadow(m_data.Frames[frame], flip);
            var verticalOffset = offsetY - height;
            var left = (int)Math.Round(point.X + offsetX + verticalOffset * ShadowSkew) + shadow.Left;
            var top = (int)Math.Round(point.Y + verticalOffset * ShadowSquash) + shadow.Top;
            for (var sy = Math.Max(0, -top); sy < shadow.Height && top + sy < screen.Height; sy++)
            {
                var y = top + sy;
                var depth = y + 0.5 - point.Y;
                var z = actor.Z + depth * 4 / 3;
                var fade = Math.Clamp(1 + depth / 27, 0.12, 1);
                // Deepen the contact end without changing the distant silhouette or its soft edge.
                // Airborne actors lose this extra grounding as they rise away from the floor.
                var nearFeet = Math.Clamp(1 - Math.Abs(depth) / 18, 0, 1);
                fade *= 1 + 0.55 * nearFeet * nearFeet / (1 + height / 24);
                for (var sx = Math.Max(0, -left); sx < shadow.Width && left + sx < screen.Width; sx++)
                {
                    var coverage = shadow.Mask[sy * shadow.Width + sx];
                    if (coverage == 0)
                        continue;
                    var x = left + sx;
                    var terrain = ShadowTerrainCoverage(game, actor.X + x + 0.5 - point.X, z, ground);
                    var opacity = (byte)(strength * fade * terrain * coverage / 16);
                    if (opacity != 0)
                        screen.ShadePixel(y * screen.Width + x, opacity);
                }
            }
        }
    }

    private SunShadow CreateSunShadow(SpriteFrame frame, bool flip)
    {
        var origin = flip ? -frame.Ox - frame.Width : frame.Ox;
        var left = origin + (int)Math.Floor(frame.Oy * ShadowSkew) - 2;
        var top = (int)Math.Floor(frame.Oy * ShadowSquash) - 2;
        var width = origin + frame.Width + (int)Math.Ceiling((frame.Oy + frame.Height) * ShadowSkew) + 2 - left;
        var height = (int)Math.Ceiling((frame.Oy + frame.Height) * ShadowSquash) + 2 - top;
        var mask = new byte[width * height];
        for (var y = 0; y < frame.Height; y++)
        {
            var worldY = frame.Oy + y + 0.5;
            var row = ((int)Math.Floor(worldY * ShadowSquash) - top) * width;
            var column = origin + (int)Math.Floor(worldY * ShadowSkew) - left;
            var source = (frame.Y + y) * m_data.SpriteStride + frame.X;
            for (var x = 0; x < frame.Width; x++)
            {
                if (IsOpaquePen(m_data.SpritePens[source + x]))
                    mask[row + column + (flip ? frame.Width - 1 - x : x)] = 1;
            }
        }

        var softened = new byte[mask.Length];
        for (var y = 1; y < height - 1; y++)
        for (var x = 1; x < width - 1; x++)
        {
            var i = y * width + x;
            softened[i] = (byte)(4 * mask[i] + 2 * (mask[i - 1] + mask[i + 1] + mask[i - width] + mask[i + width]) +
                mask[i - width - 1] + mask[i - width + 1] + mask[i + width - 1] + mask[i + width + 1]);
        }
        return new SunShadow(left, top, width, height, softened);
    }

    private sealed record SunShadow(int Left, int Top, int Width, int Height, byte[] Mask);

    private static void DrawGroundShadow(PixelScreenData screen, Game game, Fighter actor, double ground,
        (double X, double Y) point, double centerX, double centerY, double radiusX, double radiusY, double strength)
    {
        var left = Math.Max(0, (int)Math.Floor(centerX - radiusX));
        var right = Math.Min(screen.Width - 1, (int)Math.Ceiling(centerX + radiusX));
        var top = Math.Max(0, (int)Math.Floor(centerY - radiusY));
        var bottom = Math.Min(screen.Height - 1, (int)Math.Ceiling(centerY + radiusY));

        for (var y = top; y <= bottom; y++)
        {
            var dy = (y + 0.5 - centerY) / radiusY;
            // A depth step projects to roughly three quarters of a screen pixel.
            var z = actor.Z + (y + 0.5 - point.Y) * 4 / 3;
            for (var x = left; x <= right; x++)
            {
                var dx = (x + 0.5 - centerX) / radiusX;
                var coverage = 1 - dx * dx - dy * dy;
                if (coverage <= 0)
                    continue;

                var terrain = ShadowTerrainCoverage(game, actor.X + x + 0.5 - point.X, z, ground);
                var opacity = (byte)(strength * coverage * coverage * terrain);
                if (opacity > 0)
                    screen.ShadePixel(y * screen.Width + x, opacity);
            }
        }
    }

    private static double ShadowTerrainCoverage(Game game, double x, double z, double ground)
    {
        double Match(double sampleX, double sampleZ)
        {
            var height = game.Ground(sampleX, sampleZ);
            if (height is <= 3840 or >= 5248)
                return 0;
            return Math.Clamp((32 - Math.Abs(height - ground)) / 16, 0, 1);
        }

        var center = Match(x, z);
        if (center >= 1)
            return 1;

        // Fade over a few pixels at a ledge instead of cutting the ellipse along a height-map cell.
        var nearby = Math.Max(Math.Max(Match(x - 3, z), Match(x + 3, z)),
            Math.Max(Match(x, z - 4), Match(x, z + 4)));
        return Math.Max(center, nearby * 0.35);
    }

    private PoseFootprint GetAnimatedFootprint(Fighter actor)
    {
        const int easeTicks = 3;
        var animation = actor.Animation;
        var current = ProjectFootprint(actor, animation.Current);
        if (animation.PreviousPose is not { } previous ||
            (uint)previous.Frame >= (uint)m_footprints.Length || animation.PoseTicks >= easeTicks)
            return current;

        var old = ProjectFootprint(actor, previous);
        var amount = (animation.PoseTicks + 1.0) / (easeTicks + 1);
        return current with { Feet = old.Feet.Lerp(current.Feet, amount), LowerBody = old.LowerBody.Lerp(current.LowerBody, amount) };
    }

    private PoseFootprint ProjectFootprint(Fighter actor, AnimationStep step)
    {
        var footprint = m_footprints[step.Frame] ??= ScanFootprint(m_data.Frames[step.Frame]);
        var offset = step.Dx * actor.Facing;
        var flip = IsFlipped(actor, step);
        return footprint with { Feet = footprint.Feet.Project(offset, flip), LowerBody = footprint.LowerBody.Project(offset, flip) };
    }

    private PoseFootprint ScanFootprint(SpriteFrame frame)
    {
        const int footRows = 20;
        var bottom = -1;
        var bodyBottom = -1;
        var feet = new ShadowSpan(frame.Width, 0);
        var lowerBody = feet;
        for (var y = frame.Height - 1; y >= 0; y--)
        {
            var row = (frame.Y + y) * m_data.SpriteStride + frame.X;
            var centerPixels = 0;
            for (var x = 0; x < frame.Width; x++)
            {
                if (!IsOpaquePen(m_data.SpritePens[row + x]))
                    continue;

                if (bottom < 0)
                    bottom = y;
                lowerBody = lowerBody.Include(x);
                if (bottom - y < footRows)
                    feet = feet.Include(x);
                if (x >= frame.Width / 3 && x < frame.Width * 2 / 3)
                    centerPixels++;
            }
            // Keep a prone shadow under the body even if a limb reaches farther down.
            if (bodyBottom < 0 && centerPixels >= Math.Max(1, frame.Width / 8))
                bodyBottom = y;
        }

        if (bottom < 0)
            feet = lowerBody = new ShadowSpan(0, frame.Width);
        var contactRow = bodyBottom >= 0 ? bodyBottom + 1 : bottom >= 0 ? bottom + 1 : frame.Height;
        return new PoseFootprint(feet.Project(frame.Ox, false), lowerBody.Project(frame.Ox, false), frame.Oy + contactRow);
    }

    private readonly record struct PoseFootprint(ShadowSpan Feet, ShadowSpan LowerBody, int BodyBottom);

    private readonly record struct ShadowSpan(double Left, double Right)
    {
        public double Center => (Left + Right) / 2;
        public double Width => Right - Left;

        public ShadowSpan Include(int x) => new(Math.Min(Left, x), Math.Max(Right, x + 1));

        public ShadowSpan Project(double offset, bool flip) =>
            flip ? new(offset - Right, offset - Left) : new(offset + Left, offset + Right);

        public ShadowSpan Lerp(ShadowSpan other, double amount) =>
            new(Left + (other.Left - Left) * amount, Right + (other.Right - Right) * amount);
    }

    private void DrawCampfireShadows(PixelScreenData screen, Game game)
    {
        var fire = game.Actors.FirstOrDefault(actor => actor.Kind == ActorKind.Campfire && !actor.IsGone);
        if (fire == null)
            return;

        foreach (var actor in game.Actors)
        {
            if (actor.Mount != null || actor.State == FighterState.Gone && actor.PickupTick < 0 || ActorFade(game, actor) == 0 || !IsCharacter(actor))
                continue;

            var dx = actor.X - fire.X;
            var dy = (actor.Z - fire.Z) * 0.75;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            var fireCoverage = 1 - dx * dx / (78 * 78) - dy * dy / (33 * 33);
            if (distance <= 5 || fireCoverage <= 0)
                continue;

            var ground = game.Ground(actor.X, actor.Z);
            if (ground is <= 3840 or >= 5248 || actor.Y > ground + 12)
                continue;

            var foot = game.ProjectView(actor.X, actor.Z, ground);
            var sprite = GetIdleSprite(actor);
            var posture = actor.State is FighterState.Sleeping or FighterState.Down or FighterState.Dying ? 0.55 : 1;
            var length = Math.Clamp(12 + distance * 1.15, 18, 58) * posture;
            var width = sprite.Width * 0.23;
            var strength = 245 * Math.Min(1, (distance - 5) / 10) * Math.Min(1, fireCoverage * 3) /
                (1 + Math.Max(0, ground - actor.Y) / 22) * ActorFade(game, actor);
            DrawCastShadow(screen, game, actor, ground, foot, (dx, dy), distance, length, width, strength);
        }
    }

    private static void DrawCastShadow(PixelScreenData screen, Game game, Fighter actor, double ground,
        (double X, double Y) foot, (double X, double Y) fromFire, double distance, double length, double width, double strength)
    {
        var directionX = fromFire.X / distance;
        var directionY = fromFire.Y / distance;
        var endX = foot.X + directionX * length;
        var endY = foot.Y + directionY * length;
        var padding = width + 7;
        var left = Math.Max(0, (int)Math.Floor(Math.Min(foot.X, endX) - padding));
        var right = Math.Min(screen.Width - 1, (int)Math.Ceiling(Math.Max(foot.X, endX) + padding));
        var top = Math.Max(0, (int)Math.Floor(Math.Min(foot.Y, endY) - padding));
        var bottom = Math.Min(screen.Height - 1, (int)Math.Ceiling(Math.Max(foot.Y, endY) + padding));
        for (var y = top; y <= bottom; y++)
        {
            var dy = y + 0.5 - foot.Y;
            var z = actor.Z + dy * 4 / 3;
            for (var x = left; x <= right; x++)
            {
                var dx = x + 0.5 - foot.X;
                var progress = (dx * directionX + dy * directionY + 4) / (length + 4);
                if (progress is < 0 or >= 1)
                    continue;

                var across = (dy * directionX - dx * directionY) / (width + 6 * progress);
                var edge = 1 - across * across;
                if (edge <= 0)
                    continue;

                var terrain = ShadowTerrainCoverage(game, actor.X + dx, z, ground);
                var nearFeet = 1 - progress;
                var contact = 1 + 0.55 * nearFeet * nearFeet;
                var opacity = (byte)Math.Min(255, strength * contact * edge * edge * Math.Min(1, progress * 10) * (1 - progress * progress) * terrain);
                if (opacity == 0)
                    continue;

                var offset = y * screen.Width + x;
                screen.OccludeLightPixel(offset, opacity);
                // Reinforce the cast shadow where the fire spill would otherwise blend into the floor.
                var lightX = (fromFire.X + dx) / 96;
                var lightY = (fromFire.Y + dy) / 47;
                var ambient = (byte)(opacity * 0.64 * Math.Max(0, 1 - lightX * lightX - lightY * lightY));
                if (ambient != 0)
                    screen.ShadePixel(offset, ambient);
            }
        }
    }

    private SpriteFrame GetIdleSprite(Fighter actor)
    {
        var idle = actor.IsMount ? actor.FreeMoves.Idle : actor.Moves.Idle;
        return m_data.Frames[m_data.Animations[idle][0].Frame];
    }

    private static bool IsFlipped(Fighter actor, AnimationStep step) => (step.Flags & 1) != 0 ^ actor.Facing < 0;

    private static bool IsCharacter(Fighter actor) =>
        actor.State != FighterState.Emerging &&
        (actor.IsEnemy || actor.IsMount || actor.Kind is ActorKind.Hero or ActorKind.Thief or ActorKind.Villager);

    private void DrawFeathers(PixelScreenData screen, Game game)
    {
        if (!EagleFeathers.IsVisible(game))
            return;
        for (var i = 0; i < EagleFeathers.Count; i++)
        {
            var feather = EagleFeathers.Sample(i, game.TickCount, game.ViewCamera.X);
            Sprite(screen, feather.Frame, EagleFeathers.Palette, feather.X, feather.Y, feather.Flip, divisor: 2);
        }
    }

    private void DrawSurf(PixelScreenData screen, Game game)
    {
        if (game.SurfAge is not { } age)
            return;

        var index = TurtleVillageSurf.Frame(age);
        var frame = m_data.Frames[index];
        var point = game.ProjectView(TurtleVillageSurf.X(age), TurtleVillageSurf.Depth, TurtleVillageSurf.Y(age));
        var opacity = TurtleVillageSurf.Opacity(ArcadeData.Height - (int)Math.Round(point.Y + frame.Oy));
        if (opacity == 0)
            return;

        var width = TurtleVillageSurf.StripWidth;
        var left = (int)point.X - (frame.Ox + width);
        left -= (int)Math.Ceiling((double)left / width) * width;
        if (opacity < 255)
            screen.BeginTranslucentSprite(opacity);
        for (; left < ArcadeData.Width; left += width)
            Sprite(screen, index, TurtleVillageSurf.Palette, left + frame.Ox + width, point.Y, true, paletteOverride: m_data.SurfPalette);
        if (opacity < 255)
            screen.EndTranslucentSprite();
    }

    private void DrawGoPrompt(PixelScreenData screen)
    {
        // Frame 646 contains both the sword and GO! lettering (atlas position 398, 1502).
        // The gold palette is provisional until checked against a set-6 capture.
        var frame = m_data.Frames[646];
        Sprite(screen, 646, 0x40, 248 - frame.Ox, 48 - frame.Oy, false);
    }

    private byte Color(int red, int green, int blue) => (byte)Enumerable.Range(0, m_data.StaticPaletteCount)
        .OrderBy(i => Math.Pow(m_data.Palette[i].R - red, 2) + Math.Pow(m_data.Palette[i].G - green, 2) + Math.Pow(m_data.Palette[i].B - blue, 2)).First();

    private void DrawHud(PixelScreenData screen, Game game)
    {
        Text(screen, $"STAGE {m_data.StageNumber + (game.IsCamp ? 1 : 0)}", 16, 16, m_hudRed, m_yellow);
        DrawMagicMeter(screen, game);
        for (var bar = 0; bar < 3; bar++)
        {
            var amount = Math.Clamp(game.Hero.Health - bar * 16, 0, 16);
            Fill(screen, 56 + bar * 24, 205, 22, 8, m_data.Black);
            Fill(screen, 57 + bar * 24, 206, amount * 20 / 16, 6, m_blue);
            Fill(screen, 57 + bar * 24, 206, amount * 20 / 16, 1, m_lightBlue);
        }
        DrawLives(screen, game.Lives);
        DrawHudPortrait(screen, game.Character.PortraitTile);
    }

    private void DrawLives(PixelScreenData screen, int lives)
    {
        var digits = Math.Max(0, lives).ToString();
        for (var digit = 0; digit < digits.Length; digit++)
        {
            var number = digits[digit] - '0';
            var left = 136 - (digits.Length - digit) * 8;
            for (var y = 0; y < 16; y++)
            for (var x = 0; x < 8; x++)
            {
                // Zero and nine share their upper half; the lower halves run from zero through nine.
                var tile = y < 8 ? 0x60 + number % 9 : 0x69 + number;
                if (TilePen(tile, x, y % 8) != 0)
                    screen.SetPixel(left + x, 200 + y, m_livesYellow);
            }
        }
    }

    private void DrawHudPortrait(PixelScreenData screen, int firstTile)
    {
        const int left = 136;
        const int top = 200;
        var palette = m_data.PortraitPalettes[firstTile];
        for (var y = 0; y < 16; y++)
        for (var x = 0; x < 16; x++)
        {
            var tile = firstTile + y / 8 * 2 + x / 8;
            var pen = TilePen(tile, x % 8, y % 8);
            if (pen != 0)
                screen.SetPixel(left + x, top + y, palette[pen]);
        }
    }

    private void DrawMagicMeter(PixelScreenData screen, Game game)
    {
        const int left = 104;
        const int top = MagicMeterTop;
        const int slotWidth = 10;
        const int height = 18;
        var hero = game.Character;
        Text(screen, "MAGIC", left, 2, m_hudPink, m_hudRed);
        // Pulse the filled background smoothly; artwork and tier dividers stay crisp.
        var pulse = (byte)(96 + 159 * (0.5 + 0.5 * Math.Sin(game.TickCount * Math.PI / 30)));
        Fill(screen, left, top, hero.PotionCapacity * slotWidth + 1, height, m_data.White);
        for (var tier = 0; tier < hero.MagicLevels; tier++)
        {
            var start = hero.MagicThresholds[tier] - 1;
            var end = tier + 1 < hero.MagicLevels ? hero.MagicThresholds[tier + 1] - 1 : hero.PotionCapacity;
            var x = left + start * slotWidth + 1;
            Fill(screen, x, top + 1, (end - start) * slotWidth - 1, height - 2, m_darkRed);
            var filled = Math.Min(end, game.Potions) - start;
            if (filled > 0)
            {
                screen.BeginTranslucentSprite(pulse);
                Fill(screen, x, top + 1, filled * slotWidth - 1, height - 2, m_filledRed);
                screen.EndTranslucentSprite();
            }
        }

        for (var slot = 0; slot < game.Potions; slot++)
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            // The text tile includes the bottle and the meter's lower border.
            var pen = TilePen(0x01a, x, y);
            if (pen is not (3 or 4 or 5))
                continue;
            var color = pen switch { 3 => m_data.White, 4 => m_lightBlue, _ => m_blue };
            screen.SetPixel(left + slot * slotWidth + 1 + x, top + height - 8 + y, color);
        }

        for (var tier = 0; tier < hero.MagicLevels; tier++)
            Text(screen, (tier + 1).ToString(), left + (hero.MagicThresholds[tier] - 1) * slotWidth + 1, top + 1, m_yellow);
    }

    private static void Fill(PixelScreenData screen, int left, int top, int width, int height, byte color)
    {
        for (var y = top; y < top + height; y++)
        for (var x = left; x < left + width; x++)
            screen.SetPixel(x, y, color);
    }
    private void Layer(PixelScreenData screen, TileLayer layer, int u, int v, byte[] palette, bool high, bool opaque,
        bool wrap = false, bool skipSky = false, byte[] previousPalette = null, byte opacity = 255)
    {
        for (var sy = 0; sy < ArcadeData.Height; sy++)
        {
            var y = sy + v - layer.Y;
            if (wrap)
                y = (y % (layer.Height * 8) + layer.Height * 8) % (layer.Height * 8);
            if (y < 0 || y >= layer.Height * 8)
                continue;
            for (var sx = 0; sx < ArcadeData.Width; sx++)
            {
                var x = sx + u - layer.X;
                if (wrap)
                    x = (x % (layer.Width * 8) + layer.Width * 8) % (layer.Width * 8);
                if (x < 0 || x >= layer.Width * 8)
                    continue;
                var cell = layer.Cells[y / 8 * layer.Width + x / 8];
                if (cell == uint.MaxValue || !opaque && ((cell >> 23 & 1) != 0) != high)
                    continue;
                if (skipSky && (cell >> 16 & 127) is 114 or 115)
                    continue;
                var tile = (int)(cell & 65535);
                var pen = m_data.TilePens[(tile / 128 * 8 + y % 8) * m_data.TileStride + tile % 128 * 8 + x % 8];
                if (pen != 0 || opaque)
                {
                    var offset = sy * ArcadeData.Width + sx;
                    var color = palette[(cell >> 16 & 127) * 8 + pen];
                    if (previousPalette != null)
                    {
                        var previous = previousPalette[(cell >> 16 & 127) * 8 + pen];
                        screen.SetPixelAtOffset(offset, previous);
                        if (previous != color && opacity > 0)
                        {
                            screen.BeginTranslucentSprite(opacity);
                            screen.SetPixelAtOffset(offset, color);
                            screen.EndTranslucentSprite();
                        }
                    }
                    else if (high)
                        screen.SetPixelAtOffset(offset, color);
                    else
                        screen.Pixels[offset] = color;
                }
            }
        }
    }

    private void Sprite(PixelScreenData screen, int index, int palette, double anchorX, double anchorY, bool flip,
        bool flash = false, int divisor = 1, bool softFlash = false, bool softenOutline = false, bool campfireWood = false,
        byte[] paletteOverride = null, bool keepTerrainShade = false)
    {
        if (index < 0 || index >= m_data.Frames.Length)
            return;
        var frame = m_data.Frames[index];
        var left = (int)Math.Round(flip ? anchorX - (frame.Ox + frame.Width) / (double)divisor : anchorX + frame.Ox / (double)divisor);
        var top = (int)Math.Round(anchorY + frame.Oy / (double)divisor);
        var spritePalette = paletteOverride ?? m_data.SpritePalette[palette];
        var sourceColors = softenOutline ? m_data.SpriteColors[palette] : null;
        var stride = m_data.SpriteStride;
        for (var y = Math.Max(0, -top); y < frame.Height / divisor && top + y < ArcadeData.Height; y++)
        for (var x = Math.Max(0, -left); x < frame.Width / divisor && left + x < ArcadeData.Width; x++)
        {
            var frameX = flip ? frame.Width - 1 - x * divisor : x * divisor;
            var frameY = y * divisor;
            var sourceOffset = (frame.Y + frameY) * stride + frame.X + frameX;
            var pen = m_data.SpritePens[sourceOffset];
            if (IsOpaquePen(pen))
            {
                var offset = (top + y) * ArcadeData.Width + left + x;
                if (campfireWood && pen <= m_campfireWood.Length)
                    screen.SetPixelAtOffset(offset, m_campfireWood[pen - 1], lightAttenuation: 225);
                else if (sourceColors != null && sourceColors[pen] == 0 && IsSilhouetteEdge(frame, frameX, frameY, sourceOffset))
                    screen.BlendBlackPixel(offset, 125);
                else
                    screen.SetPixelAtOffset(offset, flash && !softFlash ? m_data.White : spritePalette[pen], keepTerrainShade: keepTerrainShade);

                if (flash && softFlash)
                    screen.LightPixel(offset, 56);
            }
        }
    }

    private bool IsSilhouetteEdge(SpriteFrame frame, int x, int y, int offset)
    {
        if (x == 0 || x == frame.Width - 1 || y == 0 || y == frame.Height - 1)
            return true;

        var pens = m_data.SpritePens;
        var stride = m_data.SpriteStride;
        return !IsOpaquePen(pens[offset - 1]) || !IsOpaquePen(pens[offset + 1]) ||
               !IsOpaquePen(pens[offset - stride]) || !IsOpaquePen(pens[offset + stride]);
    }

    private static bool IsOpaquePen(byte pen) => pen is > 0 and < 15;

    private void Text(PixelScreenData screen, string text, int left, int top, int color = -1, int shadowColor = -1)
    {
        for (var i = 0; i < text.Length; i++)
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            var code = text[i];
            var pen = TilePen(code, x, y);
            if (pen != 0)
                screen.SetPixel(left + i * 8 + x, top + y, pen == 2 ? shadowColor < 0 ? m_data.Black : (byte)shadowColor : color < 0 ? m_data.White : (byte)color);
        }
    }

    private byte TilePen(int tile, int x, int y) =>
        m_data.TilePens[(tile / 128 * 8 + y) * m_data.TileStride + tile % 128 * 8 + x];
}
