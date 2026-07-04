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
using System.IO;
using DTC.Core;
using G33kShell.Desktop.Console.Controls;
using G33kShell.Desktop.Skins;
using JetBrains.Annotations;
using SkiaSharp;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A roaming glass lens that refracts a pixel rendering of the shell snapshot.
/// </summary>
[UsedImplicitly]
public class ScienceCanvas : ScreensaverBase
{
    private const int PixelScaleX = 4;
    private const int PixelScaleY = 8;
    private const double LensRadiusRatio = 0.18;
    private const double LensSpeed = 42.0;
    private const double RefractionStrength = 0.44;
    private const int PaletteSize = 16;
    private const double LightingShadeScale = (PaletteSize - 1) / 7.0;
    private const string FontAsset = "PxPlus_IBM_VGA8.ttf";

    private static readonly Rgb[] s_basePalette = CreateBasePalette();

    private WindowManager m_windowManager;
    private PixelScreenDataLock m_pixelScreen;
    private byte[] m_sourcePixels;
    private bool m_isActive;
    private double m_lensX;
    private double m_lensY;
    private double m_velocityX;
    private double m_velocityY;
    private int m_pixelWidth;
    private int m_pixelHeight;
    private int m_lensRadius;
    private LensSample[] m_lensSamples;

    public ScienceCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, targetFps: 30)
    {
        Name = "science";
    }

    public override void OnLoaded(WindowManager windowManager)
    {
        m_windowManager = windowManager;
        base.OnLoaded(windowManager);
    }

    protected override void OnUnloaded()
    {
        base.OnUnloaded();
        ClearPixelScreen();
        m_windowManager = null;
    }

    public override void OnSkinChanged(SkinBase skin)
    {
        base.OnSkinChanged(skin);
        if (!m_isActive || m_pixelScreen == null)
            return;

        using (m_pixelScreen.Lock(out var pixels))
            pixels.SetPalette(GetPalette());
    }

    public override void StartScreensaver(ScreenData shellScreen)
    {
        base.StartScreensaver(shellScreen);
        if (m_windowManager == null || shellScreen == null)
            return;

        m_pixelWidth = shellScreen.Width * PixelScaleX;
        m_pixelHeight = shellScreen.Height * PixelScaleY;
        m_lensRadius = Math.Max(16, (int)Math.Round(Math.Min(m_pixelWidth, m_pixelHeight) * LensRadiusRatio));
        m_lensSamples = BuildLensSamples();
        m_sourcePixels = RasterizeSnapshot(shellScreen);
        ResetLens();

        m_isActive = true;
        m_pixelScreen = m_windowManager.SetPixelScreen(m_pixelWidth, m_pixelHeight, GetPalette());
    }

    public override void StopScreensaver()
    {
        base.StopScreensaver();
        ClearPixelScreen();
    }

    public override void BuildScreen(ScreenData screen) =>
        ClearTextOverlay(screen);

    public override void UpdateFrame(ScreenData screen)
    {
        ClearTextOverlay(screen);
        if (!m_isActive || m_pixelScreen == null || m_sourcePixels == null)
            return;

        AdvanceLens();
        using (m_pixelScreen.Lock(out var pixels))
        {
            Array.Copy(m_sourcePixels, pixels.Pixels, m_sourcePixels.Length);
            DrawLens(pixels);
        }
    }

    private byte[] RasterizeSnapshot(ScreenData shellScreen)
    {
        var source = new byte[m_pixelWidth * m_pixelHeight];

        for (var cellY = 0; cellY < shellScreen.Height; cellY++)
        {
            for (var cellX = 0; cellX < shellScreen.Width; cellX++)
            {
                var backgroundShade = GetShade(shellScreen.Chars[cellY][cellX].Background, fallback: 0);
                FillCell(source, cellX, cellY, backgroundShade);
            }
        }

        var fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "VGA", FontAsset);
        using var typeface = SKTypeface.FromFile(fontPath);
        using var glyphBitmap = new SKBitmap(m_pixelWidth, m_pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(glyphBitmap);
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            Typeface = typeface,
            TextSize = PixelScaleY,
            IsAntialias = false,
            IsStroke = false,
            LcdRenderText = false,
            SubpixelText = false
        };

        canvas.Clear(SKColors.Transparent);
        for (var cellY = 0; cellY < shellScreen.Height; cellY++)
        {
            for (var cellX = 0; cellX < shellScreen.Width; cellX++)
            {
                var ch = shellScreen.Chars[cellY][cellX].Ch;
                if (ch is '\0' or ' ')
                    continue;

                canvas.DrawText(ch.ToString(), cellX * PixelScaleX, (cellY + 1) * PixelScaleY - 1, paint);
            }
        }

        for (var y = 0; y < m_pixelHeight; y++)
        {
            for (var x = 0; x < m_pixelWidth; x++)
            {
                if (glyphBitmap.GetPixel(x, y).Alpha < 96)
                    continue;

                var attr = shellScreen.Chars[y / PixelScaleY][x / PixelScaleX];
                source[y * m_pixelWidth + x] = GetShade(attr.Foreground, fallback: PaletteSize - 2);
            }
        }

        return source;
    }

    private void FillCell(byte[] pixels, int cellX, int cellY, byte shade)
    {
        var left = cellX * PixelScaleX;
        var top = cellY * PixelScaleY;
        for (var y = 0; y < PixelScaleY; y++)
            Array.Fill(pixels, shade, (top + y) * m_pixelWidth + left, PixelScaleX);
    }

    private byte GetShade(Rgb color, byte fallback)
    {
        if (color == null)
            return fallback;

        var backgroundLuminosity = (Background ?? Rgb.Black).Luminosity();
        var foregroundLuminosity = (Foreground ?? Rgb.White).Luminosity();
        var range = foregroundLuminosity - backgroundLuminosity;
        if (Math.Abs(range) < 0.001)
            return fallback;

        var amount = Math.Clamp((color.Luminosity() - backgroundLuminosity) / range, 0.0, 1.0);
        return (byte)Math.Round(amount * (s_basePalette.Length - 1));
    }

    private void ResetLens()
    {
        m_lensX = Random.Shared.Next(m_lensRadius, Math.Max(m_lensRadius + 1, m_pixelWidth - m_lensRadius));
        m_lensY = Random.Shared.Next(m_lensRadius, Math.Max(m_lensRadius + 1, m_pixelHeight - m_lensRadius));

        var angle = Random.Shared.NextDouble() * Math.PI * 2.0;
        m_velocityX = Math.Cos(angle) * LensSpeed;
        m_velocityY = Math.Sin(angle) * LensSpeed;
        if (Math.Abs(m_velocityX) < LensSpeed * 0.35)
            m_velocityX = Math.CopySign(LensSpeed * 0.35, m_velocityX == 0.0 ? 1.0 : m_velocityX);
        if (Math.Abs(m_velocityY) < LensSpeed * 0.25)
            m_velocityY = Math.CopySign(LensSpeed * 0.25, m_velocityY == 0.0 ? 1.0 : m_velocityY);
    }

    private void AdvanceLens()
    {
        var deltaTime = 1.0 / TargetFps;
        m_lensX += m_velocityX * deltaTime;
        m_lensY += m_velocityY * deltaTime;

        if (m_lensX < m_lensRadius)
        {
            m_lensX = m_lensRadius;
            m_velocityX = Math.Abs(m_velocityX);
        }
        else if (m_lensX > m_pixelWidth - 1 - m_lensRadius)
        {
            m_lensX = m_pixelWidth - 1 - m_lensRadius;
            m_velocityX = -Math.Abs(m_velocityX);
        }

        if (m_lensY < m_lensRadius)
        {
            m_lensY = m_lensRadius;
            m_velocityY = Math.Abs(m_velocityY);
        }
        else if (m_lensY > m_pixelHeight - 1 - m_lensRadius)
        {
            m_lensY = m_pixelHeight - 1 - m_lensRadius;
            m_velocityY = -Math.Abs(m_velocityY);
        }
    }

    private void DrawLens(PixelScreenData screen)
    {
        var centerX = (int)Math.Round(m_lensX);
        var centerY = (int)Math.Round(m_lensY);

        // The light is fixed above the display, so its direction across the
        // sphere changes subtly as the lens travels beneath it.
        var lightX = m_pixelWidth * 0.18 - centerX;
        var lightY = m_pixelHeight * 0.10 - centerY;
        var lightZ = Math.Max(m_pixelWidth, m_pixelHeight) * 0.75;
        Normalize(ref lightX, ref lightY, ref lightZ);

        // Blinn-Phong half vector for a viewer facing the screen.
        var halfX = lightX;
        var halfY = lightY;
        var halfZ = lightZ + 1.0;
        Normalize(ref halfX, ref halfY, ref halfZ);

        foreach (var sample in m_lensSamples)
        {
            var sourceIndex =
                (centerY + sample.SourceOffsetY) * m_pixelWidth +
                centerX + sample.SourceOffsetX;
            var shade = m_sourcePixels[sourceIndex];

            var diffuse = Math.Max(
                0.0,
                sample.NormalX * lightX +
                sample.NormalY * lightY +
                sample.NormalZ * lightZ);

            var specular = Math.Max(
                0.0,
                sample.NormalX * halfX +
                sample.NormalY * halfY +
                sample.NormalZ * halfZ);
            specular *= specular;
            specular *= specular;
            specular *= specular;
            specular *= specular;
            specular *= specular; // x^32, without a per-pixel Math.Pow().

            var ambientRim = sample.Rim * (0.85 + diffuse * 0.65);
            var surfaceLight = 0.04 + (diffuse - 0.4) * 0.45;
            var lighting = ambientRim + surfaceLight + specular * 6.5;
            var shaded = shade + (int)Math.Round(lighting * LightingShadeScale);
            var targetIndex =
                (centerY + sample.OffsetY) * m_pixelWidth +
                centerX + sample.OffsetX;
            screen.Pixels[targetIndex] = (byte)Math.Clamp(shaded, 0, PaletteSize - 1);
        }
    }

    private LensSample[] BuildLensSamples()
    {
        var diameter = m_lensRadius * 2 + 1;
        var samples = new LensSample[diameter * diameter];
        var sampleCount = 0;
        var radiusSquared = m_lensRadius * m_lensRadius;

        for (var offsetY = -m_lensRadius; offsetY <= m_lensRadius; offsetY++)
        {
            for (var offsetX = -m_lensRadius; offsetX <= m_lensRadius; offsetX++)
            {
                var distanceSquared = offsetX * offsetX + offsetY * offsetY;
                if (distanceSquared > radiusSquared)
                    continue;

                var radialSquared = distanceSquared / (double)radiusSquared;
                var radial = Math.Sqrt(radialSquared);
                var sourceScale = 1.0 - RefractionStrength * (1.0 - radialSquared);
                samples[sampleCount++] = new LensSample(
                    (short)offsetX,
                    (short)offsetY,
                    (short)Math.Round(offsetX * sourceScale),
                    (short)Math.Round(offsetY * sourceScale),
                    (float)(offsetX / (double)m_lensRadius),
                    (float)(offsetY / (double)m_lensRadius),
                    (float)Math.Sqrt(Math.Max(0.0, 1.0 - radialSquared)),
                    (float)Math.Clamp((radial - 0.82) / 0.18, 0.0, 1.0));
            }
        }

        Array.Resize(ref samples, sampleCount);
        return samples;
    }

    private static void Normalize(ref double x, ref double y, ref double z)
    {
        var inverseLength = 1.0 / Math.Sqrt(x * x + y * y + z * z);
        x *= inverseLength;
        y *= inverseLength;
        z *= inverseLength;
    }

    private Rgb[] GetPalette()
    {
        var foreground = Foreground ?? Rgb.White;
        var background = Background ?? Rgb.Black;
        return PixelScreenData.CreateGreyscalePalette(s_basePalette, background, foreground);
    }

    private static Rgb[] CreateBasePalette()
    {
        var palette = new Rgb[PaletteSize];
        for (var i = 0; i < palette.Length; i++)
        {
            var value = (byte)Math.Round(i / (double)(palette.Length - 1) * byte.MaxValue);
            palette[i] = new Rgb(value, value, value);
        }
        return palette;
    }

    private void ClearPixelScreen()
    {
        m_isActive = false;
        if (m_windowManager != null &&
            m_pixelScreen != null &&
            ReferenceEquals(m_windowManager.PixelScreen, m_pixelScreen))
            m_windowManager.ClearPixelScreen();
        m_pixelScreen = null;
    }

    private static void ClearTextOverlay(ScreenData screen)
    {
        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Width; x++)
            {
                var attr = screen.Chars[y][x];
                attr.Set(' ');
                attr.Foreground = null;
                attr.Background = null;
            }
        }
    }

    private readonly record struct LensSample(
        short OffsetX,
        short OffsetY,
        short SourceOffsetX,
        short SourceOffsetY,
        float NormalX,
        float NormalY,
        float NormalZ,
        float Rim);
}
