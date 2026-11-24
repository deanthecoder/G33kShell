// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
// purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using DTC.Core;
using DTC.Core.Extensions;

namespace G33kShell.Desktop.Console;

/// <summary>
/// Represents a low-resolution, palette-indexed pixel surface.
/// </summary>
public class PixelScreenData
{
    private byte[] m_shade;
    private byte[] m_terrainShade;
    private byte[] m_light;
    private byte[] m_magicLight;
    private byte[] m_lightOcclusion;
    private byte[] m_lightAttenuation;
    private byte[] m_smoke;
    private byte[] m_smokeSoot;
    private (byte ColorIndex, byte Opacity)[] m_dust;
    private byte[] m_fog;
    private (byte ColorIndex, byte Weight)[] m_refraction;
    private (Rgb Background, byte Opacity, int Group)[] m_translucent;
    private byte m_spriteOpacity = 255;
    private int m_spriteGroup;
    private int[] m_bgraFrame;
    private int[] m_sceneSnapshot;
    private bool m_hasBgraFrame;
    internal Rgb LightColor { private get; set; }
    internal Rgb MagicLightColor { get; set; }
    internal Rgb SmokeColor { private get; set; }
    internal byte SmokeOpacity { private get; set; } = 255;
    internal Rgb FogColor { private get; set; }
    internal byte FogOpacity { private get; set; } = 255;
    internal byte TerrainShadeOpacity { private get; set; }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
    public Rgb[] Palette { get; }

    public PixelScreenData(int width, int height, Rgb[] palette)
    {
        if (width <= 0)
            throw new ArgumentException("Width must be greater than zero.", nameof(width));
        if (height <= 0)
            throw new ArgumentException("Height must be greater than zero.", nameof(height));
        if (palette == null || palette.Length == 0 || palette.Length > 256)
            throw new ArgumentException("Palette must contain between 1 and 256 colors.", nameof(palette));

        Width = width;
        Height = height;
        Pixels = new byte[width * height];
        // Palette fades belong to this surface, not to the reusable source artwork.
        Palette = (Rgb[])palette.Clone();
    }

    public void Clear(byte colorIndex = 0)
    {
        m_hasBgraFrame = false;
        Array.Fill(Pixels, colorIndex);
        if (m_shade != null)
            Array.Clear(m_shade);
        TerrainShadeOpacity = 0;
        if (m_light != null)
            Array.Clear(m_light);
        if (m_magicLight != null)
            Array.Clear(m_magicLight);
        MagicLightColor = null;
        if (m_lightOcclusion != null)
            Array.Clear(m_lightOcclusion);
        if (m_lightAttenuation != null)
            Array.Clear(m_lightAttenuation);
        if (m_smoke != null)
            Array.Clear(m_smoke);
        if (m_dust != null)
            Array.Clear(m_dust);
        if (m_fog != null)
            Array.Clear(m_fog);
        if (m_refraction != null)
            Array.Clear(m_refraction);
        if (m_translucent != null)
            Array.Clear(m_translucent);
        m_spriteOpacity = 255;
        m_spriteGroup = 0;
    }

    internal void RefractPixel(int offset, int sampleOffset, byte weight)
    {
        if (Pixels[offset] == Pixels[sampleOffset])
            return;
        m_refraction ??= new (byte ColorIndex, byte Weight)[Pixels.Length];
        if (weight > m_refraction[offset].Weight)
            m_refraction[offset] = (Pixels[sampleOffset], weight);
    }

    internal void ShadePixel(int offset, byte opacity)
    {
        m_shade ??= new byte[Pixels.Length];
        m_shade[offset] = Math.Max(m_shade[offset], opacity);
    }

    internal void SetTerrainShade(ReadOnlySpan<byte> mask, byte opacity)
    {
        m_terrainShade ??= new byte[Pixels.Length];
        mask.CopyTo(m_terrainShade);
        TerrainShadeOpacity = opacity;
    }

    internal void BlendBlackPixel(int offset, byte opacity)
    {
        m_shade ??= new byte[Pixels.Length];
        opacity = (byte)(opacity * m_spriteOpacity / 255);
        var remaining = (255 - m_shade[offset]) * (255 - opacity) / 255;
        m_shade[offset] = (byte)(255 - remaining);
    }

