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
using Avalonia.Input;
using Avalonia.Layout;
using DTC.Core;
using G33kShell.Desktop.Console.Controls;
using G33kShell.Desktop.Console.Events;
using G33kShell.Desktop.Skins;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console;

public class WindowManager
{
    private readonly object m_renderLock = new object();
    private readonly List<ConsoleEvent> m_consoleEvents = new List<ConsoleEvent>();
    private SkinBase m_skin;
    private ConsoleCursor m_cursor;
    internal int OffsetY { get; private set; }

    public Visual Root { get; }

    public ScreenDataLock Screen => Root.Screen;

    public SkinBase Skin
    {
        get => m_skin;
        set
        {
            if (m_skin == value)
                return;
            m_skin = value ?? new RetroMonoDos();

            foreach (var visual in GetVisualTree(Root))
            {
                visual.Foreground = m_skin.ForegroundColor;
                visual.Background = m_skin.BackgroundColor;
                visual.OnSkinChanged(Skin);
            }
        }
    }
    
    /// <summary>
    /// The current cursor.
    /// </summary>
    public ConsoleCursor Cursor => m_cursor ??= new ConsoleCursor();

    public WindowManager(int width, int height, SkinBase skin)
    {
        Root = new RootCanvas(width, height);
        Skin = skin ?? throw new ArgumentNullException(nameof(skin));
    }

    /// <summary>
    /// Recurse through all visual children, ask them to update their screen data,
    /// then composite onto the root Screen.
    /// </summary>
    public void Render()
    {
        lock (m_renderLock)
        {
            if (!Root.IsInvalidatedVisual)
                return; // No changes have been made - Stop here.

            // Render the entire view hierarchy.
            Render(Root);

            // Now blit each child onto our top-level 'screen'.
            using (Screen.Lock(out var targetScreen))
            {
                targetScreen.ClearChars('\0');
                targetScreen.ClearColor(Root.Foreground, Root.Background);
                foreach (var visual in GetVisualTree(Root).Skip(1).Where(o => o.IsVisible))
                {
                    var pos = GetAbsolutePos(visual);
                    visual.ActualX = pos.x;
                    visual.ActualY = pos.y;
                    using (visual.Screen.Lock(out var sourceScreen))
                        sourceScreen.CopyTo(targetScreen, pos.x, pos.y + OffsetY);
                }
            }
        }
    }

    /// <summary>
    /// Recurse through all visual children, asking them to update their own screen data.
    /// </summary>
    /// <remarks>
    /// The collection of screens will be composited later.
    /// </remarks>
    private void Render([NotNull] Visual visual)
    {
        if (visual == null)
            throw new ArgumentNullException(nameof(visual));
        
        try
        {
            // Ask the visual to render its own content.
            if (visual.IsInvalidatedVisual)
            {
                using (visual.Screen.Lock(out var screen))
                {
                    if (visual.LoadOnFirstRender)
                    {
                        visual.LoadOnFirstRender = false;
                        visual.OnLoaded(this);
                    }
                
                    visual.IsInvalidatedVisual = false;
                
                    if (IsOnScreen(visual))
                        visual.Render(screen);
                }
            }
        }
        catch (Exception e)
        {
            Logger.Instance.Exception($"Failed to update render frame ('{visual.Name ?? visual.GetType().ToString()}')).", e);
        }

        // ...and recurse. 
        foreach (var child in visual.Children.ToArray().Where(o => o.IsInvalidatedVisual))
            Render(child);
    }

    /// <summary>
    /// Determines whether the given Visual instance is within the bounds of the screen.
    /// </summary>
    private bool IsOnScreen(Visual visual)
    {
        if (visual.ActualX >= Root.Screen.Width || visual.ActualY >= Root.Screen.Height)
            return false; 
        if (visual.ActualX + visual.Width <= 0 || visual.ActualY + visual.Height <= 0)
            return false; 

        return true;
    }

    private static (int x, int y) GetAbsolutePos(Visual visual)
    {
        if (visual == null)
            return (0, 0);

        // Get the origin of the parent.
        var parent = visual.Parent;
        var (x, y) = GetAbsolutePos(parent);

        // Offset by the origin of this visual.
        x += visual.X;
        y += visual.Y;

        if (parent == null)
            return (x, y); // Done - We're at the root visual.
        
        // Apply parent's padding offset.
        x += parent.Padding.Left;
        y += parent.Padding.Top;

        var parentHeight = parent.Height - parent.Padding.TopBottom;
        var parentWidth = parent.Width - parent.Padding.LeftRight;

        // Apply alignment.
        if (visual.VerticalAlignment == VerticalAlignment.Center)
            y += (parentHeight - visual.Height) / 2;
        else if (visual.VerticalAlignment == VerticalAlignment.Bottom)
            y += parentHeight - visual.Height;

        if (visual.HorizontalAlignment == HorizontalAlignment.Center)
            x += (parentWidth - visual.Width) / 2;
        else if (visual.HorizontalAlignment == HorizontalAlignment.Right)
            x += parentWidth - visual.Width;

        return (x, y);
    }

    private static IEnumerable<Visual> GetVisualTree(Visual visual)
    {
        yield return visual;
        foreach (var child in visual.Children.ToArray())
        {
            foreach (var descendant in GetVisualTree(child))
                yield return descendant;
        }
    }

    public T Find<T>(string controlName) where T : Visual =>
        GetVisualTree(Root).FirstOrDefault(o => o.Name == controlName) as T;

    /// <summary>
    /// Represents a root canvas for rendering visuals onto a screen.
    /// </summary>
    private class RootCanvas : Visual
    {
        public RootCanvas(int width, int height) : base(width, height)
        {
        }

        public override void Render(ScreenData _) =>
            IsInvalidatedVisual = false;
    }

    public void QueueEvent(ConsoleEvent consoleEvent)
    {
        if (consoleEvent == null)
            throw new ArgumentNullException(nameof(consoleEvent));

        lock (m_consoleEvents)
            m_consoleEvents.Add(consoleEvent);
    }

    public void ProcessEvents()
    {
        lock (m_consoleEvents)
        {
            var visualTree = GetVisualTree(Root).ToArray();
            foreach (var consoleEvent in m_consoleEvents)
            {
                if (consoleEvent is KeyConsoleEvent keyEvent && keyEvent.Direction == KeyConsoleEvent.KeyDirection.Down)
                {
                    if ((keyEvent.Modifiers & KeyModifiers.Control) != KeyModifiers.None)
                    {
                        switch (keyEvent.Key)
                        {
                            case Key.Up:
                                OffsetY++;
                                Root.InvalidateVisual();
                                continue;
                            case Key.Down:
                                OffsetY--;
                                Root.InvalidateVisual();
                                continue;
                        }
                    }
                    
                    // Any other key resets scroll offset.
                    if (OffsetY != 0 && keyEvent.Modifiers == KeyModifiers.None)
                    {
                        Root.InvalidateVisual();
                        OffsetY = 0;
                    }
                }
                
                foreach (var visual in visualTree)
                {
                    var handled = false;
                    visual.OnEvent(consoleEvent, ref handled);
                    if (handled)
                    {
                        // Visual object has handled the event, so stop propagating it.
                        break;
                    }
                }
            }
            
            m_consoleEvents.Clear();
        }
    }
}