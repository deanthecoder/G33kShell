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
    private readonly Type[] m_screenSaverTypes;
    private bool m_cycleCompleted;
    private ScreensaverBase m_active;
    private ScreenData m_shellScreen;
    private int m_frameNumber;
    private int m_activeIndex;
    private WindowManager m_windowManager;

    public RandomScreensaver(int width, int height) : this(width, height,
        typeof(IScreensaver).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ScreensaverBase).IsAssignableFrom(t))
            .Where(t => t != typeof(RandomScreensaver))
            .Where(IsScreensaverReadyToRun)
            .ToArray())
    {
        m_screenSaverTypes.Shuffle();
    }

    internal RandomScreensaver(int width, int height, Type[] screensaverTypes) : base(width, height)
    {
        Name = "random";
        m_screenSaverTypes = screensaverTypes.ToArray();
    }

    public override bool IsReadyToRun => m_screenSaverTypes.Length > 0;

    private static bool IsScreensaverReadyToRun(Type type)
    {
        var screensaver = (IScreensaver)Activator.CreateInstance(type, args: [10, 10]);
        return screensaver?.IsReadyToRun == true;
    }

    private ScreensaverBase CreateInstance(int width, int height, Type o)
    {
        var instance = (ScreensaverBase)Activator.CreateInstance(o, args: [width, height]) ?? throw new InvalidOperationException($"Could not create instance: {o}");
        instance.FrameNumber = 0;
        instance.Foreground = Foreground;
        instance.Background = Background;
        m_frameNumber = 0;
        m_cycleCompleted = false;
        ActivationName = instance.ActivationName;
        if (m_windowManager != null)
        {
            instance.OnLoaded(m_windowManager);
            instance.Stop();
        }
        instance.CycleCompleted += OnActiveCycleCompleted;
        TargetFps = instance.TargetFps;
        return instance;
    }

    public override void OnLoaded(WindowManager windowManager)
    {
        m_windowManager = windowManager;
        base.OnLoaded(windowManager);
    }

    protected override void OnUnloaded()
    {
        ReleaseActiveScreensaver();
        m_active = null;
        m_windowManager = null;
        base.OnUnloaded();
    }

    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);

        ReleaseActiveScreensaver();
        m_activeIndex = 0;
        if (m_screenSaverTypes.Length == 0)
            return;

        m_active = CreateInstance(screen.Width, screen.Height, m_screenSaverTypes[m_activeIndex]);
        m_active.BuildScreen(screen);
        StartActiveScreensaver();
    }

    public override void StartScreensaver(ScreenData shellScreen)
    {
        m_shellScreen = shellScreen?.Clone();
        StartActiveScreensaver();
    }

    public override void StopScreensaver()
    {
        m_active?.StopScreensaver();
    }

    public override void UpdateFrame(ScreenData screen)
    {
        if (m_active == null)
        {
            base.BuildScreen(screen);
            screen.PrintAt(0, 0, "No ready screensavers are available.");
            return;
        }

        // Wait until the next frame so the completed cycle's final frame can be displayed.
        if (!m_cycleCompleted && m_frameNumber < TargetFps * 300)
        {
            m_active.FrameNumber = m_frameNumber++;
            m_active.UpdateFrame(screen);
            return;
        }

        // Cycle through the screensavers, retaining the timeout for continuous effects.
        ReleaseActiveScreensaver();
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
        StartActiveScreensaver();
    }

    private void OnActiveCycleCompleted(object sender, EventArgs e) => m_cycleCompleted = true;

    private void ReleaseActiveScreensaver()
    {
        if (m_active == null)
            return;
        m_active.CycleCompleted -= OnActiveCycleCompleted;
        m_active.StopScreensaver();
        m_active.Stop();
    }

    private void StartActiveScreensaver()
    {
        if (m_active == null || m_shellScreen == null)
            return;

        m_frameNumber = 0;
        m_active.FrameNumber = 0;
        m_cycleCompleted = false;
        System.Console.WriteLine($"Starting screensaver {m_active.Name}");
        m_active.StartScreensaver(m_shellScreen.Clone());
    }
}
