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
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using G33kShell.Desktop.Skins;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A mono, pixel-backed Bad Apple!! animation.
/// </summary>
[UsedImplicitly]
public class BadAppleCanvas : ScreensaverBase
{
    private const string AssetFileName = "badapple_128x120_24fps.bar";
    private const byte Black = 0;
    private const byte White = 1;

    private static readonly Rgb[] s_basePalette = [Rgb.Black, Rgb.White];

    private WindowManager m_windowManager;
    private PixelScreenDataLock m_pixelScreen;
    private MovieData m_movie;
    private bool m_isActive;
    private int m_decodedFrameIndex = -1;
    private int m_runIndex;
    private int m_runRemaining;
    private bool m_toggleAfterRun;
    private byte m_currentColor;

    public BadAppleCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, targetFps: 24)
    {
        Name = "badapple";
    }

    public override void OnLoaded(WindowManager windowManager)
    {
        m_windowManager = windowManager;
        m_movie ??= LoadMovie();
        TargetFps = m_movie.Fps;
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

        using (m_pixelScreen.Lock(out var pixelScreen))
            pixelScreen.SetPalette(GetPalette());
    }

    public override void StartScreensaver(ScreenData shellScreen)
    {
        base.StartScreensaver(shellScreen);
        if (m_windowManager == null || m_movie == null)
            return;

        ResetDecoder();
        m_isActive = true;
        m_pixelScreen = m_windowManager.SetPixelScreen(m_movie.DisplayWidth, m_movie.DisplayHeight, GetPalette());
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
        if (!m_isActive || m_pixelScreen == null || m_movie == null)
            return;

        using (m_pixelScreen.Lock(out var pixels))
            DecodeFrame(FrameNumber % m_movie.FrameCount, pixels);
    }

    private void ClearPixelScreen()
    {
        m_isActive = false;
        m_pixelScreen = null;
        m_windowManager?.ClearPixelScreen();
    }

    private Rgb[] GetPalette()
    {
        var foreground = Foreground ?? Rgb.White;
        var background = Background ?? Rgb.Black;
        return PixelScreenData.CreateGreyscalePalette(s_basePalette, background, foreground);
    }

    private void DecodeFrame(int targetFrameIndex, PixelScreenData screen)
    {
        if (targetFrameIndex != m_decodedFrameIndex + 1)
            ResetDecoder();

        while (m_decodedFrameIndex < targetFrameIndex)
        {
            DecodeNextFrame(screen);
            m_decodedFrameIndex++;
        }
    }

    private void DecodeNextFrame(PixelScreenData screen)
    {
        var pixelCount = m_movie.StoredWidth * m_movie.StoredHeight;
        for (var i = 0; i < pixelCount; i++)
        {
            var color = ReadNextPixel();
            var storedX = i % m_movie.StoredWidth;
            var storedY = i / m_movie.StoredWidth;
            var displayX = m_movie.DisplayWidth - 1 - storedY;
            var displayY = storedX;
            screen.Pixels[displayY * screen.Width + displayX] = color;
        }
    }

    private byte ReadNextPixel()
    {
        if (m_runRemaining == 0)
        {
            if (m_toggleAfterRun)
                m_currentColor = (byte)(m_currentColor == Black ? White : Black);

            if (m_runIndex >= m_movie.Runs.Length)
            {
                ResetDecoder();
                if (m_runIndex >= m_movie.Runs.Length)
                    return Black;
            }

            var runLength = m_movie.Runs[m_runIndex++];
            m_runRemaining = runLength == 0 ? byte.MaxValue : runLength;
            m_toggleAfterRun = runLength != 0;
        }

        m_runRemaining--;
        return m_currentColor;
    }

    private void ResetDecoder()
    {
        m_decodedFrameIndex = -1;
        m_runIndex = 0;
        m_runRemaining = 0;
        m_toggleAfterRun = false;
        m_currentColor = m_movie?.InitialColor ?? Black;
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

    private static MovieData LoadMovie()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "BadApple", AssetFileName);
        var data = File.ReadAllBytes(path).Decompress();

        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);
        var magic = reader.ReadBytes(4);
        if (magic.Length != 4 || magic[0] != 'B' || magic[1] != 'A' || magic[2] != 'R' || magic[3] != '1')
            throw new InvalidDataException($"Unexpected Bad Apple movie format: {path}");

        var displayWidth = reader.ReadUInt16();
        var displayHeight = reader.ReadUInt16();
        var storedWidth = reader.ReadUInt16();
        var storedHeight = reader.ReadUInt16();
        var fps = reader.ReadUInt16();
        var frameCount = reader.ReadInt32();
        var initialColor = reader.ReadByte();
        var runs = reader.ReadBytes((int)(stream.Length - stream.Position));

        return new MovieData(displayWidth, displayHeight, storedWidth, storedHeight, fps, frameCount, initialColor, runs);
    }

    private sealed record MovieData(
        int DisplayWidth,
        int DisplayHeight,
        int StoredWidth,
        int StoredHeight,
        int Fps,
        int FrameCount,
        byte InitialColor,
        byte[] Runs);
}
