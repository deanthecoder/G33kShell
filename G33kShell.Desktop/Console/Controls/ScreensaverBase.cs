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
using System;
using System.Collections.Generic;
using G33kShell.Desktop.Console.Screensavers;
using G33kShell.Desktop.Skins;

namespace G33kShell.Desktop.Console.Controls;

/// <summary>
/// Base class for screensaver implementations.
/// </summary>
public abstract class ScreensaverBase : AnimatedCanvas, IScreensaver
{
    private readonly HashSet<string> m_activationSwitches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private string m_activationName;

    public string ActivationName
    {
        get => m_activationName ?? Name;
        set
        {
            m_activationName = value;
            ParseActivationSwitches();
        }
    }

    protected IReadOnlyCollection<string> ActivationSwitches => m_activationSwitches;

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

    public virtual void BuildScreen(ScreenData screen) =>
        screen.Clear(Foreground, Background);

    public virtual void StartScreensaver(ScreenData shellScreen)
    {
    }

    public virtual void StopScreensaver()
    {
    }

    protected bool HasSwitch(string name) =>
        !string.IsNullOrWhiteSpace(name) && m_activationSwitches.Contains(NormalizeSwitchName(name));

    private void ParseActivationSwitches()
    {
        m_activationSwitches.Clear();

        var activationName = ActivationName;
        if (string.IsNullOrWhiteSpace(activationName))
            return;

        var suffixStart = -1;
        if (!string.IsNullOrWhiteSpace(Name) && activationName.StartsWith(Name, StringComparison.OrdinalIgnoreCase))
        {
            if (activationName.Length <= Name.Length || activationName[Name.Length] != '_')
                return;

            suffixStart = Name.Length + 1;
        }
        else
        {
            var separatorIndex = activationName.IndexOf('_');
            if (separatorIndex >= 0)
                suffixStart = separatorIndex + 1;
        }

        if (suffixStart < 0 || suffixStart >= activationName.Length)
            return;

        foreach (var switchName in activationName[suffixStart..].Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            m_activationSwitches.Add(NormalizeSwitchName(switchName));
    }

    private static string NormalizeSwitchName(string name) =>
        name.Trim().TrimStart('_', '-');
}
