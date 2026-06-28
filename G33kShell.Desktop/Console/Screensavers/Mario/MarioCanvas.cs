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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using DTC.Core;
using G33kShell.Desktop.Console.Screensavers.AI;
using G33kShell.Desktop.Skins;
using G33kShell.Desktop.Terminal;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SkiaSharp;
using MarioBrain = G33kShell.Desktop.Console.Screensavers.Mario.Brain;
using MarioGame = G33kShell.Desktop.Console.Screensavers.Mario.Game;

namespace G33kShell.Desktop.Console.Screensavers.Mario;

/// <summary>
/// A pixel-backed World 1-1 level scroll.
/// </summary>
[DebuggerDisplay("MarioCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class MarioCanvas : AiGameCanvasBase
{
    private const int ViewWidth = 256;
    private const int ViewHeight = 240;
    private const int MarioCollisionWidth = 12;
    private const int MarioCollisionHeight = 16;
    private const int RenderFps = 60;
    private const int SimulationFps = 30;
    private const double SimulationStepSeconds = 1.0 / SimulationFps;
    private const int MaxSimulationTicksPerFrame = 5;
    private const int DebugOverlayWidth = 112;
    private const int DebugOverlayHeight = 72;
    private const int DebugOverlayMargin = 4;
    private const int DebugMaxLinksPerLayer = 48;
    private const int MaxTrainingPopulation = 128;
    private const int MiniWorldTopMargin = 44;
    private const double RouteMasteryCompletionRate = 0.70;
    private const int RouteMasteryGenerationCount = 5;

    private WindowManager m_windowManager;
    private PixelScreenDataLock m_pixelScreen;
    private LevelData m_level;
    private byte[][] m_tiles;
    private byte[] m_usedQuestionBlockPixels;
    private Rgb[] m_levelPalette;
    private Rgb[] m_sourcePalette;
    private bool[][] m_solidMap;
    private string[] m_tileKinds;
    private SpriteFrame[] m_spriteFrames;
    private SpriteFrame[] m_enemySpriteFrames;
    private byte m_debugPanelColorIndex;
    private byte m_debugNodeColorIndex;
    private byte m_debugSightColorIndex;
    private byte[] m_debugLinkPaletteIndexes;
    private readonly DebugLink[] m_debugLinkBuffer = new DebugLink[DebugMaxLinksPerLayer];
    private int[] m_trainingMarioTileX;
    private int[] m_trainingMarioTileY;
    private readonly object m_trainingProgressLock = new();
    private double m_trainingTotalBestX;
    private double m_trainingMaxBestX;
    private int m_trainingCompletedGames;
    private int m_trainingFinishedGames;
    private readonly Queue<double> m_routeCompletionRates = new();
    private bool m_trainingWithEnemies;
    private byte[] m_miniWorldMap;
    private int m_miniWorldScale;
    private int m_miniWorldPixelWidth;
    private int m_miniWorldPixelHeight;
    private bool m_isActive;
    private double m_marioX;
    private double m_marioY;
    private double m_marioVelocityX;
    private double m_marioVelocityY;
    private bool m_marioStopped;
    private MarioGame m_aiGame;
    private int m_gameOverHoldFrames;
    private int m_lastSeenBlockHitTick;
    private double m_lastRenderTime;
    private double m_simulationAccumulator;
    private readonly List<BlockParticle> m_blockParticles = [];

    public MarioCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, RenderFps)
    {
        Name = "mario";
    }

    public override void OnLoaded(WindowManager windowManager)
    {
        m_windowManager = windowManager;
        LoadLevel();
        LoadSprites();
        BuildSolidTileMap();
        base.OnLoaded(windowManager);
    }

    protected override void OnUnloaded()
    {
        base.OnUnloaded();
        ClearPixelScreen();
        m_windowManager = null;
    }

    public override void OnSkinChanged(SkinBase skin)
    {
        base.OnSkinChanged(skin);
        if (!m_isActive || m_pixelScreen == null || m_levelPalette == null)
            return;

        using (m_pixelScreen.Lock(out var pixelScreen))
            pixelScreen.SetPalette(GetDisplayPalette());
    }

    public override void StartScreensaver(ScreenData shellScreen)
    {
        base.StartScreensaver(shellScreen);
        if (m_windowManager == null)
            return;

        using (Screen.Lock(out var screen))
            ClearTextOverlay(screen);
        InvalidateVisual();
        if (ShouldTrainAi())
            return;
        if (!IsReadyToRun)
            return;

        m_isActive = true;
        ResetMario();
        m_lastRenderTime = Time;
        m_simulationAccumulator = 0.0;
        m_pixelScreen = m_windowManager.SetPixelScreen(ViewWidth, ViewHeight, GetDisplayPalette());
    }

    public override void StopScreensaver()
    {
        base.StopScreensaver();
        ClearPixelScreen();
    }

    public override void BuildScreen(ScreenData screen) =>
        ClearTextOverlay(screen);

    protected override void PrepareAiFrame(ScreenData screen) =>
        ClearTextOverlay(screen);

    protected override void OnAiNotReady() =>
        ClearPixelScreen();

    protected override void SaveBrainBytes(byte[] brainBytes)
    {
        Settings.Instance.MarioBrain = brainBytes;
        Settings.Instance.Save();
    }

    protected override void UpdateGameFrame(ScreenData screen)
    {
        if (!m_isActive || m_pixelScreen == null || m_level == null || m_tiles == null)
            return;

        var simulationTicks = GetSimulationTicks();
        PlayAiGame(simulationTicks);
        var cameraX = GetCameraX();

        using (m_pixelScreen.Lock(out var pixels))
        {
            DrawLevelViewport(pixels, cameraX);
            UpdateBlockParticles(simulationTicks);
            DrawBlockParticles(pixels, cameraX);
            DrawEnemies(pixels, cameraX);
            DrawMario(pixels, cameraX);
            DrawVisibleWorldDebugBox(pixels, cameraX);
            DrawBrainDebugOverlay(pixels);
        }
    }

    private int GetSimulationTicks()
    {
        var now = Time;
        var elapsed = Math.Clamp(now - m_lastRenderTime, 0.0, SimulationStepSeconds * MaxSimulationTicksPerFrame);
        m_lastRenderTime = now;
        m_simulationAccumulator += elapsed;

        var ticks = Math.Min((int)(m_simulationAccumulator / SimulationStepSeconds), MaxSimulationTicksPerFrame);
        m_simulationAccumulator -= ticks * SimulationStepSeconds;
        return ticks;
    }

    private void ClearPixelScreen()
    {
        m_isActive = false;
        m_pixelScreen = null;
        m_aiGame = null;
        m_gameOverHoldFrames = 0;
        m_lastSeenBlockHitTick = 0;
        m_lastRenderTime = 0.0;
        m_simulationAccumulator = 0.0;
        m_blockParticles.Clear();
        m_windowManager?.ClearPixelScreen();
    }

    private void PlayAiGame(int simulationTicks)
    {
        if (m_aiGame == null)
        {
            var brain = (MarioBrain)CreateBrain().Load(GetSavedBrainBytes());
            m_aiGame = (MarioGame)CreateGame(brain).ResetGame();
            m_gameOverHoldFrames = 0;
            m_lastSeenBlockHitTick = 0;
            m_blockParticles.Clear();
        }

        for (var i = 0; i < simulationTicks; i++)
        {
            if (m_gameOverHoldFrames > 0)
            {
                m_gameOverHoldFrames--;
                if (m_gameOverHoldFrames == 0)
                {
                    m_aiGame.ResetGame();
                    m_lastSeenBlockHitTick = 0;
                    m_blockParticles.Clear();
                }
                continue;
            }

            m_aiGame.Tick();
            SpawnBlockParticlesFromGameEvent();
            if (m_aiGame.IsGameOver)
                m_gameOverHoldFrames = 45;
        }

        m_marioVelocityX = m_aiGame.MarioVelocityX;
        m_marioVelocityY = m_aiGame.MarioVelocityY;
        var interpolation = m_simulationAccumulator / SimulationStepSeconds;
        m_marioX = m_aiGame.MarioX + m_marioVelocityX * interpolation;
        m_marioY = m_aiGame.MarioY + m_marioVelocityY * interpolation;
    }

    private static void ClearTextOverlay(ScreenData screen)
    {
        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Width; x++)
            {
                var attr = screen.Chars[y][x];
                attr.Set(' ');
                attr.Foreground = null;
                attr.Background = null;
            }
        }
    }

    private void LoadLevel()
    {
        var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets", "PixelLevels");
        var jsonPath = Path.Combine(assetsDir, "world_1_1.json");
        var atlasPath = Path.Combine(assetsDir, "world_1_1.tiles.png");

        m_level = JsonConvert.DeserializeObject<LevelData>(File.ReadAllText(jsonPath));
        if (m_level == null)
            throw new InvalidOperationException($"Unable to load level data: {jsonPath}");

        m_levelPalette = m_level.ToPalette();
        m_sourcePalette = m_levelPalette;
        var paletteIndexes = new Dictionary<int, byte>();
        for (var i = 0; i < m_levelPalette.Length; i++)
            paletteIndexes[ToColorKey(m_levelPalette[i])] = (byte)i;

        using var atlas = SKBitmap.Decode(atlasPath);
        m_tiles = new byte[m_level.Tiles.Length][];
        foreach (var tile in m_level.Tiles)
        {
            var tilePixels = new byte[m_level.TileSize * m_level.TileSize];
            var sx = tile.Id % m_level.AtlasColumns * m_level.TileSize;
            var sy = tile.Id / m_level.AtlasColumns * m_level.TileSize;
            for (var y = 0; y < m_level.TileSize; y++)
            {
                for (var x = 0; x < m_level.TileSize; x++)
                {
                    var color = atlas.GetPixel(sx + x, sy + y);
                    var colorKey = color.Red << 16 | color.Green << 8 | color.Blue;
                    tilePixels[y * m_level.TileSize + x] = paletteIndexes[colorKey];
                }
            }

            m_tiles[tile.Id] = tilePixels;
        }

        LoadUsedQuestionBlock(Path.Combine(assetsDir, "used_question_block.png"));
    }

    private void LoadUsedQuestionBlock(string imagePath)
    {
        using var image = SKBitmap.Decode(imagePath);
        if (image.Width != 16 || image.Height != 16)
            throw new InvalidOperationException($"Used question block must be 16x16: {imagePath}");

        var paletteIndexes = new Dictionary<int, byte>();
        for (var i = 0; i < m_sourcePalette.Length; i++)
            paletteIndexes[ToColorKey(m_sourcePalette[i])] = (byte)i;

        m_usedQuestionBlockPixels = new byte[16 * 16];
        var skyIndex = m_tiles[m_level.Map[0][0]][0];
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var color = image.GetPixel(x, y);
                if (color.Alpha == 0)
                {
                    m_usedQuestionBlockPixels[y * 16 + x] = skyIndex;
                    continue;
                }

                var colorKey = color.Red << 16 | color.Green << 8 | color.Blue;
                if (!paletteIndexes.TryGetValue(colorKey, out var index))
                {
                    index = (byte)m_sourcePalette.Length;
                    paletteIndexes[colorKey] = index;
                    var palette = new Rgb[m_sourcePalette.Length + 1];
                    Array.Copy(m_sourcePalette, palette, m_sourcePalette.Length);
                    palette[^1] = new Rgb(color.Red, color.Green, color.Blue);
                    m_sourcePalette = palette;
                }

                m_usedQuestionBlockPixels[y * 16 + x] = index;
            }
        }
    }

    private void LoadSprites()
    {
        var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets", "PixelLevels");
        m_spriteFrames = LoadSpriteFrames(Path.Combine(assetsDir, "mario_sprites.json"), assetsDir);
        m_enemySpriteFrames = LoadSpriteFrames(Path.Combine(assetsDir, "enemy_sprites.json"), assetsDir);
        AddDebugPalette();
    }

    private void AddDebugPalette()
    {
        var debugColors = new[]
        {
            new Rgb(4, 8, 18),
            new Rgb(13, 23, 34),
            new Rgb(18, 38, 58),
            new Rgb(30, 70, 94),
            new Rgb(48, 112, 130),
            new Rgb(78, 158, 158),
            new Rgb(132, 212, 182),
            new Rgb(204, 244, 204),
            new Rgb(255, 255, 224),
            new Rgb(255, 255, 255)
        };
        if (m_sourcePalette.Length + debugColors.Length > 256)
            return;

        var startIndex = m_sourcePalette.Length;
        var palette = new Rgb[m_sourcePalette.Length + debugColors.Length];
        Array.Copy(m_sourcePalette, palette, m_sourcePalette.Length);
        Array.Copy(debugColors, 0, palette, startIndex, debugColors.Length);
        m_sourcePalette = palette;

        m_debugPanelColorIndex = (byte)startIndex;
        m_debugLinkPaletteIndexes = new byte[debugColors.Length - 2];
        for (var i = 0; i < m_debugLinkPaletteIndexes.Length; i++)
            m_debugLinkPaletteIndexes[i] = (byte)(startIndex + i + 1);
        m_debugNodeColorIndex = (byte)(startIndex + debugColors.Length - 1);
        m_debugSightColorIndex = (byte)(startIndex + debugColors.Length - 2);
    }

    private SpriteFrame[] LoadSpriteFrames(string jsonPath, string assetsDir)
    {
        var spriteData = JsonConvert.DeserializeObject<SpriteData>(File.ReadAllText(jsonPath));
        if (spriteData == null)
            throw new InvalidOperationException($"Unable to load sprite data: {jsonPath}");

        var spritePalette = spriteData.ToPalette();
        var paletteOffset = m_sourcePalette.Length;
        var paletteIndexes = new Dictionary<int, byte>();
        for (var i = 0; i < spritePalette.Length; i++)
            paletteIndexes[ToColorKey(spritePalette[i])] = (byte)i;

        var mergedPalette = new Rgb[m_sourcePalette.Length + spritePalette.Length];
        Array.Copy(m_sourcePalette, mergedPalette, m_sourcePalette.Length);
        Array.Copy(spritePalette, 0, mergedPalette, m_sourcePalette.Length, spritePalette.Length);
        m_sourcePalette = mergedPalette;

        var frames = new SpriteFrame[spriteData.Frames.Length];
        using var spriteSheet = SKBitmap.Decode(Path.Combine(assetsDir, spriteData.Sheet));
        for (var frameIndex = 0; frameIndex < spriteData.Frames.Length; frameIndex++)
        {
            var sourceFrame = spriteData.Frames[frameIndex];
            var pixels = new byte?[sourceFrame.Width * sourceFrame.Height];
            for (var y = 0; y < sourceFrame.Height; y++)
            {
                for (var x = 0; x < sourceFrame.Width; x++)
                {
                    var color = spriteSheet.GetPixel(sourceFrame.X + x, sourceFrame.Y + y);
                    if (color.Alpha == 0)
                        continue;

                    var colorKey = color.Red << 16 | color.Green << 8 | color.Blue;
                    pixels[y * sourceFrame.Width + x] = (byte)(paletteOffset + paletteIndexes[colorKey]);
                }
            }

            frames[frameIndex] = new SpriteFrame(sourceFrame.Name, sourceFrame.Width, sourceFrame.Height, pixels);
        }

        return frames;
    }

    private void BuildSolidTileMap()
    {
        m_solidMap = new bool[m_level.HeightTiles][];
        for (var y = 0; y < m_level.HeightTiles; y++)
            m_solidMap[y] = new bool[m_level.WidthTiles];

        m_tileKinds = new string[m_level.Tiles.Length];
        foreach (var tile in m_level.Tiles)
            m_tileKinds[tile.Id] = tile.Kind;

        for (var y = 0; y < m_level.HeightTiles; y++)
        {
            for (var x = 0; x < m_level.WidthTiles; x++)
            {
                var tileId = m_level.Map[y][x];
                m_solidMap[y][x] = m_tileKinds[tileId] == "orange";
            }
        }

        for (var x = 0; x < m_level.WidthTiles; x++)
        {
            int? pipeTopY = null;
            for (var y = 0; y < m_level.HeightTiles; y++)
            {
                var tileId = m_level.Map[y][x];
                if (tileId is >= 31 and <= 39)
                {
                    pipeTopY = y;
                    break;
                }
            }

            if (!pipeTopY.HasValue)
                continue;

            for (var y = pipeTopY.Value; y < m_level.HeightTiles; y++)
            {
                var tileId = m_level.Map[y][x];
                if (m_tileKinds[tileId] == "green")
                    m_solidMap[y][x] = true;
            }
        }

        BuildMiniWorldMap();
    }

    private void BuildMiniWorldMap()
    {
        // Scale so the full level width fits within the training screen.
        m_miniWorldScale = Math.Max(1, (m_level.WidthTiles + ViewWidth - 1) / ViewWidth);
        m_miniWorldPixelWidth = (m_level.WidthTiles + m_miniWorldScale - 1) / m_miniWorldScale;
        m_miniWorldPixelHeight = (m_level.HeightTiles + m_miniWorldScale - 1) / m_miniWorldScale;

        var skyTileId = m_level.Map[0][0]; // top-left tile is always sky
        m_miniWorldMap = new byte[m_miniWorldPixelWidth * m_miniWorldPixelHeight];
        for (var py = 0; py < m_miniWorldPixelHeight; py++)
        {
            for (var px = 0; px < m_miniWorldPixelWidth; px++)
            {
                byte mapValue = 0;
                for (var dy = 0; dy < m_miniWorldScale; dy++)
                {
                    var ty = py * m_miniWorldScale + dy;
                    if (ty >= m_level.HeightTiles)
                        break;
                    for (var dx = 0; dx < m_miniWorldScale; dx++)
                    {
                        var tx = px * m_miniWorldScale + dx;
                        if (tx >= m_level.WidthTiles || m_level.Map[ty][tx] == skyTileId)
                            continue;

                        mapValue = Math.Max(mapValue, m_solidMap[ty][tx] ? (byte)2 : (byte)1);
                    }
                }

                m_miniWorldMap[py * m_miniWorldPixelWidth + px] = mapValue;
            }
        }

        m_trainingMarioTileX = new int[MaxTrainingPopulation];
        m_trainingMarioTileY = new int[MaxTrainingPopulation];
        for (var i = 0; i < MaxTrainingPopulation; i++)
        {
            m_trainingMarioTileX[i] = -1;
            m_trainingMarioTileY[i] = -1;
        }
    }

    protected override void OnTrainingTick(int brainIndex, AiGameBase game)
    {
        if (m_miniWorldMap == null || m_level == null || brainIndex >= MaxTrainingPopulation)
            return;

        var marioGame = (MarioGame)game;
        m_trainingMarioTileX[brainIndex] = (int)(marioGame.MarioX / m_level.TileSize);
        m_trainingMarioTileY[brainIndex] = (int)(marioGame.MarioY / m_level.TileSize);
    }

    protected override void OnTrainingGenerationStarted(int generation, int populationCount)
    {
        lock (m_trainingProgressLock)
        {
            m_trainingTotalBestX = 0.0;
            m_trainingMaxBestX = 0.0;
            m_trainingCompletedGames = 0;
            m_trainingFinishedGames = 0;
            if (m_trainingMarioTileX != null)
            {
                Array.Fill(m_trainingMarioTileX, -1);
                Array.Fill(m_trainingMarioTileY, -1);
            }
        }
    }

    protected override void OnTrainingGameComplete(int brainIndex, AiGameBase game)
    {
        var marioGame = (MarioGame)game;
        var bestX = marioGame.BestX;
        lock (m_trainingProgressLock)
        {
            m_trainingTotalBestX += bestX;
            m_trainingMaxBestX = Math.Max(m_trainingMaxBestX, bestX);
            m_trainingCompletedGames++;
            if (marioGame.HasReachedFlagPole)
                m_trainingFinishedGames++;
        }
    }

    protected override void OnTrainingGenerationComplete(TrainingGenerationResult result)
    {
        base.OnTrainingGenerationComplete(result);
        lock (m_trainingProgressLock)
        {
            if (m_trainingCompletedGames > 0)
            {
                var completionRate = m_trainingFinishedGames / (double)m_trainingCompletedGames;
                System.Console.WriteLine(
                    $"  X: avg={m_trainingTotalBestX / m_trainingCompletedGames:F0} max={m_trainingMaxBestX:F0}" +
                    $" finished={completionRate:P1} (flag={MarioGame.FlagPoleX})");
                UpdateTrainingCurriculum(completionRate);
            }
        }
    }

    protected override void OnTrainingStarted()
    {
        m_trainingWithEnemies = false;
        m_routeCompletionRates.Clear();
    }

    private void UpdateTrainingCurriculum(double completionRate)
    {
        if (m_trainingWithEnemies)
            return;

        m_routeCompletionRates.Enqueue(completionRate);
        while (m_routeCompletionRates.Count > RouteMasteryGenerationCount)
            m_routeCompletionRates.Dequeue();
        if (m_routeCompletionRates.Count < RouteMasteryGenerationCount ||
            m_routeCompletionRates.Average() < RouteMasteryCompletionRate)
            return;

        m_trainingWithEnemies = true;
        m_routeCompletionRates.Clear();
        ResetExplorationProgressTracking();
        ResetCompletingArchiveAfterBreeding();
        System.Console.WriteLine("Mario curriculum advanced to enemy training after the population mastered the route.");
    }

    protected override void OnAfterDrawTrainingGraph(PixelScreenData screen)
    {
        if (m_miniWorldMap == null || m_level == null || m_trainingMarioTileX == null)
            return;

        var mapWidth = m_miniWorldPixelWidth;
        var mapHeight = m_miniWorldPixelHeight;
        var startX = Math.Max(0, (screen.Width - mapWidth) / 2);
        var startY = MiniWorldTopMargin;

        // Draw terrain background.
        for (var y = 0; y < mapHeight; y++)
        {
            var sy = startY + y;
            if (sy < 0 || sy >= screen.Height)
                continue;
            for (var x = 0; x < mapWidth; x++)
            {
                var sx = startX + x;
                if (sx >= 0 && sx < screen.Width)
                    screen.Pixels[sy * screen.Width + sx] = m_miniWorldMap[y * mapWidth + x];
            }
        }

        // Draw each Mario as a bright dot.
        for (var i = 0; i < MaxTrainingPopulation; i++)
        {
            var tileX = m_trainingMarioTileX[i];
            var tileY = m_trainingMarioTileY[i];
            if (tileX < 0 || tileX >= m_level.WidthTiles || tileY < 0 || tileY >= m_level.HeightTiles)
                continue;

            var sx = startX + tileX / m_miniWorldScale;
            var sy = startY + tileY / m_miniWorldScale;
            if (sx >= 0 && sx < screen.Width && sy >= 0 && sy < screen.Height)
                screen.Pixels[sy * screen.Width + sx] = 3;
        }
    }

    private void ResetMario()
    {
        m_marioX = 24;
        m_marioVelocityX = 0;
        m_marioVelocityY = 0;
        m_marioStopped = false;
        m_gameOverHoldFrames = 0;
        m_lastSeenBlockHitTick = 0;
        m_blockParticles.Clear();
        m_marioY = FindGroundY((int)m_marioX + MarioCollisionWidth / 2) - MarioCollisionHeight;
    }
    
    private int FindGroundY(int x)
    {
        for (var y = 0; y < m_level.HeightPixels; y++)
        {
            if (IsSolidPixel(x, y))
                return y;
        }

        return m_level.HeightPixels;
    }
    
    private bool IsSolidPixel(int x, int y)
    {
        if (x < 0 || x >= m_level.WidthPixels || y < 0 || y >= m_level.HeightPixels)
            return false;

        var tileX = x / m_level.TileSize;
        var tileY = y / m_level.TileSize;
        return m_solidMap[tileY][tileX];
    }

    private Rgb[] GetDisplayPalette() =>
        HasSwitch("color")
            ? m_sourcePalette
            : PixelScreenData.CreateGreyscalePalette(m_sourcePalette, Background, Foreground);

    private int GetCameraX()
    {
        var maxCameraX = Math.Max(0, m_level.WidthPixels - ViewWidth);
        return Clamp((int)m_marioX - 80, 0, maxCameraX);
    }

    private void DrawLevelViewport(PixelScreenData screen, int cameraX)
    {
        var tileSize = m_level.TileSize;
        var skyTile = m_tiles[m_level.Map[0][0]];
        var firstTileX = Math.Max(0, cameraX / tileSize);
        var lastTileX = Math.Min(m_level.WidthTiles - 1, (cameraX + screen.Width - 1) / tileSize);
        var lastTileY = Math.Min(m_level.HeightTiles - 1, (screen.Height - 1) / tileSize);

        for (var tileY = 0; tileY <= lastTileY; tileY++)
        {
            var screenY = tileY * tileSize;
            for (var tileX = firstTileX; tileX <= lastTileX; tileX++)
            {
                var tile = m_tiles[m_level.Map[tileY][tileX]];
                var sourceStride = tileSize;
                var sourceX = 0;
                var sourceY = 0;
                if (m_aiGame?.IsBlockBroken(tileX, tileY) == true)
                {
                    tile = skyTile;
                }
                else if (m_aiGame?.IsQuestionBlockUsed(tileX, tileY) == true)
                {
                    tile = m_usedQuestionBlockPixels;
                    sourceStride = 16;
                    sourceX = tileX % 2 * tileSize;
                    sourceY = tileY % 2 * tileSize;
                }

                BlitTile(screen, tile, sourceStride, sourceX, sourceY, tileX * tileSize - cameraX, screenY, tileSize);
            }
        }
    }

    private static void BlitTile(
        PixelScreenData screen,
        byte[] source,
        int sourceStride,
        int sourceX,
        int sourceY,
        int destinationX,
        int destinationY,
        int tileSize)
    {
        var clippedX = Math.Max(0, destinationX);
        var clippedRight = Math.Min(screen.Width, destinationX + tileSize);
        var copyWidth = clippedRight - clippedX;
        if (copyWidth <= 0)
            return;

        var sourceClipX = clippedX - destinationX;
        var clippedBottom = Math.Min(screen.Height, destinationY + tileSize);
        for (var y = Math.Max(0, destinationY); y < clippedBottom; y++)
        {
            var sourceRow = sourceY + y - destinationY;
            Array.Copy(
                source,
                sourceRow * sourceStride + sourceX + sourceClipX,
                screen.Pixels,
                y * screen.Width + clippedX,
                copyWidth);
        }
    }

    private void DrawMario(PixelScreenData screen, int cameraX)
    {
        var frame = GetMarioFrame();
        var drawX = (int)Math.Round(m_marioX) - cameraX;
        var drawY = (int)Math.Round(m_marioY) - (frame.Height - MarioCollisionHeight);

        for (var y = 0; y < frame.Height; y++)
        {
            var sy = drawY + y;
            if (sy < 0 || sy >= screen.Height)
                continue;

            for (var x = 0; x < frame.Width; x++)
            {
                var sx = drawX + x;
                if (sx < 0 || sx >= screen.Width)
                    continue;

                var color = frame.Pixels[y * frame.Width + x];
                if (color.HasValue)
                    screen.Pixels[sy * screen.Width + sx] = color.Value;
            }
        }
    }

    private void DrawEnemies(PixelScreenData screen, int cameraX)
    {
        if (m_aiGame == null || m_enemySpriteFrames == null)
            return;

        foreach (var enemy in m_aiGame.Enemies)
        {
            var frame = GetEnemyFrame(enemy.IsDead);
            var drawX = (int)Math.Round(enemy.X) - cameraX;
            var drawY = (int)Math.Round(enemy.Y);
            for (var y = 0; y < frame.Height; y++)
            {
                var sy = drawY + y;
                if (sy < 0 || sy >= screen.Height)
                    continue;

                for (var x = 0; x < frame.Width; x++)
                {
                    var sx = drawX + x;
                    if (sx < 0 || sx >= screen.Width)
                        continue;

                    var color = frame.Pixels[y * frame.Width + x];
                    if (color.HasValue)
                        screen.Pixels[sy * screen.Width + sx] = color.Value;
                }
            }
        }
    }

    private void SpawnBlockParticlesFromGameEvent()
    {
        if (m_aiGame.LastBlockHitTick == 0 || m_aiGame.LastBlockHitTick == m_lastSeenBlockHitTick)
            return;

        m_lastSeenBlockHitTick = m_aiGame.LastBlockHitTick;
        SpawnBlockParticles(
            m_aiGame.LastBlockHitTileX,
            m_aiGame.LastBlockHitTileY,
            m_aiGame.LastBlockHitTileId,
            m_aiGame.LastBlockHitSizeTiles);
    }

    private void SpawnBlockParticles(int tileX, int tileY, int tileId, int sizeTiles)
    {
        if (tileId < 0 || tileId >= m_tiles.Length)
            return;

        sizeTiles = Math.Max(1, sizeTiles);
        var blockSize = m_level.TileSize * sizeTiles;
        var pieceSize = blockSize / 2;
        var x = tileX * m_level.TileSize;
        var y = tileY * m_level.TileSize;
        m_blockParticles.Add(new BlockParticle(x, y, -1.0, -2.5, GetBlockPiecePixels(tileX, tileY, 0, 0, pieceSize), pieceSize));
        m_blockParticles.Add(new BlockParticle(x + pieceSize, y, 1.0, -2.5, GetBlockPiecePixels(tileX, tileY, pieceSize, 0, pieceSize), pieceSize));
        m_blockParticles.Add(new BlockParticle(x, y + pieceSize, -1.4, -1.35, GetBlockPiecePixels(tileX, tileY, 0, pieceSize, pieceSize), pieceSize));
        m_blockParticles.Add(new BlockParticle(x + pieceSize, y + pieceSize, 1.4, -1.35, GetBlockPiecePixels(tileX, tileY, pieceSize, pieceSize, pieceSize), pieceSize));
    }

    private byte[] GetBlockPiecePixels(int tileX, int tileY, int blockLocalX, int blockLocalY, int size)
    {
        var pixels = new byte[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
                pixels[y * size + x] = GetBlockPixel(tileX, tileY, blockLocalX + x, blockLocalY + y);
        }

        return pixels;
    }

    private byte GetBlockPixel(int tileX, int tileY, int blockLocalX, int blockLocalY)
    {
        var sourceTileX = tileX + blockLocalX / m_level.TileSize;
        var sourceTileY = tileY + blockLocalY / m_level.TileSize;
        if (sourceTileX < 0 || sourceTileX >= m_level.WidthTiles || sourceTileY < 0 || sourceTileY >= m_level.HeightTiles)
            return m_tiles[m_level.Map[0][0]][0];

        var sourceTile = m_tiles[m_level.Map[sourceTileY][sourceTileX]];
        var localX = blockLocalX % m_level.TileSize;
        var localY = blockLocalY % m_level.TileSize;
        return sourceTile[localY * m_level.TileSize + localX];
    }

    private void UpdateBlockParticles(double simulationStep)
    {
        for (var i = m_blockParticles.Count - 1; i >= 0; i--)
        {
            var particle = m_blockParticles[i];
            particle.X += particle.VelocityX * simulationStep;
            particle.Y += particle.VelocityY * simulationStep;
            particle.VelocityY += 0.22 * simulationStep;
            particle.Life -= simulationStep;
            if (particle.Life <= 0 || particle.Y > ViewHeight + 16)
                m_blockParticles.RemoveAt(i);
        }
    }

    private void DrawBlockParticles(PixelScreenData screen, int cameraX)
    {
        foreach (var particle in m_blockParticles)
        {
            var drawX = (int)Math.Round(particle.X) - cameraX;
            var drawY = (int)Math.Round(particle.Y);
            for (var y = 0; y < particle.Size; y++)
            {
                var sy = drawY + y;
                if (sy < 0 || sy >= screen.Height)
                    continue;

                for (var x = 0; x < particle.Size; x++)
                {
                    var sx = drawX + x;
                    if (sx < 0 || sx >= screen.Width)
                        continue;

                    screen.Pixels[sy * screen.Width + sx] = particle.Pixels[y * particle.Size + x];
                }
            }
        }
    }

    private void DrawBrainDebugOverlay(PixelScreenData screen)
    {
        if (!HasSwitch("debug") || m_aiGame?.Brain == null || m_debugLinkPaletteIndexes == null)
            return;

        var brain = m_aiGame.Brain;
        var layerCount = brain.DebugLayerCount;
        if (layerCount < 2)
            return;

        var overlayX = screen.Width - DebugOverlayWidth - DebugOverlayMargin;
        var overlayY = DebugOverlayMargin;
        FillRect(screen, overlayX, overlayY, DebugOverlayWidth, DebugOverlayHeight, m_debugPanelColorIndex);

        for (var sourceLayer = 0; sourceLayer < layerCount - 1; sourceLayer++)
        {
            var linkCount = CollectStrongestDebugLinks(brain, sourceLayer);
            var maxStrength = GetStrongestDebugLinkStrength(linkCount);
            for (var i = 0; i < linkCount; i++)
            {
                var link = m_debugLinkBuffer[i];
                var x0 = GetDebugLayerX(overlayX, sourceLayer, layerCount);
                var y0 = GetDebugNeuronY(overlayY, link.SourceNeuron, brain.GetDebugLayerSize(sourceLayer));
                var x1 = GetDebugLayerX(overlayX, sourceLayer + 1, layerCount);
                var y1 = GetDebugNeuronY(overlayY, link.TargetNeuron, brain.GetDebugLayerSize(sourceLayer + 1));
                screen.DrawLine(x0, y0, x1, y1, GetDebugLinkColor(link.Strength / maxStrength));
            }
        }

        DrawDebugNodes(screen, brain, overlayX, overlayY, layerCount);
    }

    private void DrawVisibleWorldDebugBox(PixelScreenData screen, int cameraX)
    {
        if (!HasSwitch("debug") || m_level == null || m_debugSightColorIndex == 0)
            return;

        var blockSize = m_level.TileSize * GameState.SensorBlockTileSize;
        var marioBlockX = (int)Math.Floor(m_marioX / blockSize);
        var left = (marioBlockX + GameState.SensorOriginBlockDx) * blockSize - cameraX;
        var bottomBlockY = (Game.ViewHeight - 1) / blockSize;
        var top = (bottomBlockY - GameState.SensorGridSizeY + 1) * blockSize;
        DrawRect(screen, left, top, GameState.SensorGridSizeX * blockSize, GameState.SensorGridSizeY * blockSize, m_debugSightColorIndex);
    }

    private int CollectStrongestDebugLinks(AiBrainBase brain, int sourceLayer)
    {
        var sourceSize = brain.GetDebugLayerSize(sourceLayer);
        var targetSize = brain.GetDebugLayerSize(sourceLayer + 1);
        var count = 0;
        var weakestIndex = 0;

        for (var targetNeuron = 0; targetNeuron < targetSize; targetNeuron++)
        {
            for (var sourceNeuron = 0; sourceNeuron < sourceSize; sourceNeuron++)
            {
                var strength = Math.Abs(brain.GetDebugNeuronValue(sourceLayer, sourceNeuron) * brain.GetDebugWeight(sourceLayer, sourceNeuron, targetNeuron));
                if (strength <= 0.001)
                    continue;

                if (count < m_debugLinkBuffer.Length)
                {
                    m_debugLinkBuffer[count++] = new DebugLink(sourceNeuron, targetNeuron, strength);
                    weakestIndex = FindWeakestDebugLink(count);
                    continue;
                }

                if (strength <= m_debugLinkBuffer[weakestIndex].Strength)
                    continue;

                m_debugLinkBuffer[weakestIndex] = new DebugLink(sourceNeuron, targetNeuron, strength);
                weakestIndex = FindWeakestDebugLink(count);
            }
        }

        return count;
    }

    private double GetStrongestDebugLinkStrength(int count)
    {
        var maxStrength = 0.0;
        for (var i = 0; i < count; i++)
            maxStrength = Math.Max(maxStrength, m_debugLinkBuffer[i].Strength);
        return Math.Max(0.001, maxStrength);
    }

    private int FindWeakestDebugLink(int count)
    {
        var weakestIndex = 0;
        var weakestStrength = m_debugLinkBuffer[0].Strength;
        for (var i = 1; i < count; i++)
        {
            if (m_debugLinkBuffer[i].Strength >= weakestStrength)
                continue;

            weakestStrength = m_debugLinkBuffer[i].Strength;
            weakestIndex = i;
        }

        return weakestIndex;
    }

    private void DrawDebugNodes(PixelScreenData screen, AiBrainBase brain, int overlayX, int overlayY, int layerCount)
    {
        for (var layer = 0; layer < layerCount; layer++)
        {
            var layerSize = brain.GetDebugLayerSize(layer);
            var maxLayerValue = GetDebugLayerMaxValue(brain, layer, layerSize);
            var visibleNodeCount = Math.Min(layerSize, 16);
            for (var i = 0; i < visibleNodeCount; i++)
            {
                var neuron = visibleNodeCount == 1 ? 0 : (int)Math.Round(i * (layerSize - 1) / (double)(visibleNodeCount - 1));
                var x = GetDebugLayerX(overlayX, layer, layerCount);
                var y = GetDebugNeuronY(overlayY, neuron, layerSize);
                var color = GetDebugLinkColor(Math.Abs(brain.GetDebugNeuronValue(layer, neuron)) / maxLayerValue);
                screen.SetPixel(x, y, m_debugNodeColorIndex);
                screen.SetPixel(x - 1, y, color);
                screen.SetPixel(x + 1, y, color);
                screen.SetPixel(x, y - 1, color);
                screen.SetPixel(x, y + 1, color);
                if (layer == layerCount - 1)
                {
                    screen.SetPixel(x + 2, y, color);
                    screen.SetPixel(x + 3, y, color);
                }
            }
        }
    }

    private static double GetDebugLayerMaxValue(AiBrainBase brain, int layer, int layerSize)
    {
        var maxValue = 0.0;
        for (var neuron = 0; neuron < layerSize; neuron++)
            maxValue = Math.Max(maxValue, Math.Abs(brain.GetDebugNeuronValue(layer, neuron)));
        return Math.Max(0.001, maxValue);
    }

    private byte GetDebugLinkColor(double normalized)
    {
        normalized = Math.Sqrt(Math.Clamp(normalized, 0.0, 1.0));
        var index = Clamp((int)Math.Round(normalized * (m_debugLinkPaletteIndexes.Length - 1)), 0, m_debugLinkPaletteIndexes.Length - 1);
        return m_debugLinkPaletteIndexes[index];
    }

    private static int GetDebugLayerX(int overlayX, int layer, int layerCount) =>
        overlayX + 5 + layer * (DebugOverlayWidth - 10) / Math.Max(1, layerCount - 1);

    private static int GetDebugNeuronY(int overlayY, int neuron, int layerSize) =>
        overlayY + 5 + (layerSize <= 1 ? (DebugOverlayHeight - 10) / 2 : neuron * (DebugOverlayHeight - 10) / (layerSize - 1));

    private static void FillRect(PixelScreenData screen, int x, int y, int width, int height, byte colorIndex)
    {
        for (var yy = y; yy < y + height; yy++)
        {
            if (yy < 0 || yy >= screen.Height)
                continue;

            for (var xx = x; xx < x + width; xx++)
            {
                if (xx >= 0 && xx < screen.Width)
                    screen.Pixels[yy * screen.Width + xx] = colorIndex;
            }
        }
    }

    private static void DrawRect(PixelScreenData screen, int x, int y, int width, int height, byte colorIndex)
    {
        if (width <= 0 || height <= 0)
            return;

        var right = x + width - 1;
        var bottom = y + height - 1;
        screen.DrawLine(x, y, right, y, colorIndex);
        screen.DrawLine(x, bottom, right, bottom, colorIndex);
        screen.DrawLine(x, y, x, bottom, colorIndex);
        screen.DrawLine(right, y, right, bottom, colorIndex);
    }

    private SpriteFrame GetMarioFrame()
    {
        if (m_aiGame?.IsMarioDead == true)
            return GetSpriteFrame("die");
        if (m_marioVelocityY > 10)
            return GetSpriteFrame("jump");
        if (m_aiGame?.HasReachedFlagPole == true && Math.Abs(m_marioVelocityX) < 0.1)
            return GetSpriteFrame("stand");
        if (m_marioStopped)
            return GetSpriteFrame("stand");

        var walkFrame = 1 + (int)(Time * 8.0) % 3;
        return m_spriteFrames[walkFrame];
    }

    private SpriteFrame GetSpriteFrame(string name)
    {
        foreach (var frame in m_spriteFrames)
        {
            if (frame.Name == name)
                return frame;
        }

        return m_spriteFrames[0];
    }

    private SpriteFrame GetEnemyFrame(bool isDead)
    {
        if (isDead)
            return m_enemySpriteFrames[0];

        var frameIndex = (int)(Time * 7.0) % Math.Min(2, m_enemySpriteFrames.Length);
        return m_enemySpriteFrames[frameIndex];
    }

    private static int ParseHexColorKey(string hex)
    {
        if (hex.StartsWith("#", StringComparison.Ordinal))
            hex = hex[1..];
        return int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static int ToColorKey(Rgb color) =>
        color.R << 16 | color.G << 8 | color.B;

    private class LevelData
    {
        public int TileSize { get; [UsedImplicitly] set; }
        public int WidthPixels { get; [UsedImplicitly] set; }
        public int HeightPixels { get; [UsedImplicitly] set; }
        public int WidthTiles { get; [UsedImplicitly] set; }
        public int HeightTiles { get; [UsedImplicitly] set; }
        public int AtlasColumns { get; [UsedImplicitly] set; }
        public string[] Palette { get; [UsedImplicitly] set; }
        public TileData[] Tiles { get; [UsedImplicitly] set; }
        public int[][] Map { get; [UsedImplicitly] set; }

        public Rgb[] ToPalette()
        {
            var palette = new Rgb[Palette.Length];
            for (var i = 0; i < Palette.Length; i++)
            {
                var colorKey = ParseHexColorKey(Palette[i]);
                palette[i] = new Rgb((byte)(colorKey >> 16), (byte)(colorKey >> 8), (byte)colorKey);
            }

            return palette;
        }
    }

    private class TileData
    {
        public int Id { get; [UsedImplicitly] set; }
        public string Kind { get; [UsedImplicitly] set; }
    }

    private class SpriteData
    {
        public string Sheet { get; [UsedImplicitly] set; }
        public string[] Palette { get; [UsedImplicitly] set; }
        public SpriteFrameData[] Frames { get; [UsedImplicitly] set; }

        public Rgb[] ToPalette()
        {
            var palette = new Rgb[Palette.Length];
            for (var i = 0; i < Palette.Length; i++)
            {
                var colorKey = ParseHexColorKey(Palette[i]);
                palette[i] = new Rgb((byte)(colorKey >> 16), (byte)(colorKey >> 8), (byte)colorKey);
            }

            return palette;
        }
    }

    private class SpriteFrameData
    {
        public string Name { get; [UsedImplicitly] set; }
        public int X { get; [UsedImplicitly] set; }
        public int Y { get; [UsedImplicitly] set; }
        public int Width { get; [UsedImplicitly] set; }
        public int Height { get; [UsedImplicitly] set; }
    }

    private record SpriteFrame(string Name, int Width, int Height, byte?[] Pixels);

    private readonly record struct DebugLink(int SourceNeuron, int TargetNeuron, double Strength);

    private sealed class BlockParticle(double x, double y, double velocityX, double velocityY, byte[] pixels, int size)
    {
        public double X = x;
        public double Y = y;
        public double VelocityX = velocityX;
        public double VelocityY = velocityY;
        public byte[] Pixels = pixels;
        public int Size = size;
        public double Life = 38;
    }

    private static int Clamp(int value, int min, int max) =>
        Math.Min(max, Math.Max(min, value));

    protected override AiGameBase CreateGame(AiBrainBase brain) =>
        new MarioGame(ArenaWidth, ArenaHeight, (MarioBrain)brain, useTrainingTimeouts: false, enableEnemies: true);

    protected override AiGameBase CreateTrainingGame(AiBrainBase brain) =>
        new MarioGame(ArenaWidth, ArenaHeight, (MarioBrain)brain, useTrainingTimeouts: true, enableEnemies: m_trainingWithEnemies);

    protected override AiGameBase CreateTrainingGame(AiBrainBase brain, int generation, bool isValidation, int candidateIndex, int gameIndex) =>
        new MarioGame(ArenaWidth, ArenaHeight, (MarioBrain)brain, useTrainingTimeouts: true, enableEnemies: m_trainingWithEnemies);

    protected override int GetGamesPerBrain() => m_trainingWithEnemies ? 6 : 1;
    protected override int GetValidationGamesPerBrain() => m_trainingWithEnemies ? 64 : 1;
    protected override int GetValidationCandidateCount() => 1;
    protected override int GetInitialPopulationSize() => 80;
    protected override int GetMinPopulationSize() => 60;
    protected override int GetEliteCount() => 8;
    protected override double GetMutationRate() => 0.006;
    protected override double GetMinimumMutationRate() => 0.001;
    protected override double GetMutationRateMultiplier(int offspringIndex) =>
        offspringIndex % 4 switch
        {
            0 => 0.25,
            1 => 0.5,
            2 => 1.0,
            _ => 2.0
        };
    protected override AiBrainBase MutateChild(
        AiBrainBase child,
        double mutationRate,
        int offspringIndex,
        bool conservativeMutation,
        Random random)
    {
        if (conservativeMutation)
        {
            var conservativeCounts = new[] { 1, 2, 4 };
            return child.Mutate(conservativeCounts[offspringIndex % conservativeCounts.Length], 0.02, random);
        }

        return child.Mutate(mutationRate, random);
    }
    protected override double GetCrossoverRate() => 0.0;
    protected override double GetRandomFraction() => 0.05;
    protected override double GetParentPoolFraction() => 0.10;
    protected override ExplorationProgress? GetExplorationProgress(IReadOnlyDictionary<string, double> averageStats)
    {
        if (!averageStats.TryGetValue("Finished", out var finished) ||
            !averageStats.TryGetValue("X", out var averageX))
            return null;

        return new ExplorationProgress(finished, averageX);
    }
    protected override bool IsMeaningfulExplorationProgress(ExplorationProgress candidate, ExplorationProgress best)
    {
        const double finishRateTolerance = 0.0001;
        if (candidate.Primary > best.Primary + finishRateTolerance)
            return true;
        if (candidate.Primary < best.Primary - finishRateTolerance)
            return false;

        return candidate.Secondary >= best.Secondary + 32.0;
    }
    protected override int GetExplorationStagnationThreshold() => 15;
    protected override string GetExplorationStagnationReason() => "route";
    protected override int GetCompletingArchiveSize() => 8;
    protected override double GetCompletingArchiveDescendantFraction() => 0.60;
    protected override bool IsCompletingArchiveCandidate(ExplorationProgress progress) => progress.Primary > 0.0;
    protected override string GetTrainingStatusText(int generation) =>
        m_trainingWithEnemies
            ? "Enemy training"
            : $"Route training: mastery {m_routeCompletionRates.Count}/{RouteMasteryGenerationCount}";
    protected override double GetValidationSkipThresholdRatio() => 1.0;
    protected override int GetValidationSkipWarmupGenerations() => 5;
    protected override int GetForcedValidationInterval() => 8;
    protected override bool UseTrainingScoreForGoat() => false;
    protected override bool UseDegeneracyPenalty() => false;
    protected override int GetTrainingSeed(int generation, int brainIndex, int gameIndex) =>
        MixTrainingSeed(0x2A17B4C3, (generation - 1) / 5, gameIndex);
    protected override int GetValidationSeed(int generation, int candidateIndex, int gameIndex) =>
        MixTrainingSeed(0x6D2B79F5, generation, gameIndex);

    private static int MixTrainingSeed(int salt, int generation, int gameIndex)
    {
        unchecked
        {
            var value = (uint)salt;
            value ^= (uint)generation * 0x9E3779B9u;
            value ^= (uint)(gameIndex + 1) * 0x85EBCA6Bu;
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (int)(value & 0x7FFFFFFF);
        }
    }

    protected override byte[] GetSavedBrainBytes() => Settings.Instance.MarioBrain;

    protected override AiBrainBase CreateBrain() => new MarioBrain();
}
