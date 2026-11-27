// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// for any purpose. The software is provided as is, without warranty of any kind.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DTC.Core;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>Plays the set-6 attract title using its original letters and scenery.</summary>
internal sealed class AnimatedTitle
{
    private const int FirstTick = 384;
    internal const int DurationTicks = 944 - FirstTick;
    private const int Width = ArcadeData.Width, Height = ArcadeData.Height;
    private static readonly int[] s_letters = [1046, 1047, 1048, 1049, 1050, 1051, 1052, 1053, 1050];
    private static readonly int[] s_centers = [39, 67, 96, 124, 153, 181, 224, 251, 279];
    private const int FadeStart = 598, FadeTicks = 30, LogoTop = 145, LogoHeight = 32;
    private readonly byte[] m_background = new byte[Width * LogoHeight];
    private readonly Dictionary<(byte From, byte To, double Opacity), byte> m_blendPens = new();
    private readonly int m_firstBlendPen;
    private readonly ArcadeData m_data;
    private readonly byte[] m_layers, m_letters, m_splat, m_logo;
    private readonly byte m_key, m_black;
    private readonly (byte Light, byte Dark)[] m_sky;
    internal Rgb[] Palette { get; }

    internal static AnimatedTitle Load(ArcadeData data)
    {
        var layers = ArcadeTitleCard.TryLoad(Path.Combine(ArcadeData.AssetDirectory, "title-layers.png"), 256, 512);
        return layers == null ? null : new AnimatedTitle(data, layers);
    }

    private AnimatedTitle(ArcadeData data, ArcadeTitleCard layers)
    {
        m_data = data;
        m_layers = layers.Pixels;
        var colors = layers.Palette.Take(m_layers.Max() + 1).ToList();
        byte Pen(Rgb rgb)
        {
            var index = colors.IndexOf(rgb);
            if (index >= 0)
                return (byte)index;
            colors.Add(rgb);
            return checked((byte)(colors.Count - 1));
        }
        byte[] SpritePens(int palette) => data.SpriteColors[palette].Select(c =>
            Pen(new Rgb((byte)(c >> 16), (byte)(c >> 8), (byte)c))).ToArray();

        m_key = Pen(new Rgb(255, 0, 255));
        m_black = Pen(Rgb.Black);
        m_letters = SpritePens(0x81);
        m_splat = SpritePens(0x48);
        m_logo = [m_key, Pen(new Rgb(214, 0, 0))];
        var skyPens = m_layers.Take(256 * Width).Distinct().OrderBy(i => colors[i].R).ToArray();
        int[] darkRed = [8, 16, 25, 33, 41, 49, 58, 66];
        m_sky = skyPens.Select((pen, i) => (pen, Pen(new Rgb((byte)darkRed[i],
            (byte)(colors[pen].G - 107), (byte)(colors[pen].B - 107))))).ToArray();
        Palette = Enumerable.Repeat(Rgb.Black, 256).ToArray();
        colors.CopyTo(Palette);
        m_firstBlendPen = colors.Count;
    }

    internal void Draw(PixelScreenData screen, Rgb[] palette, int elapsed)
    {
        var tick = FirstTick + elapsed;
        m_blendPens.Clear();
        screen.Clear(m_black);
        screen.SetPalette(palette);
        var dusk = Math.Clamp((tick - 590) / 30.0, 0, 1);
        foreach (var (light, dark) in m_sky)
        {
            var a = palette[light];
            var b = palette[dark];
            screen.Palette[light] = dusk <= 0 ? a : dusk >= 1 ? b : new Rgb(
                (byte)(a.R + (b.R - a.R) * dusk), (byte)(a.G + (b.G - a.G) * dusk), (byte)(a.B + (b.B - a.B) * dusk));
        }

        var scroll = tick < 560 ? (tick - 400) * 1.6 :
            tick < 640 ? (tick - 560) * (207 / 80.0) : 207 + (tick - 640) * 0.5;
        for (var y = 0; y < Height; y++)
            Array.Copy(m_layers, ((y + (int)Math.Round(scroll)) & 255) * Width, screen.Pixels, y * Width, Width);

        if (tick <= FadeStart)
            DrawLetters(screen, tick, behind: true);
        Layer(screen, 256, Height, Math.Max(0, 493 - tick));
        if (tick <= FadeStart)
            DrawLetters(screen, tick, behind: false);
        else if (tick < FadeStart + FadeTicks)
            CrossfadeTitle(screen, tick);
        else
            Sprite(screen, 1127, m_logo, 17, LogoTop, 287, LogoHeight);
        if (tick >= 596)
            Sprite(screen, 959, m_splat, 146, 14, 31, 32, FadeOpacity(tick - 596));
        if (tick >= 620)
            Layer(screen, 480, 32, 192, FadeOpacity(tick - 620));

        var inset = Math.Clamp((tick - 864) * 2, 0, Width / 2);
        if (inset == 0)
            return;
        for (var y = 0; y < Height; y++)
        {
            var row = screen.Pixels.AsSpan(y * Width, Width);
            if (y < inset * Height / Width || y >= Height - inset * Height / Width)
                row.Fill(m_black);
            else
            {
                row[..inset].Fill(m_black);
                row[(Width - inset)..].Fill(m_black);
            }
        }
    }

