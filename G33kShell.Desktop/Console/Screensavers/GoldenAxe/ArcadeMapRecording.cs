// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// for any purpose. The software is provided as is, without warranty of any kind.

using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using DTC.Core;
using DTC.Core.Extensions;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>Plays a local map recording using its original frame timestamps.</summary>
internal sealed class ArcadeMapRecording : IArcadeMap
{
    private const int PixelCount = ArcadeData.Width * ArcadeData.Height;
    private const int PaletteSize = 256;
    private const int OpaqueBlack = unchecked((int)0xff000000);
    internal const double FadeSeconds = 0.5;
    private readonly BinaryReader m_reader;
    private readonly byte[] m_previous;
    private readonly byte[] m_delta;
    private readonly int[] m_palette;
    private readonly bool m_hasScanlineBounds;
    private readonly int[] m_frame = new int[PixelCount];
    private uint m_frameEndsAt;
    private double m_duration;
    private int[] m_mapTint;
    private int[] m_tintedFrame;
    private int[] m_fadedFrame;

    private ArcadeMapRecording(BinaryReader reader, bool legacy, bool hasScanlineBounds)
    {
        m_reader = reader;
        m_hasScanlineBounds = hasScanlineBounds;
        m_delta = new byte[legacy ? PixelCount * 3 : PixelCount];
        m_previous = new byte[m_delta.Length];
        if (legacy)
            return;

        Span<byte> palette = stackalloc byte[PaletteSize * 3];
        m_reader.BaseStream.ReadExactly(palette);
        m_palette = new int[PaletteSize];
        for (var i = 0; i < PaletteSize; i++)
            m_palette[i] = PackBgra(palette[i * 3], palette[i * 3 + 1], palette[i * 3 + 2]);
        Array.Fill(m_frame, m_palette[0]);
    }

    internal static ArcadeMapRecording TryLoad(string path)
    {
        try
        {
            var data = File.ReadAllBytes(path);
            using var probe = Open(data);
            var duration = probe?.ReadDuration() ?? 0;
            if (duration == 0)
                return null;

            var recording = Open(data);
            if (recording.ReadNextFrame())
            {
                recording.m_duration = duration / 1_000_000.0;
                return recording;
            }
            recording.Dispose();
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            return null;
        }
    }

    private static ArcadeMapRecording Open(byte[] data)
    {
        // Play from memory so stopping the screensaver never leaves the source file open.
        Stream stream = null;
        try
        {
            stream = new MemoryStream(data, writable: false);
            var legacy = stream.ReadByte() == 0x1f && stream.ReadByte() == 0x8b;
            stream.Position = 0;
            if (legacy)
                stream = new GZipStream(stream, CompressionMode.Decompress);

            Span<byte> header = stackalloc byte[12];
            stream.ReadExactly(header);
            var hasScanlineBounds = !legacy && header[..8].SequenceEqual("G33KMAP3"u8);
            var knownFormat = hasScanlineBounds || header[..8].SequenceEqual(legacy ? "G33KMAP1"u8 : "G33KMAP2"u8);
            if (!knownFormat ||
                BinaryPrimitives.ReadUInt16LittleEndian(header[8..]) != ArcadeData.Width ||
                BinaryPrimitives.ReadUInt16LittleEndian(header[10..]) != ArcadeData.Height)
                return null;

            if (!legacy)
                stream = new BrotliStream(stream, CompressionMode.Decompress);
            var recording = new ArcadeMapRecording(new BinaryReader(stream), legacy, hasScanlineBounds);

            stream = null;
            return recording;
        }
        finally
        {
            stream?.Dispose();
        }
    }

    private uint ReadDuration()
    {
        // Timestamps are inside the compressed stream. Scan once without reconstructing pixels.
        for (var end = ReadFrameEnd(); end != 0; end = ReadFrameEnd())
        {
            var count = m_palette == null ? m_delta.Length : ReadPaletteRows().Count;
            m_reader.BaseStream.ReadExactly(m_delta.AsSpan(0, count));
            m_frameEndsAt = end;
        }
        return m_frameEndsAt;
    }

