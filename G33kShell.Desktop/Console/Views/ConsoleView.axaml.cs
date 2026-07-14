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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DTC.Core;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Events;
using SkiaSharp;

namespace G33kShell.Desktop.Console.Views;

public partial class ConsoleView : Control
{
    private const int CharWidth = 8;
    private const int CharHeight = 16;
    private const int GlyphCacheCapacity = 1024;
    private readonly LruCache<Rgb, IImmutableBrush> m_brushCache = new LruCache<Rgb, IImmutableBrush>(512);
    private readonly LruCache<char, GlyphMask> m_glyphCache = new LruCache<char, GlyphMask>(GlyphCacheCapacity);
    private WindowManager m_windowManager;
    private FontFamily m_fontFamily;
    private Thickness m_padding;
    private TopLevel m_topLevel;
    private WriteableBitmap m_pixelBitmap;
    private int[] m_pixelBuffer;
    private WriteableBitmap m_textBitmap;
    private int[] m_textBuffer;
    private CellSnapshot[] m_lastCells;
    private SKTypeface m_glyphTypeface;
    private SKPaint m_glyphPaint;
    private long m_textBitmapUploads;
    private long m_cellsComposed;

    public static readonly DirectProperty<ConsoleView, WindowManager> WindowManagerProperty = AvaloniaProperty.RegisterDirect<ConsoleView, WindowManager>(nameof(WindowManager), o => o.WindowManager, (o, v) => o.WindowManager = v);
    public static readonly DirectProperty<ConsoleView, FontFamily> FontFamilyProperty = AvaloniaProperty.RegisterDirect<ConsoleView, FontFamily>(nameof(FontFamily), o => o.FontFamily, (o, v) => o.FontFamily = v);
    public static readonly DirectProperty<ConsoleView, Thickness> PaddingProperty = AvaloniaProperty.RegisterDirect<ConsoleView, Thickness>(nameof(Padding), o => o.Padding, (o, v) => o.Padding = v);

    public ConsoleView()
    {
        InitializeComponent();

        Loaded += OnLoaded;

        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DetachTopLevelHandlers();
        DisposeRenderResources();
        base.OnDetachedFromVisualTree(e);
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

        DrawPixelScreen(context);
        
        // Compose all character cells into a single bitmap. Only changed cells are recomputed,
        // and Avalonia receives one image draw instead of many native text geometries.
        DrawTextScreen(context);

        using var _ = m_windowManager.Screen.Lock(out var screen);
        
        // Draw the screen cursor.
        var cursor = m_windowManager.Cursor;
        if (cursor?.IsVisible != true)
            return;
        var cursorY = cursor.Y + m_windowManager.OffsetY;
        if (cursorY < 0 || cursorY >= screen.Height)
            return;
        if (cursor.X < 0 || cursor.X >= screen.Width)
            return;
        var foregroundColor = m_windowManager.Skin.ForegroundColor;
        if (cursor.IsBusy)
            return;
        var isFlashOn = (Environment.TickCount64 - cursor.MoveTime) % 1000 < 500;
        if (!isFlashOn)
            return;
        var cursorRect = new Rect(cursor.X * CharWidth + Padding.Left, cursorY * CharHeight + Padding.Top, CharWidth, CharHeight);
        context.FillRectangle(GetBrush(foregroundColor), cursorRect);
    }

    private IImmutableBrush GetBrush(Rgb color) =>
        m_brushCache.GetOrAdd(color, o => new SolidColorBrush(o).ToImmutable());

