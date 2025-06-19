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
using System.Threading.Tasks;
using DTC.Core;

namespace G33kShell.Desktop.Console.Controls;

/// <summary>
/// Represents an animated canvas that can render frames asynchronously.
/// </summary>
/// <remarks>
/// Subclasses must implement <see cref="UpdateFrame"/>, which is called
/// on its own thread at the required FPS and should update the 'Screen' data
/// as required.
/// </remarks>
public abstract class AnimatedCanvas : Visual
{
    private readonly Stopwatch m_stopwatch = new Stopwatch();
    private int m_frameTimeMs;
    private bool m_running;
    private Task m_animationTask;
    private int m_targetFps;

    public int FrameNumber { get; set; }
    
    public int TargetFps
    {
        get => m_targetFps;
        protected set
        {
            m_targetFps = value;
            m_frameTimeMs = (int)(1000.0 / value); // Frame time in milliseconds
        }
    }

    protected double Time
    {
        get
        {
            if (!m_stopwatch.IsRunning)
                m_stopwatch.Restart();
            return m_stopwatch.Elapsed.TotalSeconds;
        }
    }

    protected AnimatedCanvas(int width, int height, int targetFps = 30) : base(width, height)
    {
        if (targetFps <= 0)
            throw new ArgumentException("Target FPS must be greater than 0.", nameof(targetFps));
        TargetFps = targetFps;
    }

    /// <summary>
    /// Represents a method that updates the frame of an AnimatedCanvas.
    /// </summary>
    /// <remarks>
    /// This method is called on its own thread at the required FPS and should be
    /// implemented by subclasses to update the 'Screen' data as required.
    /// </remarks>
    public abstract void UpdateFrame(ScreenData screen);

    public override void Render(ScreenData _)
    {
        // Do nothing - Updating the screen is handled in UpdateFrame().
    }

    private async Task UpdateFrameBase()
    {
        try
        {
            m_stopwatch.Restart();
            var frameStopwatch = new Stopwatch();
            while (m_running)
            {
                if (!IsVisible)
                {
                    await Task.Delay(100);
                    continue;
                }
            
                if (!frameStopwatch.IsRunning)
                    frameStopwatch.Start();
                var timeToWaitMs = m_frameTimeMs - frameStopwatch.ElapsedMilliseconds;
                if (timeToWaitMs > 0 || !IsVisible)
                {
                    // Skip this frame, too soon to render again.
                    await Task.Delay((int)timeToWaitMs);
                    continue;
                }

                frameStopwatch.Restart(); // Reset for the next frame

                using (Screen.Lock(out var screen))
                    UpdateFrame(screen);

                // Request rendering update.
                InvalidateVisual();
                FrameNumber++;
            }
        }
        catch (Exception e)
        {
            Logger.Instance.Exception("Failed to update render frame.", e);
        }
    }

    public override void OnLoaded(WindowManager windowManager)
    {
        base.OnLoaded(windowManager);
        
        // Start the animation asynchronously.
        m_running = true;
        m_animationTask = Task.Run(UpdateFrameBase);
    }

    protected override void OnUnloaded()
    {
        Stop();
        base.OnUnloaded();
    }

    public void Stop()
    {
        m_running = false;
        m_animationTask?.Wait();
        m_animationTask = null;
    }
}