    public bool AdvanceTo(double elapsedSeconds)
    {
        try
        {
            while (elapsedSeconds * 1_000_000 >= m_frameEndsAt)
            {
                if (!ReadNextFrame())
                    return false;
            }
            return true;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException)
        {
            return false;
        }
    }

    private bool ReadNextFrame()
    {
        var end = ReadFrameEnd();
        if (end == 0)
            return false;

        if (m_palette == null)
            ApplyLegacyDelta();
        else
            ApplyPaletteDelta();

        m_frameEndsAt = end;
        m_mapTint = null;
        return true;
    }

    private uint ReadFrameEnd()
    {
        var end = m_reader.ReadUInt32();
        if (end != 0 && end <= m_frameEndsAt)
            throw new InvalidDataException("Map timestamps must increase.");
        return end;
    }

    private void ApplyLegacyDelta()
    {
        m_reader.BaseStream.ReadExactly(m_delta);
        for (var pixel = 0; pixel < m_frame.Length; pixel++)
        {
            var offset = pixel * 3;
            var blue = m_previous[offset] ^= m_delta[offset];
            var green = m_previous[offset + 1] ^= m_delta[offset + 1];
            var red = m_previous[offset + 2] ^= m_delta[offset + 2];
            m_frame[pixel] = PackBgra(blue, green, red);
        }
    }

    private void ApplyPaletteDelta()
    {
        var (offset, count) = ReadPaletteRows();
        var rows = m_delta.AsSpan(0, count);
        m_reader.BaseStream.ReadExactly(rows);
        for (var i = 0; i < rows.Length; i++)
        {
            if (rows[i] != 0)
            {
                var pixel = offset + i;
                m_frame[pixel] = m_palette[m_previous[pixel] ^= rows[i]];
            }
        }
    }

    private (int Offset, int Count) ReadPaletteRows()
    {
        if (!m_hasScanlineBounds)
            return (0, PixelCount);

        var first = m_reader.ReadByte();
        var last = m_reader.ReadByte();
        if (first == ArcadeData.Height && last == 0)
            return (0, 0);
        if (first > last || last >= ArcadeData.Height)
            throw new InvalidDataException("Invalid map scanline range.");
        return (first * ArcadeData.Width, (last - first + 1) * ArcadeData.Width);
    }

    private static int PackBgra(byte blue, byte green, byte red) => OpaqueBlack | red << 16 | green << 8 | blue;

    public void Draw(PixelScreenData screen, int[] shades, double elapsedSeconds = 0)
    {
        if (shades != null && !ReferenceEquals(shades, m_mapTint))
        {
            m_tintedFrame ??= new int[PixelCount];
            for (var i = 0; i < m_frame.Length; i++)
                m_tintedFrame[i] = TintPixel(m_frame[i], shades);
            m_mapTint = shades;
        }
        var source = shades == null ? m_frame : m_tintedFrame;
        var fade = Math.Clamp((m_duration - elapsedSeconds) / Math.Min(FadeSeconds, m_duration), 0.0, 1.0);
        var level = (int)Math.Round(fade * 256);
        if (level == 256)
        {
            screen.SetBgraFrame(source);
            return;
        }

        m_fadedFrame ??= new int[PixelCount];
        for (var i = 0; i < source.Length; i++)
            m_fadedFrame[i] = FadePixel(source[i], level);
        screen.SetBgraFrame(m_fadedFrame);
    }

    internal static int TintPixel(int pixel, int[] shades)
    {
        var luminance = ((pixel >> 16 & 255) * 2126 + (pixel >> 8 & 255) * 7152 + (pixel & 255) * 722) / 10000;
        return shades[luminance];
    }

    internal static int FadePixel(int pixel, int level) =>
        OpaqueBlack | ((pixel >> 16 & 255) * level >> 8) << 16 |
        ((pixel >> 8 & 255) * level >> 8) << 8 | ((pixel & 255) * level >> 8);

    public static int[] CreateTint(Rgb background, Rgb foreground)
    {
        var shades = new int[256];
        for (var i = 0; i < shades.Length; i++)
        {
            var color = (i / 255.0).Lerp(background, foreground);
            shades[i] = PackBgra(color.B, color.G, color.R);
        }
        return shades;
    }

    public void Dispose() => m_reader.Dispose();
}