    private void DrawPixelScreen(DrawingContext context)
    {
        var pixelScreenLock = m_windowManager.PixelScreen;
        if (pixelScreenLock == null)
        {
            m_pixelBitmap?.Dispose();
            m_pixelBitmap = null;
            m_pixelBuffer = null;
            return;
        }

        using (pixelScreenLock.Lock(out var pixelScreen))
        {
            if (m_pixelBitmap == null ||
                m_pixelBitmap.PixelSize.Width != pixelScreen.Width ||
                m_pixelBitmap.PixelSize.Height != pixelScreen.Height)
            {
                m_pixelBitmap?.Dispose();
                m_pixelBitmap = new WriteableBitmap(
                    new PixelSize(pixelScreen.Width, pixelScreen.Height),
                    new Vector(96, 96),
                    PixelFormats.Bgra8888,
                    AlphaFormat.Opaque);
                m_pixelBuffer = new int[pixelScreen.Width * pixelScreen.Height];
            }

            for (var i = 0; i < pixelScreen.Pixels.Length; i++)
            {
                var color = pixelScreen.Palette[pixelScreen.Pixels[i]];
                m_pixelBuffer[i] = color.B | (color.G << 8) | (color.R << 16) | unchecked((int)0xff000000);
            }

            using (var frameBuffer = m_pixelBitmap.Lock())
            {
                var width = pixelScreen.Width;
                var height = pixelScreen.Height;
                var expectedRowBytes = width * 4;
                if (frameBuffer.RowBytes == expectedRowBytes)
                {
                    Marshal.Copy(m_pixelBuffer, 0, frameBuffer.Address, m_pixelBuffer.Length);
                }
                else
                {
                    for (var y = 0; y < height; y++)
                    {
                        var rowAddress = frameBuffer.Address + y * frameBuffer.RowBytes;
                        Marshal.Copy(m_pixelBuffer, y * width, rowAddress, width);
                    }
                }
            }

            var sourceRect = new Rect(0, 0, pixelScreen.Width, pixelScreen.Height);
            var targetRect = new Rect(
                Padding.Left,
                Padding.Top,
                m_windowManager.Screen.Width * CharWidth,
                m_windowManager.Screen.Height * CharHeight);

            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                context.DrawImage(m_pixelBitmap, sourceRect, targetRect);
        }
    }

    private void DrawTextScreen(DrawingContext context)
    {
        var changed = false;
        using (m_windowManager.Screen.Lock(out var screen))
        {
            var screenWidth = screen.Width;
            var screenHeight = screen.Height;
            EnsureTextBitmap(screenWidth, screenHeight);

            for (var y = 0; y < screenHeight; y++)
            {
                var row = screen.Chars[y];
                var cellIndex = y * screenWidth;
                for (var x = 0; x < screenWidth; x++, cellIndex++)
                {
                    var attr = row[x];
                    var foreground = attr.Foreground;
                    var background = attr.Background;
                    var foregroundBgra = ToBgra(foreground);
                    var backgroundBgra = ToBgra(background);
                    ref var previous = ref m_lastCells[cellIndex];
                    if (previous.Matches(attr.Ch, foregroundBgra, backgroundBgra))
                        continue;

                    previous = new CellSnapshot(attr.Ch, foregroundBgra, backgroundBgra);
                    ComposeCell(x, y, attr.Ch, foreground, background, foregroundBgra, backgroundBgra);
                    changed = true;
                    m_cellsComposed++;
                }
            }

            var cursor = m_windowManager.Cursor;
            var cursorY = (cursor?.Y ?? -1) + m_windowManager.OffsetY;
            if (cursor is { IsVisible: true, IsBusy: true, X: >= 0 } && cursor.X < screenWidth && cursorY >= 0 && cursorY < screenHeight)
            {
                const string animChars = "/-\\|";
                var animFrame = (int)((Environment.TickCount64 / 100) % animChars.Length);
                var foreground = m_windowManager.Skin.ForegroundColor;
                var background = screen.Chars[cursorY][cursor.X].Background;
                ComposeCell(cursor.X, cursorY, animChars[animFrame], foreground, background, ToBgra(foreground), ToBgra(background));
                m_lastCells[cursorY * screenWidth + cursor.X] = CellSnapshot.Uninitialized;
                changed = true;
                m_cellsComposed++;
            }
        }

        if (changed)
        {
            using var frameBuffer = m_textBitmap.Lock();
            CopyToFramebuffer(m_textBuffer, frameBuffer, m_textBitmap.PixelSize.Width, m_textBitmap.PixelSize.Height);
            m_textBitmapUploads++;
        }

        var sourceRect = new Rect(0, 0, m_textBitmap.PixelSize.Width, m_textBitmap.PixelSize.Height);
        var targetRect = new Rect(Padding.Left, Padding.Top, sourceRect.Width, sourceRect.Height);
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            context.DrawImage(m_textBitmap, sourceRect, targetRect);
    }