    internal void OccludeLightPixel(int offset, byte opacity)
    {
        m_lightOcclusion ??= new byte[Pixels.Length];
        m_lightOcclusion[offset] = Math.Max(m_lightOcclusion[offset], opacity);
    }

    internal void LightPixel(int offset, byte strength, bool occludedByGround = false)
    {
        if (occludedByGround && m_lightOcclusion != null)
            strength = (byte)(strength * (255 - m_lightOcclusion[offset]) / 255);
        if (m_lightAttenuation != null)
            strength = (byte)(strength * (255 - m_lightAttenuation[offset]) / 255);

        m_light ??= new byte[Pixels.Length];
        m_light[offset] = (byte)Math.Min(255, m_light[offset] + strength);
    }

    internal void SmokePixel(int offset, byte opacity, byte soot = 0)
    {
        if (opacity == 0)
            return;
        m_smoke ??= new byte[Pixels.Length];
        m_smokeSoot ??= new byte[Pixels.Length];
        var remaining = m_smoke[offset] * (255 - opacity) / 255;
        var combined = remaining + opacity;
        m_smokeSoot[offset] = (byte)((m_smokeSoot[offset] * remaining + soot * opacity) / combined);
        m_smoke[offset] = (byte)combined;
    }

    internal void DustPixel(int offset, byte colorIndex, byte opacity)
    {
        m_dust ??= new (byte ColorIndex, byte Opacity)[Pixels.Length];
        var previous = m_dust[offset];
        var remaining = previous.Opacity * (255 - opacity) / 255;
        m_dust[offset] = (opacity >= remaining ? colorIndex : previous.ColorIndex, (byte)(remaining + opacity));
    }

    internal void MagicLightPixel(int offset, byte strength)
    {
        m_magicLight ??= new byte[Pixels.Length];
        m_magicLight[offset] = Math.Max(m_magicLight[offset], strength);
    }

    internal void SetFogPixel(int offset, byte opacity)
    {
        m_fog ??= new byte[Pixels.Length];
        m_fog[offset] = opacity;
    }

    internal void CopyToBgra(Span<int> destination)
    {
        if (m_hasBgraFrame)
        {
            m_bgraFrame.AsSpan().CopyTo(destination);
            return;
        }

        for (var i = 0; i < Pixels.Length; i++)
        {
            var color = GetColorAtOffset(i);
            destination[i] = color.B | (color.G << 8) | (color.R << 16) | unchecked((int)0xff000000);
        }
    }

