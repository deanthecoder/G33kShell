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

    public override void OnLoaded(WindowManager windowManager)
    {
        m_windowManager = windowManager;
        base.OnLoaded(windowManager);

        AddChild(new MatrixCanvas(Width, Height));

        m_taskCancellationRequested = false;
        m_periodicTask = Task.Run(async () =>
        {
            while (!m_taskCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1)); 

                if (m_secondsUntilDisplay == 0)
                    continue; // Screensaver is active.
                
                m_secondsUntilDisplay--;
                if (m_secondsUntilDisplay > 0)
                    continue; // Not ready yet...

                Y = 0;
                IsVisible = true;
                m_windowManager.HideCursor = true;
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

    private void ResetTimer()
    {
        IsVisible = false;
        m_windowManager.HideCursor = false;
        m_secondsUntilDisplay = m_timeToSleepSecs;
    }

    public override void Render(ScreenData screen)
    {
        // Nothing to do - The child renders itself.
    }
}