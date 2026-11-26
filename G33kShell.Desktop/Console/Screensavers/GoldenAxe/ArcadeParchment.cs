// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// for any purpose. The software is provided as is, without warranty of any kind.

using System;
using System.IO;
using System.Linq;
using SkiaSharp;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>Composites parchment, timed ink tiles, tile-font text, and a moving quill.</summary>
internal sealed class ArcadeParchment : IArcadeMap
{
    internal const int TileSize = 8;
    internal const int FramesPerTile = 4;
    internal const double TicksPerSecond = 6293700.0 / (400 * 262);
    private const int ParchmentWidth = 160;
    private const int FirstReveal = 126;
    private const int AtlasColumns = 21;
    private const int FramesPerLetter = 3;
    private const int Black = unchecked((int)0xff000000);
    private static readonly int[] s_textColors = [Black, Black | 0x94a5d6, Black | 0x5a6b9c];

    internal sealed record TextLine(int FirstTick, int Y, string Text);

    private readonly int[] m_atlas;
    private readonly int m_atlasStride;
    private readonly (int X, int Y)[] m_inkTiles;
    private readonly (TextLine Line, int[] Pixels)[] m_text;
    private readonly SpriteFrame m_quill;
    private readonly int[] m_feather;
    private readonly Func<int, (int X, int Y)?> m_quillOrigin;
    private readonly int m_closeTick;
    private readonly int m_holdTick;
    private readonly double m_duration;
    private readonly int[] m_frame = new int[ArcadeData.Width * ArcadeData.Height];
    private int[] m_presentedFrame;
    private int[] m_shades;
    private int m_drawnTick = -1;
    private int m_presentedTick = -1;
    private int m_brightness = -1;

    private ArcadeParchment(ArcadeData data, SKBitmap atlas, int ticks, (int X, int Y)[] inkTiles,
        TextLine[] text, Func<int, (int X, int Y)?> quillOrigin, int quillEnd)
    {
        m_atlasStride = atlas.Width;
        m_atlas = Array.ConvertAll(atlas.Pixels, color => unchecked((int)(uint)color));
        m_inkTiles = inkTiles;
        m_text = Array.ConvertAll(text, line => (line, RenderText(data, line.Text)));
        m_quillOrigin = quillOrigin;
        m_duration = ticks / TicksPerSecond;
        m_closeTick = ticks - 60;
        var lastInk = FirstReveal + (inkTiles.Length - 1) * FramesPerTile;
        var lastLetter = text.Max(line => line.FirstTick + (line.Text.Length - 1) * FramesPerLetter);
        m_holdTick = Math.Max(quillEnd, Math.Max(lastInk, lastLetter));

        m_quill = data.Frames[1042];
        m_feather = new int[m_quill.Width * m_quill.Height];
        var colors = data.SpriteColors[0x80];
        for (var row = 0; row < m_quill.Height; row++)
        for (var column = 0; column < m_quill.Width; column++)
        {
            var pen = data.SpritePens[(m_quill.Y + row) * data.SpriteStride + m_quill.X + column];
            if (pen is > 0 and < 15)
                m_feather[row * m_quill.Width + column] = Black | colors[pen];
        }
    }

    internal static ArcadeParchment Load(ArcadeData data, string assetName, int ticks, (int X, int Y)[] inkTiles,
        TextLine[] text, Func<int, (int X, int Y)?> quillOrigin, int quillEnd)
    {
        using var atlas = SKBitmap.Decode(Path.Combine(ArcadeData.AssetDirectory, assetName));
        var inkRows = (inkTiles.Length + AtlasColumns - 1) / AtlasColumns;
        return atlas?.Width == AtlasColumns * TileSize && atlas.Height == ArcadeData.Height + inkRows * TileSize
            ? new ArcadeParchment(data, atlas, ticks, inkTiles, text, quillOrigin, quillEnd)
            : null;
    }

    private static int[] RenderText(ArcadeData data, string text)
    {
        var width = text.Length * TileSize;
        var pixels = new int[width * TileSize];
        for (var letter = 0; letter < text.Length; letter++)
        {
            var code = text[letter];
            for (var row = 0; row < TileSize; row++)
            for (var column = 0; column < TileSize; column++)
            {
                var index = (code / 128 * TileSize + row) * data.TileStride + code % 128 * TileSize + column;
                pixels[row * width + letter * TileSize + column] = s_textColors[data.TilePens[index]];
            }
        }
        return pixels;
    }

