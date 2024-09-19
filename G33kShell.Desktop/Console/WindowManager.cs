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
using Avalonia.Layout;
using G33kShell.Desktop.Skins;

namespace G33kShell.Desktop.Console;

public class WindowManager
{
    private readonly object m_renderLock = new object();
    private SkinBase m_skin;

    public Canvas Root { get; }

    public ScreenData Screen => Root.Screen;

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
            }
        }
    }

    public WindowManager(int width, int height, SkinBase skin)
    {
        Root = new Canvas().Init(width, height);
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
            Screen.ClearChars('\0');
            Screen.ClearColor(Root.Foreground, Root.Background);
            foreach (var visual in GetVisualTree(Root).Skip(1))
            {
                var pos = GetAbsolutePos(visual);
                visual.ActualX = pos.x;
                visual.ActualY = pos.y;
                visual.Screen.CopyTo(Screen, pos.x, pos.y);
            }
        }
    }

    /// <summary>
    /// Recurse through all visual children, asking them to update their own screen data.
    /// </summary>
    /// <remarks>
    /// The collection of screens will be composited later.
    /// </remarks>
    private static void Render(Visual visual)
    {
        // Ask the visual to render its own content.
        if (visual.IsInvalidatedVisual)
            visual.Render();

        // ...and recurse. 
        foreach (var child in visual.Children.Where(o => o.IsInvalidatedVisual))
            Render(child);
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
        foreach (var child in visual.Children)
        {
            foreach (var descendant in GetVisualTree(child))
                yield return descendant;
        }
    }

    public T Find<T>(string controlName) where T : Canvas =>
        GetVisualTree(Root).FirstOrDefault(o => o.Name == controlName) as T;
}