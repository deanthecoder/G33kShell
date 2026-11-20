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
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DTC.Core;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console;
using G33kShell.Desktop.Console.Screensavers;
using Visual = G33kShell.Desktop.Console.Controls.Visual;

namespace G33kShell.Desktop.Terminal.Controls;

public class ScreensaverControl : Visual
{
    private readonly int m_timeToSleepSecs;
    private WindowManager m_windowManager;
    private TopLevel m_topLevel;
    private int m_secondsUntilDisplay;
    private Task m_periodicTask;
    private CancellationTokenSource m_periodicTokenSource;

    public event EventHandler<string> ScreensaverStarted;

    public ScreensaverControl(int width, int height, int timeToSleepSecs) : base(width, height)
    {
        m_timeToSleepSecs = timeToSleepSecs;
    }

    public static string[] GetAvailableNames()
    {
        try
        {
            return
                GetAvailableScreensaverInfo()
                    .Select(o => o.Name)
                    .OrderBy(o => o)
                    .ToArray();
        }
        catch (Exception ex)
        {
            Logger.Instance.Exception("Failed to enumerate screensavers.", ex);
            throw;
        }
    }

    public static (string Name, bool IsAi)[] GetAvailableScreensaverInfo()
    {
        try
        {
            return typeof(IScreensaver).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(IScreensaver).IsAssignableFrom(t))
                .Select(type =>
                {
                    var screensaver = (IScreensaver)Activator.CreateInstance(type, args: [10, 10]);
                    return (screensaver?.Name, IsAi: typeof(Console.Screensavers.AI.AiGameCanvasBase).IsAssignableFrom(type));
                })
                .Where(o => !string.IsNullOrWhiteSpace(o.Name))
                .Select(o => (o.Name, o.IsAi))
                .OrderBy(o => o.Name)
                .ToArray();
        }
        catch (Exception ex)
        {
            Logger.Instance.Exception("Failed to enumerate screensavers.", ex);
            throw;
        }
    }

    public override void OnLoaded(WindowManager windowManager)
    {
        m_windowManager = windowManager;
        base.OnLoaded(windowManager);

        StopPeriodicTask();

        m_periodicTokenSource = new CancellationTokenSource();
        var cancellationToken = m_periodicTokenSource.Token;
        m_periodicTask = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken); 

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

                // Restore the current view before capturing it or showing the screensaver.
                windowManager.ResetViewScroll();
                windowManager.Render();

                // Take a snapshot of the current shell screen (in case the screensaver wants it).
                ScreenData shellScreen;
                using (windowManager.Screen.Lock(out var screenData))
                    shellScreen = screenData.Clone();
                    
                SendToFront();
                Y = 0;
                IsVisible = true;
                m_windowManager.Cursor.IsVisible = false;
                var screensaver = (IScreensaver)Children.Single();
                ScreensaverStarted?.Invoke(this, screensaver.ActivationName);
                screensaver.StartScreensaver(shellScreen);
            }
        }, cancellationToken);

        if (m_topLevel != null)
        {
            m_topLevel.KeyDown -= OnTopLevelKeyDown;
            m_topLevel.RemoveHandler(InputElement.KeyDownEvent, OnInteractiveKeyDown);
            m_topLevel.RemoveHandler(InputElement.KeyUpEvent, OnInteractiveKeyUp);
            if (m_topLevel is Window oldWindow)
                oldWindow.Deactivated -= OnWindowDeactivated;
            m_topLevel.PointerWheelChanged -= OnTopLevelPointerWheelChanged;
        }

        m_topLevel = TopLevel.GetTopLevel(Application.Current.GetMainWindow());
        if (m_topLevel != null)
        {
            m_topLevel.KeyDown += OnTopLevelKeyDown;
            m_topLevel.AddHandler(InputElement.KeyDownEvent, OnInteractiveKeyDown, RoutingStrategies.Tunnel);
            m_topLevel.AddHandler(InputElement.KeyUpEvent, OnInteractiveKeyUp, RoutingStrategies.Tunnel);
            if (m_topLevel is Window window)
                window.Deactivated += OnWindowDeactivated;
            m_topLevel.PointerWheelChanged += OnTopLevelPointerWheelChanged;
        }
        
        ResetTimer();
    }

    protected override void OnUnloaded()
    {
        if (m_topLevel != null)
        {
            m_topLevel.KeyDown -= OnTopLevelKeyDown;
            m_topLevel.RemoveHandler(InputElement.KeyDownEvent, OnInteractiveKeyDown);
            m_topLevel.RemoveHandler(InputElement.KeyUpEvent, OnInteractiveKeyUp);
            if (m_topLevel is Window oldWindow)
                oldWindow.Deactivated -= OnWindowDeactivated;
            m_topLevel.PointerWheelChanged -= OnTopLevelPointerWheelChanged;
            m_topLevel = null;
        }

        StopPeriodicTask();
        base.OnUnloaded();
    }

    private void StopPeriodicTask()
    {
        m_periodicTokenSource?.Cancel();
        try
        {
            m_periodicTask?.Wait();
        }
        catch (AggregateException e) when (e.InnerExceptions.All(o => o is OperationCanceledException or TaskCanceledException))
        {
            // Expected when the visual is unloaded.
        }
        m_periodicTask = null;
        m_periodicTokenSource?.Dispose();
        m_periodicTokenSource = null;
    }

    /// <summary>
    /// Sets the control to be invisible, shows the cursor, and resets the countdown timer to the original value.
    /// </summary>
    private void ResetTimer()
    {
        if (IsVisible)
            ((IScreensaver)Children.Single()).StopScreensaver();
        IsVisible = false;
        m_windowManager.Cursor.IsVisible = true;
        m_secondsUntilDisplay = m_timeToSleepSecs;
    }

    private void OnTopLevelPointerWheelChanged(object sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y != 0 && ActiveInteractive == null)
            ResetTimer();
    }

    private IInteractiveScreensaver ActiveInteractive => IsVisible && Children.FirstOrDefault() is IInteractiveScreensaver { IsInteractive: true } interactive
        ? interactive : null;

    private void OnInteractiveKeyDown(object sender, KeyEventArgs e)
    {
        var interactive = ActiveInteractive;
        if (interactive == null)
            return;

        // Tunnel routing keeps Ctrl/arrows and Tab away from terminal scrolling and focus navigation.
        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            interactive.ClearInput();
            ResetTimer();
        }
        else
            interactive.SetKey(e.Key, true);
    }

    private void OnInteractiveKeyUp(object sender, KeyEventArgs e)
    {
        var interactive = ActiveInteractive;
        if (interactive == null)
            return;
        e.Handled = true;
        interactive.SetKey(e.Key, false);
    }

    private void OnWindowDeactivated(object sender, EventArgs e) => ActiveInteractive?.ClearInput();
    private void OnTopLevelKeyDown(object sender, KeyEventArgs e) =>
        ResetTimer();

    public override void Render(ScreenData screen)
    {
        // Nothing to do - The child renders itself.
    }

    public void SetScreensaver(string name, string extendedName = null)
    {
        var screensaverControls = typeof(IScreensaver).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(IScreensaver).IsAssignableFrom(t));
        var screensavers = screensaverControls.Select(o => (Visual)Activator.CreateInstance(o, Width, Height));
        var screensaver = screensavers.FirstOrDefault(o => o?.Name.Equals(name, StringComparison.OrdinalIgnoreCase) == true);
        if (screensaver == null)
            return;
        
        ClearChildren();
        AddChild(screensaver);

        ((IScreensaver)screensaver).ActivationName = extendedName ?? name;
    }

    public void StartNow() => m_secondsUntilDisplay = 1;
}