    /// <summary>
    /// Offsets the composited scene; subsequent pixel drawing forms a stationary overlay.
    /// </summary>
    internal void OffsetScene(int dx, int dy)
    {
        m_sceneSnapshot ??= new int[Pixels.Length];
        CopyToBgra(m_sceneSnapshot);
        m_bgraFrame ??= new int[Pixels.Length];
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            // Extend the edge pixels rather than exposing black strips during a small shake.
            var sourceX = Math.Clamp(x - dx, 0, Width - 1);
            var sourceY = Math.Clamp(y - dy, 0, Height - 1);
            m_bgraFrame[y * Width + x] = m_sceneSnapshot[sourceY * Width + sourceX];
        }
        m_hasBgraFrame = true;
    }

    internal void SetBgraFrame(ReadOnlySpan<int> pixels)
    {
        if (pixels.Length != Pixels.Length)
            throw new ArgumentException("The frame must match the screen size.", nameof(pixels));

        m_bgraFrame ??= new int[Pixels.Length];
        pixels.CopyTo(m_bgraFrame);
        m_hasBgraFrame = true;
    }

    private Rgb GetColorAtOffset(int offset)
    {
        var color = Palette[Pixels[offset]];
        if (m_translucent != null && m_translucent[offset].Opacity != 0)
        {
            var sprite = m_translucent[offset];
            color = BlendColor(sprite.Background, color, sprite.Opacity);
        }

        if (m_refraction != null && m_refraction[offset].Weight != 0)
        {
            var sample = m_refraction[offset];
            color = BlendColor(color, Palette[sample.ColorIndex], sample.Weight);
        }

        if (m_fog != null && m_fog[offset] != 0)
            color = BlendColor(color, FogColor, m_fog[offset] * FogOpacity / 255);

        var shadow = m_shade?[offset] ?? 0;
        var terrainShade = TerrainShadeOpacity == 0 ? 0 : m_terrainShade[offset] * TerrainShadeOpacity / 255;
        if (shadow != 0 || terrainShade != 0)
        {
            var brightness = (255 - shadow) * (255 - terrainShade) / 255;
            color = new Rgb(
                (byte)(color.R * brightness / 255),
                (byte)(color.G * brightness / 255),
                (byte)(color.B * brightness / 255));
        }

        if (m_light != null && m_light[offset] != 0)
            color = LiftColor(color, LightColor, m_light[offset]);

        if (m_magicLight != null && m_magicLight[offset] != 0)
            color = LiftColor(color, MagicLightColor, m_magicLight[offset]);

        if (m_smoke != null && m_smoke[offset] != 0)
        {
            var soot = m_smokeSoot[offset];
            var charcoal = new Rgb((byte)(SmokeColor.R / 4), (byte)(SmokeColor.G / 4), (byte)(SmokeColor.B / 4));
            color = BlendColor(color, BlendColor(SmokeColor, charcoal, soot), m_smoke[offset] * SmokeOpacity / 255);
        }

        if (m_dust != null && m_dust[offset].Opacity != 0)
            color = BlendColor(color, Palette[m_dust[offset].ColorIndex], m_dust[offset].Opacity);

        return color;
    }

    private static Rgb LiftColor(Rgb color, Rgb light, byte strength) =>
        new(LiftChannel(color.R, light.R, strength),
            LiftChannel(color.G, light.G, strength),
            LiftChannel(color.B, light.B, strength));

    private static byte LiftChannel(byte channel, byte light, byte strength) =>
        (byte)(channel + (255 - channel) * light * strength / (255 * 255));

    private static Rgb BlendColor(Rgb color, Rgb overlay, int opacity) =>
        new(BlendChannel(color.R, overlay.R, opacity),
            BlendChannel(color.G, overlay.G, opacity),
            BlendChannel(color.B, overlay.B, opacity));

    private static byte BlendChannel(byte channel, byte overlay, int opacity) =>
        (byte)((channel * (255 - opacity) + overlay * opacity) / 255);

    public void SetPalette(Rgb[] palette)
    {
        if (palette == null || palette.Length != Palette.Length)
            throw new ArgumentException("Palette must contain the same number of colors as the current palette.", nameof(palette));
        Array.Copy(palette, Palette, palette.Length);
    }

    public static Rgb[] CreateGreyscalePalette(Rgb[] sourcePalette, Rgb background, Rgb foreground)
    {
        if (sourcePalette == null)
            throw new ArgumentNullException(nameof(sourcePalette));

        var palette = new Rgb[sourcePalette.Length];
        for (var i = 0; i < sourcePalette.Length; i++)
            palette[i] = sourcePalette[i].Luminosity().Lerp(background, foreground);
        return palette;
    }

    public void SetPixel(int x, int y, byte colorIndex)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return;
        SetPixelAtOffset(y * Width + x, colorIndex);
    }

    internal void BeginTranslucentSprite(byte opacity)
    {
        m_spriteOpacity = opacity;
        m_spriteGroup++;
    }

    internal void EndTranslucentSprite() => m_spriteOpacity = 255;

    internal void SetPixelAtOffset(int offset, byte colorIndex, byte lightAttenuation = 0)
    {
        if (m_spriteOpacity < 255)
        {
            m_translucent ??= new (Rgb Background, byte Opacity, int Group)[Pixels.Length];
            // All parts of the sprite share the scene underneath their first draw at this pixel.
            if (m_translucent[offset].Group != m_spriteGroup)
                m_translucent[offset] = (GetColorAtOffset(offset), m_spriteOpacity, m_spriteGroup);
        }
        else if (m_translucent != null)
            m_translucent[offset] = default;

        Pixels[offset] = colorIndex;
        if (m_shade != null)
            m_shade[offset] = 0;
        if (TerrainShadeOpacity != 0)
            m_terrainShade[offset] = 0;
        if (m_light != null)
            m_light[offset] = 0;
        if (m_magicLight != null)
            m_magicLight[offset] = 0;
        if (m_lightOcclusion != null)
            m_lightOcclusion[offset] = 0;
        if (lightAttenuation != 0)
            m_lightAttenuation ??= new byte[Pixels.Length];
        if (m_lightAttenuation != null)
            m_lightAttenuation[offset] = lightAttenuation;
        if (m_smoke != null)
            m_smoke[offset] = 0;
        if (m_dust != null)
            m_dust[offset] = default;
        if (m_fog != null)
            m_fog[offset] = 0;
        if (m_refraction != null)
            m_refraction[offset] = default;
        if (m_hasBgraFrame)
        {
            var color = GetColorAtOffset(offset);
            m_bgraFrame[offset] = color.B | (color.G << 8) | (color.R << 16) | unchecked((int)0xff000000);
        }
    }

    public void DrawLine(int x0, int y0, int x1, int y1, byte colorIndex)
    {
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;

        while (true)
        {
            SetPixel(x0, y0, colorIndex);
            if (x0 == x1 && y0 == y1)
                return;

            var e2 = 2 * error;
            if (e2 >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    public void DrawAntialiasedLine(double x0, double y0, double x1, double y1, byte colorIndex, bool blend = true)
    {
        var steep = Math.Abs(y1 - y0) > Math.Abs(x1 - x0);
        if (steep)
        {
            (x0, y0) = (y0, x0);
            (x1, y1) = (y1, x1);
        }

        if (x0 > x1)
        {
            (x0, x1) = (x1, x0);
            (y0, y1) = (y1, y0);
        }

        var dx = x1 - x0;
        var dy = y1 - y0;
        if (dx < 0.001)
        {
            DrawAntialiasedVerticalLine(steep, x0, y0, y1, colorIndex, blend);
            return;
        }

        var gradient = dy / dx;
        var firstX = Round(x0);
        var firstY = y0 + gradient * (firstX - x0);
        var firstCoverage = ReverseFractionalPart(x0 + 0.5);
        PlotAntialiasedLinePair(steep, firstX, firstY, firstCoverage, colorIndex, blend);

        var interY = firstY + gradient;
        var lastX = Round(x1);
        var lastY = y1 + gradient * (lastX - x1);
        var lastCoverage = FractionalPart(x1 + 0.5);

        for (var x = firstX + 1; x < lastX; x++)
        {
            PlotAntialiasedLinePair(steep, x, interY, 1.0, colorIndex, blend);
            interY += gradient;
        }

        PlotAntialiasedLinePair(steep, lastX, lastY, lastCoverage, colorIndex, blend);
    }

    private void DrawAntialiasedVerticalLine(bool steep, double x, double y0, double y1, byte colorIndex, bool blend)
    {
        if (y0 > y1)
            (y0, y1) = (y1, y0);

        var ix = Round(x);
        var firstY = Round(y0);
        var lastY = Round(y1);
        for (var y = firstY; y <= lastY; y++)
            PlotAntialiasedPixel(steep, ix, y, 1.0, colorIndex, blend);
    }

    private void PlotAntialiasedLinePair(bool steep, int x, double y, double coverage, byte colorIndex, bool blend)
    {
        var wholeY = (int)Math.Floor(y);
        PlotAntialiasedPixel(steep, x, wholeY, ReverseFractionalPart(y) * coverage, colorIndex, blend);
        PlotAntialiasedPixel(steep, x, wholeY + 1, FractionalPart(y) * coverage, colorIndex, blend);
    }

    private void PlotAntialiasedPixel(bool steep, int x, int y, double coverage, byte colorIndex, bool blend)
    {
        if (coverage <= 0.0)
            return;

        if (steep)
            (x, y) = (y, x);

        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return;

        var index = y * Width + x;
        Pixels[index] = blend ? BlendColorIndex(Pixels[index], colorIndex, coverage) : colorIndex;
    }

    private static byte BlendColorIndex(byte currentColorIndex, byte targetColorIndex, double coverage) =>
        (byte)Math.Clamp(
            (int)Math.Round(currentColorIndex + (targetColorIndex - currentColorIndex) * Math.Clamp(coverage, 0.0, 1.0)),
            byte.MinValue,
            byte.MaxValue);

    private static int Round(double value) =>
        (int)Math.Floor(value + 0.5);

    private static double FractionalPart(double value) =>
        value - Math.Floor(value);

    private static double ReverseFractionalPart(double value) =>
        1.0 - FractionalPart(value);
}
