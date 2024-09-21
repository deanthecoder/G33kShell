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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace G33kShell.Desktop.Console;

/// <summary>
/// Represents an animated canvas that can render frames asynchronously.
/// </summary>
/// <remarks>
/// Subclasses must implement <see cref="UpdateFrame"/>, which is called
/// on its own thread at the required FPS and should update the 'Screen' data
/// as required.
/// </remarks>
public abstract class AnimatedCanvas : Canvas
{
    private readonly Stopwatch m_stopwatch = new Stopwatch();
    private readonly long m_frameTimeMs;
    private readonly object m_updateLock = new object();
    private bool m_running;

    protected int m_frameNumber;

    protected AnimatedCanvas(int width, int height, int targetFps = 30) : base(width, height)
    {
        m_frameTimeMs = 1000 / targetFps; // Frame time in milliseconds
    }

    public override void Render()
    {
        if (!m_running) // todo - Do this from OnLoaded() (and find other classes with similar logic)
        {
            // Start the animation asynchronously.
            m_running = true;
            Task.Run(UpdateFrameBase);
        }
        
        lock (m_updateLock)
            base.Render();
    }

    /// <summary>
    /// Represents a method that updates the frame of an AnimatedCanvas.
    /// </summary>
    /// <remarks>
    /// This method is called on its own thread at the required FPS and should be
    /// implemented by subclasses to update the 'Screen' data as required.
    /// </remarks>
    protected abstract void UpdateFrame();

    private void UpdateFrameBase()
    {
        while (m_running)
        {
            if (!m_stopwatch.IsRunning)
                m_stopwatch.Start();
            if (m_stopwatch.ElapsedMilliseconds < m_frameTimeMs)
            {
                // Skip this frame, too soon to render again.
                Thread.Sleep(10);
                continue;
            }

            m_stopwatch.Restart(); // Reset for the next frame

            lock (m_updateLock)
            {
                UpdateFrame();

                // Request rendering update.
                InvalidateVisual();

                m_frameNumber++;
            }
        }
    }

    protected override void OnUnloaded()
    {
        m_running = false;
        base.OnUnloaded();
    }
}