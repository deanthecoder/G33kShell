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

using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CSharp.Core;
using G33kShell.Desktop.Console;

namespace G33kShell.Desktop.Views;

public partial class ConsoleView : Control
{
    private const int CharWidth = 8;
    private const int CharHeight = 16;
    private readonly LruCache<Rgb, IImmutableBrush> m_brushCache = new LruCache<Rgb, IImmutableBrush>(64);
    private readonly LruCache<string, Geometry> m_geometryCache = new LruCache<string, Geometry>(4096);
    private WindowManager m_windowManager;
    private FontFamily m_fontFamily;
    private Thickness m_padding;

    public static readonly DirectProperty<ConsoleView, WindowManager> WindowManagerProperty = AvaloniaProperty.RegisterDirect<ConsoleView, WindowManager>(nameof(WindowManager), o => o.WindowManager, (o, v) => o.WindowManager = v);
    public static readonly DirectProperty<ConsoleView, FontFamily> FontFamilyProperty = AvaloniaProperty.RegisterDirect<ConsoleView, FontFamily>(nameof(FontFamily), o => o.FontFamily, (o, v) => o.FontFamily = v);
    public static readonly DirectProperty<ConsoleView, Thickness> PaddingProperty = AvaloniaProperty.RegisterDirect<ConsoleView, Thickness>(nameof(Padding), o => o.Padding, (o, v) => o.Padding = v);

    public ConsoleView()
    {
        InitializeComponent();
    }
    
    public WindowManager WindowManager
    {
        get => m_windowManager;
        set
        {
            if (!SetAndRaise(WindowManagerProperty, ref m_windowManager, value))
                return;
            Width = WindowManager.Screen.Width * CharWidth + Padding.Left + Padding.Right;
            Height = WindowManager.Screen.Height * CharHeight + Padding.Top + Padding.Bottom;
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

    private ScreenData m_lastFrame;
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (m_windowManager == null)
            return;

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
                    DrawTextRun(context, currentText.ToString(), xStart, y, currentAttrForeground, currentAttrBackground);

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
            {
                DrawTextRun(context, currentText.ToString(), xStart, y, currentAttrForeground, currentAttrBackground);
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
        return formattedText.BuildGeometry(new Point());
    }

    private void DrawTextRun(DrawingContext context, string text, int xStart, int y, Rgb foreground, Rgb background)
    {
        // Draw the background rectangle for the entire text run.
        var rect = new Rect(
            xStart * CharWidth + Padding.Left,
            y * CharHeight + Padding.Top,
            text.Length * CharWidth,
            CharHeight
        );
        
        if (background != null)
            context.FillRectangle(GetBrush(background), rect);

        if (foreground == null)
            return; // The text is invisible.

        if (foreground == background)
            return; // Text is same color as the background.

        // Skip invisible characters at the end.
        if (text[^1] == '\0')
            text = text.TrimEnd(' ', '\0');
        if (text.Length == 0)
            return; // Nothing to render.
        
        // Skip invisible characters at the start.
        var toSkip = 0;
        while (toSkip < text.Length && (text[toSkip] == '\0' || text[toSkip] == ' '))
            toSkip++;
        if (toSkip == text.Length)
            return; // Nothing to render.

        if (toSkip > 0)
        {
            text = text.Substring(toSkip);
            rect = new Rect(rect.X + toSkip * CharWidth, rect.Y, rect.Width - toSkip * CharWidth, rect.Height);
        }

        // Draw the text on top of the background.
        var textGeometry = GetTextGeometry(text);
        using (context.PushTransform(Matrix.CreateTranslation(rect.X, rect.Y)))
            context.DrawGeometry(GetBrush(foreground), null, textGeometry);
    }
}