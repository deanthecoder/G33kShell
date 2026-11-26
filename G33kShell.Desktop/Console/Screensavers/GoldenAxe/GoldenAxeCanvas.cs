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
    private const int EndFadeTicks = 45;
    private const int StageEndTicks = 180;
    private const int IntroFadeTicks = 20;
    private const int IntroDurationTicks = 120 + IntroFadeTicks * 2;
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
    private ArcadeTitleCard m_intro;
    private Rgb[] m_introPalette;
    private int m_introTicks;
    private ArcadeTitleCard m_title;
    private AnimatedTitle m_animatedTitle;
    private Rgb[] m_titlePalette;
    private int m_titleTicks;
    private bool m_fadeInTitle;
    private ArcadeMapRecording m_map;
    private int[] m_mapTint;
    private double m_mapSeconds;

    public GoldenAxeCanvas(int width, int height) : base(width, height, 60) => Name = "goldenaxe";
    public override bool IsReadyToRun => ArcadeData.IsAvailable;
    private int StartingStage => Enumerable.Range(1, ArcadeData.AreaCount * 2 - 1).LastOrDefault(i => HasSwitch($"stage{i}"), 1);
    private int EndDelayTicks => m_game.RescuingCaptives
        ? CastleRescue.Duration : m_game.IsCamp && m_game.Complete ? EndFadeTicks : StageEndTicks;
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
        m_map?.Dispose();
        m_map = null;
        m_intro = ArcadeTitleCard.Load("intro.png", m_data.Palette.Length);
        m_introPalette = null;
        m_introTicks = m_intro == null ? 0 : IntroDurationTicks;
        m_title = ArcadeTitleCard.TryLoad(System.IO.Path.Combine(ArcadeData.LocalAssetDirectory, "title.png"), m_data.Palette.Length);
        m_animatedTitle = m_title == null ? AnimatedTitle.Load(m_data) : null;
        if (m_title == null && m_animatedTitle == null)
            m_title = ArcadeTitleCard.Load("title.png", m_data.Palette.Length);
        m_titlePalette = null;
        StartCycle();
        m_lastTime = Time;
        m_accumulator = 0;
        StartPixelScreen(ArcadeData.Width, ArcadeData.Height, reduceCrtGlow: HasSwitch("color"));
    }

    public override void UpdateFrame(ScreenData screen)
    {
        if (!IsPixelScreenActive || PixelScreen == null || m_game == null)
            return;
        var now = Time;
        var elapsed = now - m_lastTime;
        m_lastTime = now;
        if (m_map == null)
            m_accumulator += Math.Clamp(elapsed, 0, 0.1);
        else
        {
            m_mapSeconds += Math.Max(0, elapsed);
            if (!m_map.AdvanceTo(m_mapSeconds))
            {
                using (PixelScreen.Lock(out var mapEnd))
                    m_map.Draw(mapEnd, null, double.PositiveInfinity);
                m_map.Dispose();
                m_map = null;
                ClearInput();
                FinishMap();
                return;
            }
        }
        while (m_map == null && m_accumulator >= 1.0 / 60)
        {
            m_accumulator -= 1.0 / 60;
            if (m_introTicks > 0)
            {
                if (--m_introTicks == 0)
                    ClearInput();
                continue;
            }
            if (m_titleTicks > 0)
            {
                if (--m_titleTicks == 0)
                    ClearInput();
                continue;
            }

            var requested = Interlocked.Exchange(ref m_requestedCharacter, -1);
            if (requested >= 0)
                m_game.SelectCharacter(requested);
            m_game.Tick(IsInteractive ? m_keyboard.Read() : m_controller.Read(m_game));
            if ((m_game.Complete || m_game.GameOver) && ++m_endTicks >= EndDelayTicks)
            {
                ClearInput();
                if (m_game.GameOver)
                {
                    OnCycleCompleted();
                    StartCycle();
                }
                else if (!m_game.IsCamp && m_game.AreaIndex + 1 < m_areas.Length)
                {
                    m_game.StartCamp();
                    m_fadePalettes = null;
                }
                else
                {
                    m_map = ArcadeMapRecording.Load(m_game.AreaIndex);
                    if (m_map == null)
                        FinishMap();
                    else
                    {
                        m_mapSeconds = 0;
                        m_accumulator = 0;
                        m_lastTime = Time;
                    }
                }
                m_endTicks = 0;
            }
        }
        using (PixelScreen.Lock(out var pixels))
        {
            if (m_map != null)
            {
                pixels.OutputColorCurve = null;
                if (!HasSwitch("color"))
                    m_mapTint ??= ArcadeMapRecording.CreateTint(Background ?? Rgb.Black, Foreground ?? Rgb.White);
                m_map.Draw(pixels, HasSwitch("color") ? null : m_mapTint, m_mapSeconds);
                return;
            }

            if (m_introTicks > 0)
            {
                m_introPalette ??= OpeningPalette(m_intro.Palette);
                var introFade = Math.Min(IntroDurationTicks - m_introTicks, m_introTicks);
                m_intro.Draw(pixels, m_introPalette, Math.Min(1.0, introFade / (double)IntroFadeTicks));
                return;
            }

            if (m_titleTicks > 0)
            {
                m_titlePalette ??= OpeningPalette(m_animatedTitle?.Palette ?? m_title.Palette);
                if (m_animatedTitle != null)
                {
                    var tick = AnimatedTitle.DurationTicks - m_titleTicks;
                    m_animatedTitle.Draw(pixels, m_titlePalette, tick);
                    if (m_fadeInTitle && tick < IntroFadeTicks)
                        ArcadeTitleCard.ApplyBrightness(pixels, tick / (double)IntroFadeTicks);
                    return;
                }
                var brightness = m_fadeInTitle
                    ? Math.Min(1.0, (ArcadeTitleCard.DurationTicks - m_titleTicks) / (double)IntroFadeTicks)
                    : 1.0;
                m_title.Draw(pixels, m_titlePalette, brightness);
                return;
            }

            var fadeTicks = EndDelayTicks - m_endTicks;
            var fadeDuration = m_game.RescuingCaptives ? CastleRescue.FadeTicks : EndFadeTicks;
            var fade = fadeTicks < fadeDuration ? Math.Clamp(fadeTicks * 30 / fadeDuration, 0, 30) : Math.Min(30, m_game.TickCount);
            pixels.SetPalette(GetFadePalette(fade));
            m_renderer.Draw(pixels, m_game, tint: HasSwitch("color") ? null : Foreground, effectOpacity: (byte)(255 * fade / 30),
                showTerrainShade: HasSwitch("ao"));
        }
    }

    private void FinishMap()
    {
        if (m_game.AreaIndex + 1 == m_areas.Length)
        {
            OnCycleCompleted();
            StartCycle();
            return;
        }

        LoadArea(m_game.AreaIndex + 1);
        m_game = m_game.ContinueIn(m_data);
    }

    private void StartCycle()
    {
        var stage = StartingStage;
        LoadArea((stage - 1) / 2);
        m_game = new Game(m_data, Settings.Instance.GoldenAxeCharacter, startInCamp: stage % 2 == 0, playOpening: stage == 1);
        m_endTicks = 0;
        m_fadeInTitle = m_introTicks > 0;
        m_titleTicks = m_animatedTitle != null ? AnimatedTitle.DurationTicks : m_title == null ? 0 : ArcadeTitleCard.DurationTicks;
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
        m_introPalette = null;
        m_titlePalette = null;
        m_mapTint = null;
        base.OnSkinChanged(skin);
    }

    private Rgb[] OpeningPalette(Rgb[] palette) => HasSwitch("color") ? palette :
        PixelScreenData.CreateGreyscalePalette(palette, Background ?? Rgb.Black, Foreground ?? Rgb.White);

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