    private void EnsureTextBitmap(int screenWidth, int screenHeight)
    {
        var pixelSize = new PixelSize(screenWidth * CharWidth, screenHeight * CharHeight);
        if (m_textBitmap?.PixelSize == pixelSize)
            return;

        m_textBitmap?.Dispose();
        m_textBitmap = new WriteableBitmap(pixelSize, new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Premul);
        m_textBuffer = new int[pixelSize.Width * pixelSize.Height];
        m_lastCells = new CellSnapshot[screenWidth * screenHeight];
        Array.Fill(m_lastCells, CellSnapshot.Uninitialized);
    }

    private void ComposeCell(int cellX, int cellY, char ch, Rgb foregroundColor, Rgb backgroundColor, int foreground, int background)
    {
        var bufferWidth = m_textBitmap.PixelSize.Width;
        var pixelX = cellX * CharWidth;
        var pixelY = cellY * CharHeight;

        for (var y = 0; y < CharHeight; y++)
            Array.Fill(m_textBuffer, background, (pixelY + y) * bufferWidth + pixelX, CharWidth);

        if (foregroundColor == null || ch is '\0' or ' ' || foregroundColor == backgroundColor)
            return;

        var mask = m_glyphCache.GetOrAdd(ch, RasterizeGlyph);
        var maskOffset = 0;
        for (var y = 0; y < CharHeight; y++)
        {
            var bufferOffset = (pixelY + y) * bufferWidth + pixelX;
            for (var x = 0; x < CharWidth; x++)
            {
                var coverage = mask.Coverage[maskOffset++];
                if (coverage == 0)
                    continue;
                m_textBuffer[bufferOffset + x] = coverage == byte.MaxValue
                    ? foreground
                    : BlendPixel(foregroundColor, backgroundColor, coverage);
            }
        }
    }

    private GlyphMask RasterizeGlyph(char ch)
    {
        EnsureGlyphPaint();
        using var bitmap = new SKBitmap(new SKImageInfo(CharWidth, CharHeight, SKColorType.Alpha8, SKAlphaType.Unpremul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawText(ch.ToString(), 0, CharHeight - 1, m_glyphPaint);

        var pixels = bitmap.Bytes;
        var coverage = new byte[CharWidth * CharHeight];
        for (var y = 0; y < CharHeight; y++)
        {
            for (var x = 0; x < CharWidth; x++)
                coverage[y * CharWidth + x] = pixels[y * bitmap.RowBytes + x];
        }

        return new GlyphMask(coverage);
    }

    private void EnsureGlyphPaint()
    {
        if (m_glyphPaint != null)
            return;

        var fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "VGA", "PxPlus_IBM_VGA8.ttf");
        m_glyphTypeface = SKTypeface.FromFile(fontPath) ?? throw new FileNotFoundException("Could not load the console font.", fontPath);
        m_glyphPaint = new SKPaint
        {
            Color = SKColors.White,
            Typeface = m_glyphTypeface,
            TextSize = CharHeight,
            IsAntialias = true,
            IsStroke = false,
            LcdRenderText = false,
            SubpixelText = false
        };
    }

    private static int BlendPixel(Rgb foreground, Rgb background, byte coverage)
    {
        if (background == null)
        {
            var b = foreground.B * coverage / 255;
            var g = foreground.G * coverage / 255;
            var r = foreground.R * coverage / 255;
            return b | (g << 8) | (r << 16) | (coverage << 24);
        }

        var inverse = 255 - coverage;
        var blendedB = (foreground.B * coverage + background.B * inverse) / 255;
        var blendedG = (foreground.G * coverage + background.G * inverse) / 255;
        var blendedR = (foreground.R * coverage + background.R * inverse) / 255;
        return blendedB | (blendedG << 8) | (blendedR << 16) | unchecked((int)0xff000000);
    }

    private static int ToBgra(Rgb color) =>
        color == null ? 0 : color.B | (color.G << 8) | (color.R << 16) | unchecked((int)0xff000000);

    private static void CopyToFramebuffer(int[] source, ILockedFramebuffer destination, int width, int height)
    {
        if (destination.RowBytes == width * sizeof(int))
        {
            Marshal.Copy(source, 0, destination.Address, source.Length);
            return;
        }

        for (var y = 0; y < height; y++)
            Marshal.Copy(source, y * width, destination.Address + y * destination.RowBytes, width);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null || topLevel == m_topLevel)
            return;

        DetachTopLevelHandlers();
        m_topLevel = topLevel;
        m_topLevel.KeyDown += OnTopLevelKeyDown;
        m_topLevel.KeyUp += OnTopLevelKeyUp;
    }

