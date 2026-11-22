// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Linq;
using System.Threading;
using Avalonia.Input;
using DTC.Core;
using G33kShell.Desktop.Skins;
using G33kShell.Desktop.Terminal;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Displays an autonomous native recreation of Golden Axe's five arcade areas and camps.
/// </summary>
/// <remarks>
/// Uses local extracted set-6 assets; _color enables the original artwork colors.
/// </remarks>
[UsedImplicitly]
public sealed class GoldenAxeCanvas : PixelScreensaverBase, IInteractiveScreensaver
{
    private readonly ArcadeData[] m_areas = new ArcadeData[ArcadeData.AreaCount];
    private ArcadeData m_data;
    private Game m_game;
    private ArcadeRenderer m_renderer;
    private readonly CodedController m_controller = new();
    private readonly KeyboardController m_keyboard = new();
    private double m_lastTime;
    private double m_accumulator;
    private int m_endTicks;
    private int m_requestedCharacter = -1;
    private Rgb[][] m_fadePalettes;

    public GoldenAxeCanvas(int width, int height) : base(width, height, 60) => Name = "goldenaxe";
    public override bool IsReadyToRun => ArcadeData.IsAvailable;
    private int StartingArea => Enumerable.Range(0, ArcadeData.AreaCount).LastOrDefault(i => HasSwitch($"area{i + 1}"));
    public bool IsInteractive => HasSwitch("play");
    public void SetKey(Key key, bool down)
    {
        var character = key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            _ => -1
        };
        if (character >= 0)
        {
            if (down && Settings.Instance.GoldenAxeCharacter != character)
            {
                Settings.Instance.GoldenAxeCharacter = character;
                Settings.Instance.Save();
                Interlocked.Exchange(ref m_requestedCharacter, character);
            }
            return;
        }
        m_keyboard.SetKey(key, down);
    }
    public void ClearInput() => m_keyboard.Clear();

    public override void StopScreensaver()
    {
        ClearInput();
        base.StopScreensaver();
    }

    public override void OnLoaded(WindowManager windowManager)
    {
        if (IsReadyToRun)
        {
            LoadArea(0);
        }
        base.OnLoaded(windowManager);
    }

    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);
        if (!IsReadyToRun)
            screen.PrintAt(1, 1, "Golden Axe: local arcade data is missing. See Assets/GoldenAxe/NOTICE.txt.");
    }

    public override void StartScreensaver(ScreenData shellScreen)
    {
        if (m_data == null || WindowManager == null)
            return;
        ClearInput();
        LoadArea(StartingArea);
        m_game = new Game(m_data, Settings.Instance.GoldenAxeCharacter);
        m_endTicks = 0;
        m_fadePalettes = null;
        m_lastTime = Time;
        m_accumulator = 0;
        StartPixelScreen(ArcadeData.Width, ArcadeData.Height, reduceCrtGlow: HasSwitch("color"));
    }

    public override void UpdateFrame(ScreenData screen)
    {
        if (!IsPixelScreenActive || PixelScreen == null || m_game == null)
            return;
        var now = Time;
        m_accumulator += Math.Clamp(now - m_lastTime, 0, 0.1);
        m_lastTime = now;
        while (m_accumulator >= 1.0 / 60)
        {
            m_accumulator -= 1.0 / 60;
            var requested = Interlocked.Exchange(ref m_requestedCharacter, -1);
            if (requested >= 0)
                m_game.SelectCharacter(requested);
            m_game.Tick(IsInteractive ? m_keyboard.Read() : m_controller.Read(m_game));
            if ((m_game.Complete || m_game.GameOver) && ++m_endTicks >= 180)
            {
                ClearInput();
                if (m_game.Complete && m_game.AreaIndex + 1 < m_areas.Length)
                {
                    if (!m_game.IsCamp)
                    {
                        m_game.StartCamp();
                        m_fadePalettes = null;
                    }
                    else
                    {
                        LoadArea(m_game.AreaIndex + 1);
                        m_game = m_game.ContinueIn(m_data);
                    }
                }
                else
                {
                    OnCycleCompleted();
                    LoadArea(StartingArea);
                    m_game = new Game(m_data, Settings.Instance.GoldenAxeCharacter);
                }
                m_endTicks = 0;
            }
        }
        using (PixelScreen.Lock(out var pixels))
        {
            var fade = m_endTicks > 135 ? Math.Clamp((180 - m_endTicks) * 30 / 45, 0, 30) : Math.Min(30, m_game.TickCount);
            pixels.SetPalette(GetFadePalette(fade));
            m_renderer.Draw(pixels, m_game, HasSwitch("fx"), HasSwitch("color") ? null : Foreground, (byte)(255 * fade / 30));
        }
    }

    private void LoadArea(int area)
    {
        m_data = m_areas[area] ??= new ArcadeData(ArcadeData.AssetDirectory, area);
        m_renderer = new ArcadeRenderer(m_data);
        m_fadePalettes = null;
    }

    public override void OnSkinChanged(SkinBase skin)
    {
        m_fadePalettes = null;
        base.OnSkinChanged(skin);
    }

    private Rgb[] GetFadePalette(int level)
    {
        if (m_fadePalettes == null)
        {
            var source = GetPixelPalette();
            var background = source[m_data.Black];
            m_fadePalettes = new Rgb[31][];
            for (var step = 0; step <= 30; step++)
            {
                m_fadePalettes[step] = source.Select(c => new Rgb(
                    (byte)(background.R + (c.R - background.R) * step / 30),
                    (byte)(background.G + (c.G - background.G) * step / 30),
                    (byte)(background.B + (c.B - background.B) * step / 30))).ToArray();
            }
        }
        return m_fadePalettes[level];
    }

    protected override Rgb[] GetPixelPalette() => m_data == null ? [Rgb.Black] : HasSwitch("color")
        ? m_data.Palette
        : PixelScreenData.CreateGreyscalePalette(m_data.Palette, Background ?? Rgb.Black, Foreground ?? Rgb.White);
}
