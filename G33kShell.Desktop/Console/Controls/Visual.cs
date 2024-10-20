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
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using CSharp.Core;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Events;
using G33kShell.Desktop.Skins;

namespace G33kShell.Desktop.Console.Controls;

public abstract class Visual
{
    private readonly List<Visual> m_children = new List<Visual>();
    private Visual m_parent;
    private int m_y;
    private int m_x;
    private Rgb m_foreground;
    private Rgb m_background;
    private bool m_isVisible = true;

    public event EventHandler<(int X, int Y)?> CursorPosChanged;

    /// <summary>
    /// Arbitrary control name. (See WindowManager.Find)
    /// </summary>
    public string Name { get; init; }
    
    public Visual Parent
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

    /// <summary>
    /// The relative position of this control relative to its parent (after h/v alignment is applied).
    /// </summary>
    /// <see cref="ActualX"/>
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

    /// <summary>
    /// The relative position of this control relative to its parent (after h/v alignment is applied).
    /// </summary>
    /// <see cref="ActualY"/>
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

    /// <summary>
    /// The absolute position of this control relative to the root.
    /// </summary>
    /// <see cref="X"/>
    public int ActualX { get; internal set; }

    /// <summary>
    /// The absolute position of this control relative to the root.
    /// </summary>
    /// <see cref="Y"/>
    public int ActualY { get; internal set; }
    
    public int Width => Screen?.Width ?? 0;
    public int Height => Screen?.Height ?? 0;

    public Rect ActualBounds => new Rect(ActualX, ActualY, Width, Height);
    
    public ScreenDataLock Screen { get; }
    public IEnumerable<Visual> Children => m_children;
    
    public Rgb Foreground
    {
        get => m_foreground ?? Parent?.Foreground ?? Colors.White;
        set
        {
            if (m_foreground?.Equals(value) == true)
                return;
            m_foreground = value;
            InvalidateVisual();
        }
    }
    
    public Rgb Background
    {
        get => m_background ?? Parent?.Background ?? Colors.Black;
        set
        {
            if (m_background?.Equals(value) == true)
                return;
            m_background = value;
            InvalidateVisual();
        }
    }
    
    /// <summary>
    /// The alignment of this control relative to its parent.
    /// </summary>
    public HorizontalAlignment HorizontalAlignment { get; init; } = HorizontalAlignment.Left;

    /// <summary>
    /// The alignment of this control relative to its parent.
    /// </summary>
    public VerticalAlignment VerticalAlignment { get; init; } = VerticalAlignment.Top;
    
    /// <summary>
    /// Optional padding applied to the children of this control.
    /// </summary>
    public BorderThickness Padding { get; set; } = BorderThickness.Zero;

    /// <summary>
    /// Whether re-rendering is required, due to the content changing.
    /// </summary>
    public bool IsInvalidatedVisual { get; internal set; } = true;