    private void CrossfadeTitle(PixelScreenData screen, int tick)
    {
        // All nine letters have settled into the wordmark's rows by FadeStart.
        var pixels = screen.Pixels.AsSpan(LogoTop * Width, m_background.Length);
        pixels.CopyTo(m_background);
        DrawLetters(screen, tick, behind: false);

        var logo = m_data.Frames[1127];
        var progress = FadeOpacity(tick - FadeStart);
        for (var y = 0; y < LogoHeight; y++)
        for (var x = 0; x < Width; x++)
        {
            var index = y * Width + x;
            var from = pixels[index];
            var to = m_background[index];
            var logoX = x - 17;
            if (logoX >= 0 && logoX < logo.Width)
            {
                var pen = m_data.SpritePens[(logo.Y + y) * m_data.SpriteStride + logo.X + logoX];
                if (pen is > 0 and < 15)
                    to = m_logo[pen];
            }
            pixels[index] = Blend(screen, from, to, progress);
        }
    }

    private static double FadeOpacity(int elapsed)
    {
        var t = Math.Clamp(elapsed / (double)FadeTicks, 0, 1);
        return t * t * (3 - 2 * t);
    }

    private byte Blend(PixelScreenData screen, byte from, byte to, double opacity)
    {
        if (opacity >= 1 || from == to)
            return to;
        if (opacity <= 0)
            return from;
        var key = (from, to, opacity);
        if (m_blendPens.TryGetValue(key, out var blended))
            return blended;
        blended = checked((byte)(m_firstBlendPen + m_blendPens.Count));
        m_blendPens.Add(key, blended);
        var a = screen.Palette[from];
        var b = screen.Palette[to];
        screen.Palette[blended] = new Rgb(
            (byte)Math.Round(a.R + (b.R - a.R) * opacity),
            (byte)Math.Round(a.G + (b.G - a.G) * opacity),
            (byte)Math.Round(a.B + (b.B - a.B) * opacity));
        return blended;
    }

    private void DrawLetters(PixelScreenData screen, int tick, bool behind)
    {
        const double orbitRadius = 122, orbitCenterY = 72, baseScale = 0.69;
        Span<(double Depth, int Frame, int Left, int Top, int Width, int Height)> letters = stackalloc
            (double, int, int, int, int, int)[s_letters.Length];
        var count = 0;
        for (var i = 0; i < s_letters.Length; i++)
        {
            var age = tick - FirstTick - i * 7 - (i >= 6 ? 2 : 0);
            if (age < 0)
                continue;
            var frame = s_letters[i];
            var angle = (Math.Min(age, 112) - 16) * -0.103;
            var depth = Math.Sin(angle);
            // Project the tilted ring and the glyphs with the same perspective.
            // At the rear a letter is about half the height it has at the front.
            var perspective = 1 / (1 - 0.31 * depth);
            var x = Width / 2.0 - orbitRadius * Math.Cos(angle) * perspective;
            var centerY = (orbitCenterY + 21 * depth) * perspective;
            var sprite = m_data.Frames[frame];
            var scale = baseScale * perspective;
            var settle = Math.Clamp((age - 112) / 44.0, 0, 1);
            settle *= 2 - settle;
            var height = sprite.Height * scale;
            var top = centerY - height / 2;
            if (age < 16)
            {
                var entry = (16 - age) / 16.0;
                scale = baseScale;
                height = sprite.Height * scale;
                var entryEndX = Width / 2.0 - orbitRadius;
                x = entryEndX + (Width - 15 - entryEndX) * Math.Pow(entry, 1.3);
                top = orbitCenterY - height / 2;
                top += (Height - height - top) * (1 - Math.Pow(1 - entry, 2));
            }
            if (behind != (age >= 16 && depth < 0 && settle < 0.45))
                continue;
            x += (s_centers[i] - x) * settle;
            top += (LogoTop - top) * settle;
            var width = (int)Math.Round(sprite.Width * (scale + (0.51 - scale) * settle));
            letters[count++] = (age < 16 ? 1 : depth * (1 - settle), frame, (int)Math.Round(x - width * 0.5),
                (int)Math.Round(top), width, (int)Math.Round(height + (LogoHeight - height) * settle));
        }

        var visible = letters[..count];
        visible.Sort(static (a, b) => a.Depth.CompareTo(b.Depth));
        foreach (var (_, frame, left, top, width, height) in visible)
            Sprite(screen, frame, m_letters, left, top, width, height);
    }

    private void Layer(PixelScreenData screen, int sourceY, int height, int top, double opacity = 1)
    {
        for (var y = 0; y < height && top + y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var pen = m_layers[(sourceY + y) * Width + x];
            if (pen != m_key)
            {
                var index = (top + y) * Width + x;
                screen.Pixels[index] = opacity >= 1 ? pen : Blend(screen, screen.Pixels[index], pen, opacity);
            }
        }
    }

    private void Sprite(PixelScreenData screen, int id, byte[] palette, int left, int top, int width, int height, double opacity = 1)
    {
        var frame = m_data.Frames[id];
        for (var y = Math.Max(0, -top); y < height && top + y < Height; y++)
        for (var x = Math.Max(0, -left); x < width && left + x < Width; x++)
        {
            var source = (frame.Y + y * frame.Height / height) * m_data.SpriteStride + frame.X + x * frame.Width / width;
            var pen = m_data.SpritePens[source];
            if (pen is > 0 and < 15)
            {
                var index = (top + y) * Width + left + x;
                screen.Pixels[index] = opacity >= 1 ? palette[pen] : Blend(screen, screen.Pixels[index], palette[pen], opacity);
            }
        }
    }
}
