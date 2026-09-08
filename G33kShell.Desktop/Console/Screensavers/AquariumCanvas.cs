// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
// purpose. If you modify the code, please retain this copyright header.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Diagnostics;
using DTC.Core;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>An amber aquarium rendered on the full pixel surface.</summary>
[UsedImplicitly]
public class AquariumCanvas : PixelScreensaverBase
{
    private readonly Stopwatch m_clock = new();
    private AquariumScene m_scene;
    private AquariumRenderer m_renderer;
    private double m_lastTime;

    public AquariumCanvas(int width, int height) : base(width, height, 30) => Name = "aquarium";

    public override void StartScreensaver(ScreenData shellScreen)
    {
        base.StartScreensaver(shellScreen);
        if (WindowManager == null)
            return;
        var (width, height) = GetBufferSize(shellScreen.Width, shellScreen.Height);
        m_scene = new AquariumScene(width, height, Random.Shared.Next());
        m_renderer ??= new AquariumRenderer();
        m_clock.Restart();
        m_lastTime = 0;
        StartPixelScreen(width, height);
    }

    public override void UpdateFrame(ScreenData screen)
    {
        if (!IsPixelScreenActive || PixelScreen == null || m_scene == null)
            return;
        var now = m_clock.Elapsed.TotalSeconds;
        // Bound catch-up after a suspended window; integrate steering in small steps.
        var remaining = Math.Min(now - m_lastTime, 0.25);
        m_lastTime = now;
        var completed = false;
        while (remaining > 0)
        {
            var dt = (float)Math.Min(remaining, 1.0 / 60);
            completed |= m_scene.Step(dt);
            remaining -= dt;
        }
        using (PixelScreen.Lock(out var pixels))
            m_renderer.Draw(m_scene, pixels, Background, Foreground);
        if (completed)
            OnCycleCompleted();
    }

    public override void StopScreensaver()
    {
        base.StopScreensaver();
        m_clock.Stop();
        m_scene = null;
    }

    internal static (int Width, int Height) GetBufferSize(int columns, int rows)
    {
        // ConsoleView displays 8x16 cells. Preserve that aspect ratio, even for tall windows.
        var scale = Math.Max(384.0 / (columns * 8), 120.0 / (rows * 16));
        return ((int)Math.Round(columns * 8 * scale), (int)Math.Round(rows * 16 * scale));
    }

    protected override Rgb[] GetPixelPalette() => AquariumRenderer.CreatePalette(m_scene?.Opacity ?? 1, Background, Foreground);
}
