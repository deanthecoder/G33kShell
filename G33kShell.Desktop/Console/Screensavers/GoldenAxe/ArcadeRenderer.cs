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
/// Draws arcade tiles, priority layers and anchored sprites into the indexed pixel surface.
/// </summary>
/// <remarks>
/// All graphics use the original 320 by 224 coordinate space and a stable reduced palette.
/// </remarks>
internal sealed class ArcadeRenderer
{
    private readonly ArcadeData m_data;
    private readonly byte m_orange, m_red, m_darkRed, m_blue, m_lightBlue, m_yellow;

    public ArcadeRenderer(ArcadeData data)
    {
        m_data = data;
        m_orange = Color(255, 136, 0);
        m_red = Color(255, 68, 68);
        m_darkRed = Color(160, 0, 0);
        m_blue = Color(34, 34, 255);
        m_lightBlue = Color(120, 160, 255);
        m_yellow = Color(255, 255, 0);
    }

    public void Draw(PixelScreenData screen, Game game)
    {
        screen.Clear(m_data.Black);
        var u = -7 - game.CameraX;
        var v = (int)Math.Round(-512 - game.CameraY);
        var palette = m_data.TilePalettes[game.IsCamp ? "0" : game.Boss ? m_data.BossPalette : m_data.DayPalette];
        Layer(screen, m_data.Background, (int)Math.Floor(u * m_data.Parallax[0][0] + m_data.Parallax[0][1]), (int)(v * m_data.Parallax[1][0] + m_data.Parallax[1][1]), palette, false, true);
        Layer(screen, m_data.Foreground, (int)u, v, palette, false, false);
        DrawFeathers(screen, game, foreground: false);
        foreach (var actor in game.Actors.OrderBy(a => a.Z).ThenByDescending(a => a.Y))
        {
            if (actor.Mount != null || actor.State is FighterState.Gone or FighterState.Respawning || (actor.Invulnerable > 0 || actor.State == FighterState.Dying) && game.TickCount % 4 < 2)
                continue;
            var step = actor.Animation.Current;
            var flip = (step.Flags & 1) != 0 ^ actor.Facing < 0;
            if (actor.Kind == ActorKind.Fireball && actor.State != FighterState.Dying)
            {
                var shadow = game.Project(actor.X, actor.Z + 8, game.Ground(actor.X, actor.Z));
                Sprite(screen, step.Frame, 0, shadow.X + step.Dx * actor.Facing, shadow.Y, flip);
            }
            var point = game.Project(actor.X, actor.Z, actor.Y);
            Sprite(screen, step.Frame, actor.Palette, point.X + step.Dx * actor.Facing, point.Y + step.Dy, flip, actor.Flash > 0);
            if (actor.Rider != null && m_data.Riders.TryGetValue(actor.Animation.Address, out var riders))
            {
                var rider = riders[Math.Min(actor.Animation.Index, riders.Length - 1)];
                // Rider offsets are relative to the displayed beast, including its current pose offset.
                Sprite(screen, rider[0], actor.Rider.Palette,
                    point.X + (step.Dx + rider[2]) * actor.Facing, point.Y + step.Dy + rider[3],
                    (rider[1] != 0) ^ actor.Facing < 0);
            }
        }
        if (game.MagicTick >= 0 && game.MagicTick < game.MagicEffects[game.MagicLevel - 1].Length)
        foreach (var effect in game.MagicEffects[game.MagicLevel - 1][game.MagicTick])
        {
            var point = game.Project(game.Hero.X + effect[3], game.Hero.Z + effect[4], game.Hero.Y + effect[5]);
            Sprite(screen, effect[0], effect[1], point.X, point.Y, effect[2] == 1);
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

    private void DrawFeathers(PixelScreenData screen, Game game, bool foreground)
    {
        if (!EagleFeathers.IsVisible(game))
            return;
        var first = foreground ? EagleFeathers.BackgroundCount : 0;
        var end = foreground ? EagleFeathers.Count : EagleFeathers.BackgroundCount;
        for (var i = first; i < end; i++)
        {
            var feather = EagleFeathers.Sample(i, game.TickCount, game.CameraX);
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

    private byte Color(int red, int green, int blue) => (byte)Enumerable.Range(0, m_data.Palette.Length)
        .OrderBy(i => Math.Pow(m_data.Palette[i].R - red, 2) + Math.Pow(m_data.Palette[i].G - green, 2) + Math.Pow(m_data.Palette[i].B - blue, 2)).First();

    private void DrawHud(PixelScreenData screen, Game game)
    {
        Text(screen, $"STAGE {m_data.StageNumber + (game.IsCamp ? 1 : 0)}", 16, 16, m_orange);
        Text(screen, "MAGIC", 104, 2, m_red);
        var potionIndex = m_data.Animations[0x1870a][0].Frame;
        var potion = m_data.Frames[potionIndex];
        for (var i = 0; i < game.Character.MagicLevels; i++)
        {
            Fill(screen, 104 + i * 10, 12, 7, 10, m_darkRed);
            if (i < game.Potions)
                Sprite(screen, potionIndex, 0x4a,
                    104 + i * 10 + (7 - potion.Width / 2) / 2 - potion.Ox / 2.0,
                    12 + (10 - potion.Height / 2) / 2 - potion.Oy / 2.0, false, divisor: 2);
        }
        for (var bar = 0; bar < 3; bar++)
        {
            var amount = Math.Clamp(game.Hero.Health - bar * 16, 0, 16);
            Fill(screen, 56 + bar * 24, 205, 22, 8, m_data.Black);
            Fill(screen, 57 + bar * 24, 206, amount * 20 / 16, 6, m_blue);
            Fill(screen, 57 + bar * 24, 206, amount * 20 / 16, 1, m_lightBlue);
        }
        Text(screen, Math.Max(0, game.Lives).ToString(), 132, 205, m_yellow);
        Sprite(screen, game.Character.Portrait, game.Character.Palette, 155, 214, false, divisor: 2);
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
                    screen.Pixels[sy * ArcadeData.Width + sx] = palette[(cell >> 16 & 127) * 8 + pen];
            }
        }
    }

    private void Sprite(PixelScreenData screen, int index, int palette, double anchorX, double anchorY, bool flip, bool flash = false, int divisor = 1)
    {
        if (index < 0 || index >= m_data.Frames.Length)
            return;
        var frame = m_data.Frames[index];
        var left = (int)Math.Round(flip ? anchorX - (frame.Ox + frame.Width) / (double)divisor : anchorX + frame.Ox / (double)divisor);
        var top = (int)Math.Round(anchorY + frame.Oy / (double)divisor);
        for (var y = Math.Max(0, -top); y < frame.Height / divisor && top + y < ArcadeData.Height; y++)
        for (var x = Math.Max(0, -left); x < frame.Width / divisor && left + x < ArcadeData.Width; x++)
        {
            var pen = m_data.SpritePens[(frame.Y + y * divisor) * m_data.SpriteStride + frame.X + (flip ? frame.Width - 1 - x * divisor : x * divisor)];
            if (pen is > 0 and < 15)
                screen.Pixels[(top + y) * ArcadeData.Width + left + x] = flash ? m_data.White : m_data.SpritePalette[palette][pen];
        }
    }

    private void Text(PixelScreenData screen, string text, int left, int top, int color = -1)
    {
        for (var i = 0; i < text.Length; i++)
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            var code = text[i];
            var pen = m_data.TilePens[(code / 128 * 8 + y) * m_data.TileStride + code % 128 * 8 + x];
            if (pen != 0)
                screen.SetPixel(left + i * 8 + x, top + y, pen == 2 ? m_data.Black : color < 0 ? m_data.White : (byte)color);
        }
    }
}
