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
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console;
using G33kShell.Desktop.Console.Controls;
using Visual = G33kShell.Desktop.Console.Controls.Visual;

namespace G33kShell.Desktop.Terminal.Controls;

public class ScreensaverControl : Visual
{
    private readonly int m_timeToSleepSecs;
    private WindowManager m_windowManager;
    private int m_secondsUntilDisplay;
    private Task m_periodicTask;
    private bool m_taskCancellationRequested;

    public ScreensaverControl(int width, int height, int timeToSleepSecs) : base(width, height)
    {
        m_timeToSleepSecs = timeToSleepSecs;
    }

    public static string[] GetAvailableNames()
    {
        var screensaverControls = typeof(IScreensaver).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(IScreensaver).IsAssignableFrom(t));
        return
            screensaverControls
            .Select(o => (IScreensaver)Activator.CreateInstance(o, args: new object[] { 10, 10 }))
            .Where(o => o != null)
            .Select(o => o.Name)
            .OrderBy(o => o)
            .ToArray();
    }

    public override void OnLoaded(WindowManager windowManager)
    {
        m_windowManager = windowManager;
        base.OnLoaded(windowManager);

        m_taskCancellationRequested = false;
        m_periodicTask = Task.Run(async () =>
        {
            while (!m_taskCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1)); 

                if (m_secondsUntilDisplay == 0)
                    continue; // Screensaver is active.

                if (!Children.Any())
                {
                    // There's no screensaver set.
                    m_secondsUntilDisplay = m_timeToSleepSecs;
                    continue;
                }
                
                m_secondsUntilDisplay--;
                if (m_secondsUntilDisplay > 0)
                    continue; // Not ready yet...

                SendToFront();
                Y = 0;
                IsVisible = true;
                m_windowManager.Cursor.IsVisible = false;
            }
        });

        var topLevel = TopLevel.GetTopLevel(Application.Current.GetMainWindow());
        if (topLevel != null)
            topLevel.KeyDown += (_, _) => ResetTimer();
        
        ResetTimer();
    }

    protected override void OnUnloaded()
    {
        m_taskCancellationRequested = true;
        m_periodicTask?.Wait();
        base.OnUnloaded();
    }

    /// <summary>
    /// Sets the control to be invisible, shows the cursor, and resets the countdown timer to the original value.
    /// </summary>
    private void ResetTimer()
    {
        IsVisible = false;
        m_windowManager.Cursor.IsVisible = true;
        m_secondsUntilDisplay = m_timeToSleepSecs;
    }

    public override void Render(ScreenData screen)
    {
        // Nothing to do - The child renders itself.
    }

    public void SetScreensaver(string name)
    {
        var screensaverControls = typeof(IScreensaver).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(IScreensaver).IsAssignableFrom(t));
        var screensavers = screensaverControls.Select(o => (Visual)Activator.CreateInstance(o, new object[]
        {
            Width, Height
        }));
        var screensaver = screensavers.FirstOrDefault(o => o?.Name.Equals(name, StringComparison.OrdinalIgnoreCase) == true);
        if (screensaver == null)
            return;
        
        ClearChildren();
        AddChild(screensaver);
    }

    public void StartNow() => m_secondsUntilDisplay = 1;
}