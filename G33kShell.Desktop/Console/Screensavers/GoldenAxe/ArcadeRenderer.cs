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
    private readonly ArcadeData m_data;
    private readonly PoseFootprint?[] m_footprints;
    private readonly SunShadow[,] m_sunShadows;
    private readonly byte m_orange, m_darkRed, m_blue, m_lightBlue, m_yellow;
    private readonly byte m_emberCore;
    private readonly byte[] m_campfireWood;
    private TerrainShading m_terrainShading;
    private const byte DimMagicOpacity = 115;
    private const double ShadowSkew = 0.55;
    private const double ShadowSquash = 0.28;

    private enum EllipseEffect { Fire, Smoke, Magic, Dust }

    public ArcadeRenderer(ArcadeData data)
    {
        m_data = data;
        m_footprints = new PoseFootprint?[data.Frames.Length];
        m_sunShadows = new SunShadow[data.Frames.Length, 2];
        m_orange = Color(255, 136, 0);
        m_darkRed = Color(160, 0, 0);
        m_blue = Color(34, 34, 255);
        m_lightBlue = Color(120, 160, 255);
        m_yellow = Color(255, 255, 0);
        m_emberCore = Color(255, 202, 94);
        m_campfireWood = [Color(53, 31, 18), Color(79, 46, 24), Color(110, 64, 31)];
    }

    public void Draw(PixelScreenData screen, Game game, bool effects = false, Rgb tint = null, byte effectOpacity = 255,
        bool showTerrainShade = false)
    {
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
        var backgroundX = (int)Math.Floor(u * m_data.Parallax[0][0] + m_data.Parallax[0][1]);
        var backgroundY = (int)(v * m_data.Parallax[1][0] + m_data.Parallax[1][1]);
        Layer(screen, m_data.Background, backgroundX, backgroundY, palette, false, true);
        Layer(screen, m_data.Foreground, (int)u, v, palette, false, false);
        if (SceneryEyes.Sample(game.AreaIndex, game.IsCamp, game.TickCount) is { } eye)
        {
            var frame = m_data.Frames[eye.Frame];
            Sprite(screen, eye.Frame, eye.Palette, eye.X - (int)u - frame.Ox, eye.Y - v - frame.Oy, false);
        }
        if (showTerrainShade || effects && !game.IsCamp && effectOpacity != 0)
        {
            m_terrainShading ??= new TerrainShading(m_data);
            m_terrainShading.Draw(screen, (int)u - 1, v + 512, backgroundX, backgroundY, palette, effectOpacity,
                showTerrainShade, game.IsCamp);
            if (showTerrainShade)
                return;
        }
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
            if (game.IsCamp)
                DrawCampfireShadows(screen, game);
        }
        DrawFeathers(screen, game, foreground: false);
        foreach (var mound in game.SkeletonMounds)
        {
            var point = game.ProjectView(mound.X, mound.Z, game.Ground(mound.X, mound.Z));
            Sprite(screen, SkeletonEmergence.MoundFrame, SkeletonEmergence.GroundPalette, point.X, point.Y, false);
        }
        foreach (var mound in game.OpenedSkeletonMounds)
        {
            var point = game.ProjectView(mound.X, mound.Z, game.Ground(mound.X, mound.Z));
            Sprite(screen, SkeletonEmergence.OpenFrame, SkeletonEmergence.GroundPalette, point.X, point.Y, false);
        }
        foreach (var actor in game.Actors.Concat(game.StoneBodies).OrderBy(a => a.Z).ThenByDescending(a => a.Y))
        {
            if (actor.Mount != null || actor.State is FighterState.Gone or FighterState.Respawning ||
                actor.State == FighterState.Dying && game.TickCount % 4 < 2)
                continue;
            if (actor.State == FighterState.Emerging)
            {
                var ground = game.ProjectView(actor.X, actor.Z, actor.Y);
                Sprite(screen, SkeletonEmergence.GroundFrame(actor.StateTicks), SkeletonEmergence.GroundPalette,
                    ground.X, ground.Y, false);
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
            var translucent = pulse != 0;
            if (translucent)
                screen.BeginTranslucentSprite((byte)(255 - 18 * Math.Min(pulse, 12 - pulse)));
            Sprite(screen, step.Frame, actor.Palette, point.X + step.Dx * actor.Facing, point.Y + step.Dy, flip,
                flash: actor.Flash > 0, softFlash: effects, softenOutline: effects && IsCharacter(actor),
                campfireWood: effects && actor.Kind == ActorKind.Campfire);
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
            if (actor.Rider != null && m_data.Riders.TryGetValue(actor.Animation.Address, out var riders))
            {
                var rider = riders[Math.Min(actor.Animation.Index, riders.Length - 1)];
                // Rider offsets are relative to the displayed beast, including its current pose offset.
                Sprite(screen, rider[0], actor.Rider.Palette,
                    point.X + (step.Dx + rider[2]) * actor.Facing, point.Y + step.Dy + rider[3],
                    (rider[1] != 0) ^ actor.Facing < 0, softenOutline: effects);
            }
            if (translucent)
                screen.EndTranslucentSprite();
            if (effects && actor.Kind == ActorKind.Campfire)
            {
                DrawCampfireSmoke(screen, point.X, point.Y, game.TickCount);
                DrawCampfireEmbers(screen, point.X, point.Y, game.TickCount);
            }
        }
        if (game.MagicTick >= 0 && game.MagicTick < game.ActiveMagicEffects.Length)
            DrawMagic(screen, game, effects, tint, effectOpacity);
        if (effects)
        {
            DrawFireLighting(screen, game);
            DrawLandingDust(screen, game, dustTints);
        }
        Layer(screen, m_data.Foreground, (int)u, v, palette, true, false);
        DrawFeathers(screen, game, foreground: true);
        DrawHud(screen, game);
        if (game.CanAdvance && game.MagicTick < 0 && game.TickCount % 60 < 30)
            DrawGoPrompt(screen);
        if (game.Complete || game.GameOver)
        {
            var message = game.GameOver ? "GAME OVER" : game.IsCamp ? "DAYBREAK" : game.AreaIndex == ArcadeData.AreaCount - 1 ? "YOU WIN" : "STAGE CLEAR";
            Text(screen, message, (ArcadeData.Width - message.Length * 8) / 2, 96);
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
        var intensity = Math.Min(1, frame.Width / 42.0);
        DrawSoftEllipse(screen, centerX, centerY, frame.Width * 0.7, frame.Height * 0.75, 75 * intensity);

        var ground = game.Ground(flame.X + offsetX, flame.Z);
        if (ground is <= 3840 or >= 5248)
            return;
        var foot = game.ProjectView(flame.X, flame.Z, ground);
        DrawSoftEllipse(screen, centerX, foot.Y + 1, frame.Width * 0.95, 21, 86 * intensity);
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
        game.ProjectView(game.Hero.X + effect[3], game.Hero.Z + effect[4], game.Hero.Y + effect[5]);

    private void DrawMagic(PixelScreenData screen, Game game, bool effects, Rgb tint, byte effectOpacity)
    {
        var sprites = game.ActiveMagicEffects[game.MagicTick];
        var dim = DimMagicSprites(game.ActiveMagicEffects, game.MagicTick).ToArray();
        if (effects)
            DrawMagicLighting(screen, game, sprites, dim, tint, effectOpacity);

        if (dim.Length > 0)
        {
            screen.BeginTranslucentSprite(DimMagicOpacity);
            foreach (var effect in dim)
                DrawMagicSprite(screen, game, effect);
            screen.EndTranslucentSprite();
        }
        foreach (var effect in sprites)
            DrawMagicSprite(screen, game, effect);
    }

    private void DrawMagicSprite(PixelScreenData screen, Game game, int[] effect)
    {
        var point = ProjectMagic(game, effect);
        Sprite(screen, effect[0], effect[1], point.X, point.Y, effect[2] == 1);
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
            DrawMagicGlow(screen, game, effect, DimMagicOpacity / 255.0);
    }

    private void DrawMagicGlow(PixelScreenData screen, Game game, int[] effect, double opacity)
    {
        // The potion icons animate during a cast but are not light sources.
        if (effect[1] == 0x4a)
            return;

        var frame = m_data.Frames[effect[0]];
        var point = ProjectMagic(game, effect);
        var direction = effect[2] == 1 ? -1 : 1;
        var centerX = point.X + (frame.Ox + frame.Width / 2.0) * direction;
        var centerY = point.Y + frame.Oy + frame.Height / 2.0;
        var strength = Math.Clamp(36 + frame.Width * 0.85, 46, 91) * opacity;
        DrawSoftEllipse(screen, centerX, centerY,
            24 + frame.Width * 0.64, 16 + frame.Height * 0.66, strength, EllipseEffect.Magic);
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
            tints[i] = Color(56 + red * 199 / (255 * count), 56 + green * 199 / (255 * count), 56 + blue * 199 / (255 * count));
        }
        return tints;
    }

    private static void DrawLandingDust(PixelScreenData screen, Game game, byte[] tints)
    {
        for (var i = 0; i < tints.Length; i++)
        {
            var puff = game.LandingDust[i];
            var foot = game.Project(puff.X, puff.Z, puff.Y);
            var progress = (game.TickCount - puff.Tick) / (double)Game.LandingDustTicks;
            var remaining = 1 - progress;
            var opacity = (puff.Heavy ? 154 : 94) * remaining * Math.Sqrt(remaining);
            var spread = puff.Spread + (1 - remaining * remaining) * (puff.Heavy ? 15 : 10);
            var y = foot.Y + (puff.Heavy ? 3 : 1) - progress * (puff.Heavy ? 26 : 20);
            var radiusX = (puff.Heavy ? 10 : 6) + 8 * progress;
            var radiusY = (puff.Heavy ? 4 : 3) + 4 * progress;
            var tint = tints[i];
            DrawSoftEllipse(screen, foot.X - spread, y, radiusX, radiusY, opacity, EllipseEffect.Dust, tint: tint);
            DrawSoftEllipse(screen, foot.X + spread, y - 1, radiusX, radiusY, opacity * 0.85, EllipseEffect.Dust, tint: tint);
            DrawSoftEllipse(screen, foot.X - spread * 0.3, y + 1, radiusX * 0.7, radiusY * 0.9,
                opacity * 0.67, EllipseEffect.Dust, tint: tint);
            DrawSoftEllipse(screen, foot.X + spread * 0.28, y - 2 - progress * 3, radiusX * 0.6, radiusY * 0.8,
                opacity * 0.55, EllipseEffect.Dust, tint: tint);
        }
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
            DrawSoftEllipse(screen, centerX, centerY, 4 + 12 * progress, 6 + 9 * progress, opacity, EllipseEffect.Smoke);
            DrawSoftEllipse(screen, centerX - 3, centerY + 2, 3 + 9 * progress, 5 + 7 * progress, opacity * 0.45, EllipseEffect.Smoke);
        }
    }

    private static void DrawSoftEllipse(PixelScreenData screen, double centerX, double centerY,
        double radiusX, double radiusY, double strength, EllipseEffect effect = EllipseEffect.Fire, bool occludedByGround = false, byte tint = 0)
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
                        screen.SmokePixel(offset, opacity);
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
            if (actor.Mount != null || actor.State is FighterState.Gone or FighterState.Respawning ||
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
            var strength = 100 / (1 + height / 36);
            var validPose = (uint)step.Frame < (uint)m_data.Frames.Length;
            if (!potion && actor.State is FighterState.Down or FighterState.Dying or FighterState.Petrified && validPose)
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
            if (actor.Mount != null || actor.State is FighterState.Gone or FighterState.Respawning || !IsCharacter(actor))
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
                (1 + Math.Max(0, ground - actor.Y) / 22);
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
                var opacity = (byte)(strength * edge * edge * Math.Min(1, progress * 10) * (1 - progress * progress) * terrain);
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

    private void DrawFeathers(PixelScreenData screen, Game game, bool foreground)
    {
        if (!EagleFeathers.IsVisible(game))
            return;
        var first = foreground ? EagleFeathers.BackgroundCount : 0;
        var end = foreground ? EagleFeathers.Count : EagleFeathers.BackgroundCount;
        for (var i = first; i < end; i++)
        {
            var feather = EagleFeathers.Sample(i, game.TickCount, game.ViewCamera.X);
            Sprite(screen, feather.Frame, EagleFeathers.Palette, feather.X, feather.Y, feather.Flip);
        }
    }

    private void DrawGoPrompt(PixelScreenData screen)
    {
        Text(screen, "GO", 264, 48, m_yellow);
        Fill(screen, 282, 51, 18, 3, m_yellow);
        for (var row = -6; row <= 6; row++)
            Fill(screen, 299, 52 + row, 7 - Math.Abs(row), 1, m_yellow);
    }

    private byte Color(int red, int green, int blue) => (byte)Enumerable.Range(0, m_data.StaticPaletteCount)
        .OrderBy(i => Math.Pow(m_data.Palette[i].R - red, 2) + Math.Pow(m_data.Palette[i].G - green, 2) + Math.Pow(m_data.Palette[i].B - blue, 2)).First();

    private void DrawHud(PixelScreenData screen, Game game)
    {
        Text(screen, $"STAGE {m_data.StageNumber + (game.IsCamp ? 1 : 0)}", 16, 16, m_orange);
        DrawMagicMeter(screen, game);
        for (var bar = 0; bar < 3; bar++)
        {
            var amount = Math.Clamp(game.Hero.Health - bar * 16, 0, 16);
            Fill(screen, 56 + bar * 24, 205, 22, 8, m_data.Black);
            Fill(screen, 57 + bar * 24, 206, amount * 20 / 16, 6, m_blue);
            Fill(screen, 57 + bar * 24, 206, amount * 20 / 16, 1, m_lightBlue);
        }
        Text(screen, Math.Max(0, game.Lives).ToString(), 128, 205, m_yellow);
        DrawHudPortrait(screen, game.Character.PortraitTile);
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
        const int top = 2;
        const int slotWidth = 10;
        const int height = 18;
        var hero = game.Character;
        Fill(screen, left, top, hero.PotionCapacity * slotWidth + 1, height, m_data.White);
        for (var tier = 0; tier < hero.MagicLevels; tier++)
        {
            var start = hero.MagicThresholds[tier] - 1;
            var end = tier + 1 < hero.MagicLevels ? hero.MagicThresholds[tier + 1] - 1 : hero.PotionCapacity;
            var x = left + start * slotWidth + 1;
            Fill(screen, x, top + 1, (end - start) * slotWidth - 1, height - 2, m_darkRed);
        }

        var potionIndex = m_data.Animations[0x1870a][0].Frame;
        var potion = m_data.Frames[potionIndex];
        for (var slot = 0; slot < game.Potions; slot++)
            Sprite(screen, potionIndex, 0x4a,
                left + slot * slotWidth + (slotWidth + 1 - potion.Width / 2) / 2 - potion.Ox / 2.0,
                top + height - 1 - potion.Height / 2 - potion.Oy / 2.0, false, divisor: 2);

        for (var tier = 0; tier < hero.MagicLevels; tier++)
            Text(screen, (tier + 1).ToString(), left + (hero.MagicThresholds[tier] - 1) * slotWidth + 1, top + 1, m_yellow);
    }

    private static void Fill(PixelScreenData screen, int left, int top, int width, int height, byte color)
    {
        for (var y = top; y < top + height; y++)
        for (var x = left; x < left + width; x++)
            screen.SetPixel(x, y, color);
    }
    private void Layer(PixelScreenData screen, TileLayer layer, int u, int v, byte[] palette, bool high, bool opaque)
    {
        for (var sy = 0; sy < ArcadeData.Height; sy++)
        {
            var y = sy + v - layer.Y;
            if (y < 0 || y >= layer.Height * 8)
                continue;
            for (var sx = 0; sx < ArcadeData.Width; sx++)
            {
                var x = sx + u - layer.X;
                if (x < 0 || x >= layer.Width * 8)
                    continue;
                var cell = layer.Cells[y / 8 * layer.Width + x / 8];
                if (cell == uint.MaxValue || !opaque && ((cell >> 23 & 1) != 0) != high)
                    continue;
                var tile = (int)(cell & 65535);
                var pen = m_data.TilePens[(tile / 128 * 8 + y % 8) * m_data.TileStride + tile % 128 * 8 + x % 8];
                if (pen != 0 || opaque)
                {
                    var offset = sy * ArcadeData.Width + sx;
                    var color = palette[(cell >> 16 & 127) * 8 + pen];
                    if (high)
                        screen.SetPixelAtOffset(offset, color);
                    else
                        screen.Pixels[offset] = color;
                }
            }
        }
    }

    private void Sprite(PixelScreenData screen, int index, int palette, double anchorX, double anchorY, bool flip,
        bool flash = false, int divisor = 1, bool softFlash = false, bool softenOutline = false, bool campfireWood = false,
        byte[] paletteOverride = null)
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
                    screen.SetPixelAtOffset(offset, flash && !softFlash ? m_data.White : spritePalette[pen]);

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

    private void Text(PixelScreenData screen, string text, int left, int top, int color = -1)
    {
        for (var i = 0; i < text.Length; i++)
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            var code = text[i];
            var pen = TilePen(code, x, y);
            if (pen != 0)
                screen.SetPixel(left + i * 8 + x, top + y, pen == 2 ? m_data.Black : color < 0 ? m_data.White : (byte)color);
        }
    }

    private byte TilePen(int tile, int x, int y) =>
        m_data.TilePens[(tile / 128 * 8 + y) * m_data.TileStride + tile % 128 * 8 + x];
}
