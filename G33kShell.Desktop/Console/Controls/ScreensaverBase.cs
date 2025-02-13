// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
//  purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.
using G33kShell.Desktop.Skins;

namespace G33kShell.Desktop.Console.Controls;

/// <summary>
/// Base class for screensaver implementations.
/// </summary>
public abstract class ScreensaverBase : AnimatedCanvas, IScreensaver
{
    protected ScreensaverBase(int width, int height, int targetFps = 30) : base(width, height, targetFps)
    {
    }

    public override void OnLoaded(WindowManager windowManager)
    {
        base.OnLoaded(windowManager);
        using (Screen.Lock(out var screen))
            BuildScreen(screen);
    }

    public override void OnSkinChanged(SkinBase skin)
    {
        base.OnSkinChanged(skin);
        using (Screen.Lock(out var screen))
            BuildScreen(screen);
    }

    protected virtual void BuildScreen(ScreenData screen)
    {
        screen.ClearChars();
        screen.ClearColor(Foreground, Background);
    }
}