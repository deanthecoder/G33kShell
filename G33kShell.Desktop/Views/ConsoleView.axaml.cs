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
        context.FillRectangle(m_windowManager.Skin.BackgroundColor, Bounds);
        
        // Draw the screen content.
        var screenData = m_windowManager.Screen;
        var currentText = new StringBuilder();
        for (var y = 0; y < screenData.Height; y++)
        {
            var xStart = 0;
            var currentAttr = screenData.Chars[y][0];
            currentText.Clear();

            for (var x = 0; x < screenData.Width; x++)
            {
                var attr = screenData.Chars[y][x];

                // Check if the attribute colors (foreground or background) change
                if (ReferenceEquals(attr.Foreground, currentAttr.Foreground) && Equals(attr.Background, currentAttr.Background))
                {
                    // Same attributes - Collate the characters.
                    currentText.Append(attr.Ch);
                }
                else
                {
                    // Draw the accumulated run with the same foreground and background
                    DrawTextRun(context, currentText.ToString(), xStart, y, currentAttr.Foreground, currentAttr.Background);

                    // Start a new run
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

    private void DrawTextRun(DrawingContext context, string text, int xStart, int y, IBrush foreground, IBrush background)
    {
        // Draw the background rectangle for the entire text run.
        var rect = new Rect(
            xStart * CharWidth + Padding.Left,
            y * CharHeight + Padding.Top,
            text.Length * CharWidth,
            CharHeight
        );
        context.FillRectangle(background ?? Brushes.Black, rect);

        // Draw the text on top of the background.
        var formattedText = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, 1.0, foreground ?? Brushes.White);
        if (m_fontFamily != null)
            formattedText.SetFontFamily(m_fontFamily);
        formattedText.SetFontSize(CharHeight);
        
        context.DrawText(formattedText, rect.TopLeft);
    }
}