    public bool IsVisible
    {
        get => m_isVisible && (Parent == null || Parent.IsVisible);
        set
        {
            if (m_isVisible == value)
                return;
            m_isVisible = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Internal flag to indicate whether <see cref="OnLoaded"/> needs to be called.
    /// </summary>
    public bool LoadOnFirstRender { get; internal set; } = true;

    protected Visual(int width, int height)
    {
        Screen = new ScreenDataLock(width, height);
    }

    /// <summary>
    /// A request to the implementer to update the content of <see cref="Screen"/>.
    /// </summary>
    public abstract void Render(ScreenData screen);
    
    /// <summary>
    /// Invalidates the visual, scheduling a call to <see cref="Render"/>
    /// </summary>
    /// <remarks>
    /// Applies to this control and all parents.
    /// </remarks>
    public void InvalidateVisual()
    {
        IsInvalidatedVisual = true;
        Parent?.InvalidateVisual();
    }

    /// <summary>
    /// Adds a child <see cref="Visual"/> element to this <see cref="Visual"/> instance.
    /// </summary>
    public Visual AddChild(Visual child)
    {
        if (child.Parent != null)
            throw new InvalidOperationException("Canvas is already added to the visual tree.");
        
        m_children.Add(child);
        child.Parent = this;
        child.InvalidateVisual();
        child.LoadOnFirstRender = true;
        return this;
    }

    /// <summary>
    /// Removes the current visual instance from its parent visual.
    /// </summary>
    public void RemoveFromParent() => Parent.RemoveChild(this);

    /// <summary>
    /// Removes the specified child from the list of children of the current visual.
    /// </summary>
    /// <param name="child">The child visual to be removed.</param>
    /// <remarks>
    /// If the specified child is not a child of the current visual, nothing happens.
    /// </remarks>
    public void RemoveChild(Visual child)
    {
        if (!m_children.Remove(child))
            return; // Nothing to do.
        child.OnUnloaded();
        child.m_children.ToList().ForEach(child.RemoveChild);
        child.Parent = null;
        InvalidateVisual();
    }

    /// <summary>
    /// Move the visual to the end of its parent's child list, ensuring it is drawn last.
    /// </summary>
    public void SendToFront()
    {
        m_parent?.m_children.Remove(this);
        m_parent?.m_children.Add(this);
        InvalidateVisual();
    }

    /// <summary>
    /// Called prior to the visual being rendered for the first time.
    /// </summary>
    public virtual void OnLoaded(WindowManager windowManager)
    {
        // Do nothing.
    }

    /// <summary>
    /// Called when the control is removed from the visual hierarchy.
    /// </summary>
    /// <seealso cref="RemoveChild"/>
    protected virtual void OnUnloaded()
    {
        // Do nothing.
    }

    /// <summary>
    /// Remove all child visuals, optionally applying an effect to do so.
    /// </summary>
    /// <see cref="ClearChildren"/>
    public async Task ClearAsync(ClearTransition transition)
    {
        if (!m_children.Any())
            return; // Nothing to do.
        
        switch (transition)
        {
            case ClearTransition.Immediate:
            {
                ClearChildren();
                return;
            }

            case ClearTransition.Explode:
            {
                var duration = TimeSpan.FromSeconds(0.4);
                var anims = new List<Task>();
                foreach (var child in m_children)
                {
                    var originalX = child.X;
                    var originalY = child.Y;
                    var toEscapeLeft = -child.ActualBounds.Right;
                    var toEscapeRight = ActualX + Width - child.ActualBounds.Left;
                    var toEscapeUp = -child.ActualBounds.Bottom;
                    var toEscapeDown = ActualY + Height - child.ActualBounds.Top;
                    var dX = Math.Abs(toEscapeLeft) < Math.Abs(toEscapeRight) ? toEscapeLeft : toEscapeRight;
                    var dY = Math.Abs(toEscapeUp) < Math.Abs(toEscapeDown) ? toEscapeUp : toEscapeDown;
                    var shiftInX = Math.Abs(dX) < Math.Abs(dY * 2.0); // x2 to adjust for non-square font.
                    var anim = new Animation(duration, f =>
                        {
                            if (shiftInX)
                                child.X = (int)f.Lerp(originalX, originalX + dX);
                            else
                                child.Y = (int)f.Lerp(originalY, originalY + dY);
                            return true;
                        })
                        .StartAsync();
                    anims.Add(anim);
                }

                await Task.WhenAll(anims);
                await ClearAsync(ClearTransition.Immediate);
                break;
            }
                
            default:
                throw new ArgumentException($"Unknown transition: {transition}", nameof(transition));
        }
    }

    /// <summary>
    /// Remove all child visuals.
    /// </summary>
    /// <see cref="ClearAsync"/>
    protected void ClearChildren()
    {
        while (m_children.Count > 0)
            RemoveChild(m_children.Last());
    }

    /// <summary>
    /// Called when a console event occurs.
    /// </summary>
    /// <remarks>
    /// Passed down from the root console visual, to the children.
    /// </remarks>
    /// <param name="consoleEvent">The console event that occurred.</param>
    /// <param name="handled">Allows the implementer to stop further controls receiving the event.</param>
    public virtual void OnEvent(ConsoleEvent consoleEvent, ref bool handled)
    {
        // Do nothing by default.
    }

    /// <summary>
    /// Sets the position of the cursor relative to the visual's origin.
    /// </summary>
    protected void SetCursorPos(int dx, int dy) =>
        CursorPosChanged?.Invoke(this, (X + dx, Y + dy));

    public void SetHeight(int height)
    {
        using (Screen.Lock(out var screenData))
            screenData.Height = height;
    }

    /// <summary>
    /// Apply a delta to the position of all child controls.
    /// </summary>
    public void ScrollChildren(int dy)
    {
        if (dy == 0)
            return;
        
        foreach (var child in Children)
            child.Y += dy;
        InvalidateVisual();
    }

    /// <summary>
    /// Scroll the parent's child controls to ensure this one is visible on the screen.
    /// </summary>
    public void ScrollIntoView()
    {
        var maxBottom = Parent.Children.Max(o => o.Y + o.Height);
        var linesOffTheBottom = maxBottom - Parent.Height;
        if (linesOffTheBottom > 0)
            Parent.ScrollChildren(-linesOffTheBottom);
    }

    public virtual void OnSkinChanged(SkinBase skin)
    {
        // Do nothing.
    }
}