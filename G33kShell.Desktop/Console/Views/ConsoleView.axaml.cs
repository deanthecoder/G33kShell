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
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CSharp.Core;
using G33kShell.Desktop.Console.Events;

namespace G33kShell.Desktop.Console.Views;

public partial class ConsoleView : Control
{
    private const int CharWidth = 8;
    private const int CharHeight = 16;
    private readonly Point m_zeroPoint = new Point();
    private readonly LruCache<Rgb, IImmutableBrush> m_brushCache = new LruCache<Rgb, IImmutableBrush>(512);
    private readonly LruCache<string, Geometry> m_geometryCache = new LruCache<string, Geometry>(4096);
    private WindowManager m_windowManager;
    private FontFamily m_fontFamily;
    private Thickness m_padding;
    private ScreenData m_lastFrame;

    public static readonly DirectProperty<ConsoleView, WindowManager> WindowManagerProperty = AvaloniaProperty.RegisterDirect<ConsoleView, WindowManager>(nameof(WindowManager), o => o.WindowManager, (o, v) => o.WindowManager = v);
    public static readonly DirectProperty<ConsoleView, FontFamily> FontFamilyProperty = AvaloniaProperty.RegisterDirect<ConsoleView, FontFamily>(nameof(FontFamily), o => o.FontFamily, (o, v) => o.FontFamily = v);
    public static readonly DirectProperty<ConsoleView, Thickness> PaddingProperty = AvaloniaProperty.RegisterDirect<ConsoleView, Thickness>(nameof(Padding), o => o.Padding, (o, v) => o.Padding = v);

