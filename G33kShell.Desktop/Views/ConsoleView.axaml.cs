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
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using G33kShell.Desktop.Console;

namespace G33kShell.Desktop.Views;

public partial class ConsoleView : Control
{
    private const int CharWidth = 8;
    private const int CharHeight = 16;
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

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (m_windowManager == null)
            return;

        // Render the visual tree.
        m_windowManager.Render();
        
        // Draw the border (in the padding area).
        context.FillRectangle(new SolidColorBrush(m_windowManager.Skin.BackgroundColor), Bounds);
        
        // Draw the screen content.
        var currentText = new StringBuilder();
        using var _ = m_windowManager.Screen.Lock(out var screen);
        for (var y = 0; y < screen.Height; y++)
        {
            var xStart = 0;
            var currentAttr = screen.Chars[y][0];
            currentText.Clear();

            for (var x = 0; x < screen.Width; x++)
            {
                var attr = screen.Chars[y][x];

                // Check if the attribute colors (foreground or background) change.
                if (attr.Foreground.Equals(currentAttr.Foreground) && attr.Background.Equals(currentAttr.Background))
                {
                    // Same attributes - Collate the characters.
                    currentText.Append(attr.Ch);
                }
                else
                {
                    // Draw the accumulated run with the same foreground and background.
                    DrawTextRun(context, currentText.ToString(), xStart, y, currentAttr.Foreground, currentAttr.Background);

                    // Start a new run.
                    currentAttr = attr;
                    currentText.Clear();
                    currentText.Append(attr.Ch);
                    xStart = x;
                }
            }

            // Draw the last accumulated run
            if (currentText.Length > 0)
                DrawTextRun(context, currentText.ToString(), xStart, y, currentAttr.Foreground, currentAttr.Background);
        }
    }

    private readonly Dictionary<Color, SolidColorBrush> m_brushCache = new Dictionary<Color, SolidColorBrush>();
    private readonly Dictionary<(string, Color), FormattedText> m_textCache = new Dictionary<(string, Color), FormattedText>();
    private Task m_stats;

    private SolidColorBrush GetBrush(Color color)
    {
        if (!m_brushCache.ContainsKey(color))
            m_brushCache[color] = new SolidColorBrush(color);
        return m_brushCache[color];
    }

    private FormattedText GetText(string text, Color foreground)
    {
        if (m_textCache.ContainsKey((text, foreground)))
            return m_textCache[(text, foreground)];
        
        var formattedText = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, 1.0, GetBrush(foreground));
        m_textCache[(text, foreground)] = formattedText;
        if (m_fontFamily != null)
            formattedText.SetFontFamily(m_fontFamily);
        formattedText.SetFontSize(CharHeight);
        return formattedText;
    }

    private void DrawTextRun(DrawingContext context, string text, int xStart, int y, Color? foreground, Color? background)
    {
        m_stats ??= Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(1000);
                System.Console.WriteLine($"Brush cache: {m_brushCache.Count} entries, Text cache: {m_textCache.Count} entries.");
            }
        });
        
        // Draw the background rectangle for the entire text run.
        var rect = new Rect(
            xStart * CharWidth + Padding.Left,
            y * CharHeight + Padding.Top,
            text.Length * CharWidth,
            CharHeight
        );
        
        if (background != null)
            context.FillRectangle(GetBrush((Color)background), rect);

        if (foreground == null || ((Color)foreground).A == 0)
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
        var formattedText = GetText(text, (Color)foreground);
        context.DrawText(formattedText, rect.TopLeft);
    }
}
