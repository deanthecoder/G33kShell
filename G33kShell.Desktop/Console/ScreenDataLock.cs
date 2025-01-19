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

using System;
using System.Threading;

namespace G33kShell.Desktop.Console;

/// <summary>
/// Represents a lock for accessing and modifying a <see cref="ScreenData"/> object.
/// </summary>
public class ScreenDataLock : IDisposable
{
    private readonly object m_lock = new object();
    private readonly ScreenData m_screenData;

    public ScreenDataLock(int width, int height)
    {
        m_screenData = new ScreenData(width, height);
    }

    public int Width => m_screenData.Width;
    public int Height => m_screenData.Height;

    /// <summary>
    /// Acquires a lock on the <see cref="ScreenData"/> object for accessing and modifying it.
    /// </summary>
    public ScreenDataLock Lock(out ScreenData screenData)
    {
        Monitor.Enter(m_lock);
        screenData = m_screenData;
        return this;
    }

    public void Dispose() => Monitor.Exit(m_lock);
}