    public ConsoleView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            var topLevel = TopLevel.GetTopLevel(this)!;
            topLevel.KeyDown += (_, e) => m_windowManager.QueueEvent(new KeyConsoleEvent(e.Key, e.KeyModifiers, KeyConsoleEvent.KeyDirection.Down));
            topLevel.KeyUp += (_, e) => m_windowManager.QueueEvent(new KeyConsoleEvent(e.Key, e.KeyModifiers, KeyConsoleEvent.KeyDirection.Up));
        };

        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    public WindowManager WindowManager
    {
        get => m_windowManager;
        set
        {
            if (!SetAndRaise(WindowManagerProperty, ref m_windowManager, value))
                return;
            Width = m_windowManager.Screen.Width * CharWidth + Padding.Left + Padding.Right;
            Height = m_windowManager.Screen.Height * CharHeight + Padding.Top + Padding.Bottom;
        }
    }
    
    public FontFamily FontFamily
    {
        get => m_fontFamily;
        set => SetAndRaise(FontFamilyProperty, ref m_fontFamily, value);
    }
    
    public Thickness Padding
    {
        get => m_padding;
        set => SetAndRaise(PaddingProperty, ref m_padding, value);
    }
    
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (m_windowManager == null)
            return;
        
        // Allow console objects to process queued events.
        m_windowManager.ProcessEvents();

        // Render the visual tree.
        m_windowManager.Render();
        
        // Draw the border (in the padding area).
        context.FillRectangle(GetBrush(m_windowManager.Skin.BackgroundColor), Bounds);
        
        // Draw the screen content.
        var currentText = new StringBuilder();
        using var _ = m_windowManager.Screen.Lock(out var screen);

        if (m_lastFrame == null)
        {
            m_lastFrame = new ScreenData(screen.Width, screen.Height);
            CopyScreenToLastFrame(screen);
        }

        for (var y = 0; y < screen.Height; y++)
        {
            var xStart = 0;
            var currentAttrForeground = screen.Chars[y][0].Foreground;
            var currentAttrBackground = screen.Chars[y][0].Background;
            currentText.Clear();

            for (var x = 0; x < screen.Width; x++)
            {
                var attr = screen.Chars[y][x];
                var attrForeground = attr.Foreground;
                var attrBackground = attr.Background;

                // Check if the attribute colors (foreground or background) change.
                if (attrForeground == currentAttrForeground && attrBackground == currentAttrBackground)
                {
                    // Same attributes - Collate the characters.
                    currentText.Append(attr.Ch);
                }
                else
                {
                    // Draw the accumulated run with the same foreground and background.
                    DrawTextRun(context, currentText, xStart, y, currentAttrForeground, currentAttrBackground);

                    // Start a new run.
                    currentAttrForeground = attrForeground;
                    currentAttrBackground = attrBackground;
                    currentText.Clear();
                    currentText.Append(attr.Ch);
                    xStart = x;
                }
            }

            // Draw the last accumulated run
            if (currentText.Length > 0)
                DrawTextRun(context, currentText, xStart, y, currentAttrForeground, currentAttrBackground);
        }
        
        // Draw the screen cursor.
        var cursor = m_windowManager.Cursor;
        if (cursor?.IsVisible == true && cursor.X >= 0 && cursor.X < screen.Width && cursor.Y >= 0 && cursor.Y < screen.Height)
        {
            var foregroundColor = m_windowManager.Skin.ForegroundColor;
            if (cursor.IsBusy)
            {
                var animChars = "/-\\|";
                var animFrame = (Environment.TickCount64 / 100) % animChars.Length;
                DrawTextRun(context, new StringBuilder(animChars, (int)animFrame, 1, 1), cursor.X, cursor.Y, foregroundColor, screen.Chars[cursor.Y][cursor.X].Background);
            }
            else
            {
                var isFlashOn = (Environment.TickCount64 - cursor.MoveTime) % 1000 < 500;
                if (isFlashOn)
                {
                    var cursorRect = new Rect(cursor.X * CharWidth + Padding.Left, cursor.Y * CharHeight + Padding.Top, CharWidth, CharHeight);
                    context.FillRectangle(GetBrush(foregroundColor), cursorRect);
                }
            }
        }
    }

    private void CopyScreenToLastFrame(ScreenData screen)
    {
        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Width; x++)
                m_lastFrame.Chars[y][x].Set(screen.Chars[y][x]);
        }
    }

    private IImmutableBrush GetBrush(Rgb color) =>
        m_brushCache.GetOrAdd(color, o => new SolidColorBrush(o).ToImmutable());

    private Geometry GetTextGeometry(string text) =>
        m_geometryCache.GetOrAdd(text, BuildTextGeometry);

    private Geometry BuildTextGeometry(string text)
    {
        var formattedText = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, 1.0, Brushes.White);
        if (m_fontFamily != null)
            formattedText.SetFontFamily(m_fontFamily);
        formattedText.SetFontSize(CharHeight);
        return formattedText.BuildGeometry(m_zeroPoint);
    }

    private void DrawTextRun(DrawingContext context, StringBuilder s, int xStart, int y, Rgb foreground, Rgb background)
    {
        // Draw the background rectangle for the entire text run.
        var rect = new Rect(
            xStart * CharWidth + Padding.Left,
            y * CharHeight + Padding.Top,
            s.Length * CharWidth,
            CharHeight
        );
        
        if (background != null)
            context.FillRectangle(GetBrush(background), rect);

        if (foreground == null)
            return; // The text is invisible.

        if (foreground == background)
            return; // Text is same color as the background.

        // Skip invisible characters at the start.
        var start = 0;
        var end = s.Length;
        while (start < end && (s[start] == ' ' || s[start] == '\0'))
            start++;

        // Skip invisible characters at the end
        while (start < end && (s[end - 1] == ' ' || s[end - 1] == '\0'))
            end--;

        if (start == end)
            return; // Nothing to draw.

        // Draw the text on top of the background.
        var x = rect.X + start * CharWidth;
        var textGeometry = GetTextGeometry(s.ToString(start, end - start));
        using (context.PushTransform(Matrix.CreateTranslation(x, rect.Y)))
            context.DrawGeometry(GetBrush(foreground), null, textGeometry);
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        var items = e.Data.GetFiles();
        e.DragEffects = items?.Any() == true ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var items = e.Data.GetFiles()?.Select(storageItem => storageItem.Path.LocalPath).ToArray();
        if (items?.Any() == true)
            WindowManager.QueueEvent(new PasteEvent(items));
    }
}