    public bool AdvanceTo(double seconds) => seconds < m_duration;

    public void Draw(PixelScreenData screen, int[] shades, double elapsedSeconds)
    {
        var tick = (int)Math.Floor(Math.Clamp(elapsedSeconds, 0, m_duration) * TicksPerSecond + 1e-9);
        if (tick >= m_holdTick && tick < m_closeTick)
            tick = m_holdTick;
        if (tick != m_drawnTick)
        {
            DrawFrame(tick);
            m_drawnTick = tick;
        }

        var brightness = (int)Math.Round(Math.Clamp((m_duration - elapsedSeconds) / ArcadeMapRecording.FadeSeconds, 0, 1) * 256);
        if (shades == null && brightness == 256)
        {
            screen.SetBgraFrame(m_frame);
            return;
        }

        m_presentedFrame ??= new int[m_frame.Length];
        if (tick != m_presentedTick || !ReferenceEquals(shades, m_shades) || brightness != m_brightness)
        {
            for (var i = 0; i < m_frame.Length; i++)
            {
                var pixel = shades == null ? m_frame[i] : ArcadeMapRecording.TintPixel(m_frame[i], shades);
                m_presentedFrame[i] = ArcadeMapRecording.FadePixel(pixel, brightness);
            }
            m_shades = shades;
            m_brightness = brightness;
            m_presentedTick = tick;
        }
        screen.SetBgraFrame(m_presentedFrame);
    }

    private void DrawFrame(int tick)
    {
        Array.Fill(m_frame, Black);
        if (tick < 2 || tick >= m_closeTick + 52)
            return;

        // The arcade clips the picture in eight-pixel steps around the screen center.
        var margin = tick < 54 ? 104 - (tick - 2) / 4 * 8 : tick >= m_closeTick ? 8 + (tick - m_closeTick) / 4 * 8 : 0;
        for (var y = margin; y < ArcadeData.Height - margin; y++)
            m_atlas.AsSpan(y * m_atlasStride + margin, ParchmentWidth - margin)
                .CopyTo(m_frame.AsSpan(y * ArcadeData.Width + margin));

        var revealed = tick < FirstReveal ? 0 : Math.Min((tick - FirstReveal) / FramesPerTile + 1, m_inkTiles.Length);
        for (var i = 0; i < revealed; i++)
        {
            var (tileX, tileY) = m_inkTiles[i];
            var x = tileX * TileSize;
            var y = tileY * TileSize;
            var left = Math.Max(x, margin);
            if (left >= x + TileSize)
                continue;
            var atlasX = i % AtlasColumns * TileSize;
            var atlasY = ArcadeData.Height + i / AtlasColumns * TileSize;
            for (var row = Math.Max(y, margin); row < Math.Min(y + TileSize, ArcadeData.Height - margin); row++)
            {
                var source = (atlasY + row - y) * m_atlasStride + atlasX + left - x;
                m_atlas.AsSpan(source, x + TileSize - left).CopyTo(m_frame.AsSpan(row * ArcadeData.Width + left));
            }
        }

        foreach (var (line, pixels) in m_text)
        {
            if (tick < line.FirstTick)
                continue;
            var letters = Math.Min((tick - line.FirstTick) / FramesPerLetter + 1, line.Text.Length);
            var width = Math.Min(letters * TileSize, ArcadeData.Width - margin - ParchmentWidth);
            for (var y = Math.Max(line.Y, margin); y < Math.Min(line.Y + TileSize, ArcadeData.Height - margin); y++)
                pixels.AsSpan((y - line.Y) * line.Text.Length * TileSize, width)
                    .CopyTo(m_frame.AsSpan(y * ArcadeData.Width + ParchmentWidth));
        }

        if (m_quillOrigin(tick) is { } origin)
            DrawQuill(origin.X, origin.Y);
    }

    private void DrawQuill(int x, int y)
    {
        for (var row = Math.Max(0, -y); row < Math.Min(m_quill.Height, ArcadeData.Height - y); row++)
        for (var column = Math.Max(0, -x); column < Math.Min(m_quill.Width, ArcadeData.Width - x); column++)
        {
            var pixel = m_feather[row * m_quill.Width + column];
            if (pixel != 0)
                m_frame[(y + row) * ArcadeData.Width + x + column] = pixel;
        }
    }

    public void Dispose() { }
}