    private void DetachTopLevelHandlers()
    {
        if (m_topLevel == null)
            return;

        m_topLevel.KeyDown -= OnTopLevelKeyDown;
        m_topLevel.KeyUp -= OnTopLevelKeyUp;
        m_topLevel = null;
    }

    internal string GetMemoryDetails()
    {
        var pixelBitmapBytes = m_pixelBitmap == null
            ? 0L
            : (long)m_pixelBitmap.PixelSize.Width * m_pixelBitmap.PixelSize.Height * 4;
        var pixelBitmap = m_pixelBitmap == null
            ? "none"
            : $"{m_pixelBitmap.PixelSize.Width:N0}x{m_pixelBitmap.PixelSize.Height:N0}/{pixelBitmapBytes.ToSize()}";
        var pixelBufferBytes = (long)(m_pixelBuffer?.Length ?? 0) * sizeof(int);
        var textBitmapBytes = (long)(m_textBuffer?.Length ?? 0) * sizeof(int);
        var glyphMaskBytes = (long)m_glyphCache.Count * CharWidth * CharHeight;
        return $"render caches brushes={m_brushCache.Count:N0}, glyphs={m_glyphCache.Count:N0}/{glyphMaskBytes.ToSize()} (8bpp masks); " +
               $"text bitmap={textBitmapBytes.ToSize()}, uploads={m_textBitmapUploads:N0}, cells composed={m_cellsComposed:N0}; " +
               $"pixel bitmap={pixelBitmap}, buffer={pixelBufferBytes.ToSize()}";
    }

    private void OnTopLevelKeyDown(object sender, KeyEventArgs e)
    {
        m_windowManager?.QueueEvent(new KeyConsoleEvent(e.Key, e.KeyModifiers, KeyConsoleEvent.KeyDirection.Down, e.KeySymbol));
        InvalidateVisual();
    }

    private void OnTopLevelKeyUp(object sender, KeyEventArgs e)
    {
        m_windowManager?.QueueEvent(new KeyConsoleEvent(e.Key, e.KeyModifiers, KeyConsoleEvent.KeyDirection.Up, e.KeySymbol));
        InvalidateVisual();
    }

    private void DisposeRenderResources()
    {
        m_pixelBitmap?.Dispose();
        m_pixelBitmap = null;
        m_pixelBuffer = null;
        m_textBitmap?.Dispose();
        m_textBitmap = null;
        m_textBuffer = null;
        m_lastCells = null;
        m_glyphPaint?.Dispose();
        m_glyphPaint = null;
        m_glyphTypeface?.Dispose();
        m_glyphTypeface = null;
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        var items = e.Data.GetFiles();
        e.DragEffects = items?.Any() == true ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var items = e.Data.GetFiles()?.Select(storageItem => storageItem.Path.LocalPath).ToArray();
        if (items is { Length: > 0 })
            WindowManager.QueueEvent(new PasteEvent(items));
    }

    private sealed record GlyphMask(byte[] Coverage);

    private readonly record struct CellSnapshot(char Ch, int Foreground, int Background, bool IsInitialized = true)
    {
        public static CellSnapshot Uninitialized { get; } = new('\0', 0, 0, false);

        public bool Matches(char ch, int foreground, int background) =>
            IsInitialized && Ch == ch && Foreground == foreground && Background == background;
    }
}
