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
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Layout;
using Avalonia.Media;

namespace G33kShell.Desktop.Console;

[DebuggerDisplay("Canvas:{X},{Y} {Width}x{Height}")]
public class Canvas
{
    private readonly List<Canvas> m_children = new List<Canvas>();
    private Canvas m_parent;
    private int m_y;
    private int m_x;

    public Canvas Parent
    {
        get => m_parent;
        private set
        {
            if (m_parent == value)
                return;
            m_parent = value;
            InvalidateVisual();
        }
    }

    public int Y
    {
        get => m_y;
        set
        {
            if (m_y == value)
                return;
            m_y = value;
            InvalidateVisual();
        }
    }

    public int X
    {
        get => m_x;
        set
        {
            if (m_x == value)
                return;
            m_x = value;
            InvalidateVisual();
        }
    }

    public int Width => Screen.Width;
    public int Height => Screen.Height;

    public ScreenData Screen { get; private set; }

    public IEnumerable<Canvas> Children => m_children;

    public IBrush Foreground
    {
        get => Screen.Foreground;
        set
        {
            if (ReferenceEquals(Screen.Foreground, value))
                return;
            Screen.Foreground = value;
            InvalidateVisual();
        }
    }

    public IBrush Background
    {
        get => Screen.Background;
        set
        {
            if (ReferenceEquals(Screen.Background, value))
                return;
            Screen.Background = value;
            InvalidateVisual();
        }
    }

    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;
    public BorderThickness Padding { get; set; } = BorderThickness.Zero;

    /// <summary>
    /// Whether re-rendering is required, due to the content changing.
    /// </summary>
    /// <remarks>
    /// Defaults to 'true' so we always get an initial render.
    /// </remarks>
    public bool IsInvalidatedVisual { get; private set; } = true;
    
    public Canvas Init(int width, int height)
    {
        Screen = new ScreenData(width, height);
        return this;
    }
    
    public virtual void Render()
    {
        // The visual is not invalidated once it's been rendered.
        IsInvalidatedVisual = false;
    }

    public void InvalidateVisual()
    {
        IsInvalidatedVisual = true;
        Parent?.InvalidateVisual();
    }
    
    public Canvas AddChild(Canvas child)
    {
        if (child.Parent != null)
            throw new InvalidOperationException("Canvas is already added to the visual tree.");
        
        m_children.Add(child);
        child.Parent = this;
        child.InvalidateVisual();
        return this;
    }

    public void RemoveChild(Canvas child)
    {
        if (!m_children.Remove(child))
            return; // Nothing to do.
        child.OnUnloaded();
        child.m_children.ForEach(child.RemoveChild);
    }
    
    protected virtual void OnUnloaded()
    {
        // Do nothing.
    }
}