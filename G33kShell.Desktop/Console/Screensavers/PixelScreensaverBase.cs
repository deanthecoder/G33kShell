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

using DTC.Core;
using G33kShell.Desktop.Console.Controls;
using G33kShell.Desktop.Skins;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// Shared lifecycle for screensavers that draw into a palette-indexed pixel surface.
/// </summary>
public abstract class PixelScreensaverBase : ScreensaverBase
{
    protected WindowManager WindowManager { get; private set; }
    protected PixelScreenDataLock PixelScreen { get; private set; }
    protected bool IsPixelScreenActive { get; private set; }

    protected PixelScreensaverBase(int width, int height, int targetFps = 30) : base(width, height, targetFps)
    {
    }

    public override void OnLoaded(WindowManager windowManager)
    {
        WindowManager = windowManager;
        base.OnLoaded(windowManager);
    }

    protected override void OnUnloaded()
    {
        base.OnUnloaded();
        ClearPixelScreen();
        WindowManager = null;
    }

    public override void OnSkinChanged(SkinBase skin)
    {
        base.OnSkinChanged(skin);
        if (!IsPixelScreenActive || PixelScreen == null)
            return;

        using (PixelScreen.Lock(out var pixels))
            pixels.SetPalette(GetPixelPalette());
    }

    public override void StopScreensaver()
    {
        base.StopScreensaver();
        ClearPixelScreen();
    }

    public override void BuildScreen(ScreenData screen) =>
        ClearTextOverlay(screen);

    protected void StartPixelScreen(int width, int height)
    {
        if (WindowManager == null)
            return;

        using (Screen.Lock(out var screen))
            ClearTextOverlay(screen);
        InvalidateVisual();

        IsPixelScreenActive = true;
        PixelScreen = WindowManager.SetPixelScreen(width, height, GetPixelPalette());
    }

    protected virtual void ClearPixelScreen()
    {
        IsPixelScreenActive = false;
        if (WindowManager != null &&
            PixelScreen != null &&
            ReferenceEquals(WindowManager.PixelScreen, PixelScreen))
            WindowManager.ClearPixelScreen();
        PixelScreen = null;
    }

    protected static void ClearTextOverlay(ScreenData screen)
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

    protected abstract Rgb[] GetPixelPalette();
}
