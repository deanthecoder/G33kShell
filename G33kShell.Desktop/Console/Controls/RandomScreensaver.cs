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
using System.Diagnostics;
using System.Linq;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Screensavers;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Controls;

[DebuggerDisplay("RandomScreensaver:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class RandomScreensaver : ScreensaverBase
{
    private readonly Type[] m_screenSaverTypes ;
    private ScreensaverBase m_active;
    private int m_frameNumber;
    private int m_activeIndex;

    public RandomScreensaver(int width, int height) : base(width, height)
    {
        Name = "random";

        m_screenSaverTypes =
            typeof(IScreensaver).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(ScreensaverBase).IsAssignableFrom(t))
                .Where(t => t != GetType())
                .ToArray();
        m_screenSaverTypes.Shuffle();
    }

    private ScreensaverBase CreateInstance(int width, int height, Type o)
    {
        var instance = (ScreensaverBase)Activator.CreateInstance(o, args: [width, height]) ?? throw new InvalidOperationException($"Could not create instance: {o}");
        instance.FrameNumber = 0;
        instance.Foreground = Foreground;
        instance.Background = Background;
        m_frameNumber = 0;
        TargetFps = instance.TargetFps;
        return instance;
    }

    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);

        m_active?.Stop();
        m_activeIndex = 0;
        m_active = CreateInstance(screen.Width, screen.Height, m_screenSaverTypes[m_activeIndex]);
        m_active.BuildScreen(screen);
    }

    public override void UpdateFrame(ScreenData screen)
    {
        m_active.FrameNumber = m_frameNumber++;
        m_active.UpdateFrame(screen);

        var timeSecs = m_frameNumber / TargetFps;
        if (timeSecs <= 120)
            return; // Not ready to switch screensavers.
        
        // Cycle through the screensavers.
        m_active.Stop();
        m_activeIndex++;
        if (m_activeIndex >= m_screenSaverTypes.Length)
        {
            // Reshuffle the list.
            m_activeIndex = 0;
            m_screenSaverTypes.Shuffle();
        }

        m_active = CreateInstance(screen.Width, screen.Height, m_screenSaverTypes[m_activeIndex]);
        base.BuildScreen(screen);
        m_active.BuildScreen(screen);
    }
}