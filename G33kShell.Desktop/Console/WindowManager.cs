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

            Root.Foreground = m_skin.ForegroundColor;
            Root.Background = m_skin.BackgroundColor;
        }
    }

    public WindowManager(int width, int height)
    {
        Root = new Canvas().Init(width, height);
        Skin = new RetroMonoDos();
    }

    public void Render()
    {
        lock (m_renderLock)
        {
            if (!Root.IsInvalidatedVisual)
                return; // No changes have been made - Stop here.

            // Render the entire view hierarchy.
            Render(Root);

            // Now blit each child onto our top-level 'screen'.
            Screen.Clear();
            foreach (var textArea in GetVisualTree(Root).Skip(1))
            {
                var pos = GetAbsolutePos(textArea);
                textArea.Screen.CopyTo(Screen, pos.x, pos.y);
            }
        }
    }

    private static void Render(Canvas canvas)
    {
        // Ask the text area to render its own content.
        if (canvas.IsInvalidatedVisual)
            canvas.Render();

        // ...and recurse. 
        foreach (var child in canvas.Children.Where(o => o.IsInvalidatedVisual))
            Render(child);
    }

    private static (int x, int y) GetAbsolutePos(Canvas canvas)
    {
        if (canvas == null)
            return (0, 0);

        // Get the origin of the parent.
        var parent = canvas.Parent;
        var (x, y) = GetAbsolutePos(parent);

        // Offset by the origin of this canvas.
        x += canvas.X;
        y += canvas.Y;

        if (parent == null)
            return (x, y); // Done - We're at the root canvas.
        
        // Apply parent's padding offset.
        x += parent.Padding.Left;
        y += parent.Padding.Top;

        var parentHeight = parent.Height - parent.Padding.TopBottom;
        var parentWidth = parent.Width - parent.Padding.LeftRight;

        // Apply alignment.
        if (canvas.VerticalAlignment == VerticalAlignment.Center)
            y += (parentHeight - canvas.Height) / 2;
        else if (canvas.VerticalAlignment == VerticalAlignment.Bottom)
            y += parentHeight - canvas.Height;

        if (canvas.HorizontalAlignment == HorizontalAlignment.Center)
            x += (parentWidth - canvas.Width) / 2;
        else if (canvas.HorizontalAlignment == HorizontalAlignment.Right)
            x += parentWidth - canvas.Width;

        return (x, y);
    }

    private static IEnumerable<Canvas> GetVisualTree(Canvas canvas)
    {
        yield return canvas;
        foreach (var child in canvas.Children)
        {
            foreach (var descendant in GetVisualTree(child))
                yield return descendant;
        }
    }

    public T Find<T>(string controlName) where T : Canvas =>
        GetVisualTree(Root).FirstOrDefault(o => o.Name == controlName) as T;
}