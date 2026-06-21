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
using DTC.Core;
using G33kShell.Desktop.Console.Screensavers.AI;
using G33kShell.Desktop.Skins;
using G33kShell.Desktop.Terminal;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SkiaSharp;
using MarioBrain = G33kShell.Desktop.Console.Screensavers.Mario.Brain;
using MarioGame = G33kShell.Desktop.Console.Screensavers.Mario.Game;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A pixel-backed World 1-1 level scroll.
/// </summary>
[DebuggerDisplay("MarioCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class MarioCanvas : AiGameCanvasBase
{
    private enum TrainingPhase
    {
        Route,
        BlockHunt,
        EnemyPractice,
        ShowRun
    }

    private const int ViewWidth = 256;
    private const int ViewHeight = 240;
    private const int MarioCollisionWidth = 12;
    private const int MarioCollisionHeight = 16;

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
    private bool m_isActive;
    private double m_marioX;
    private double m_marioY;
    private double m_marioVelocityX;
    private double m_marioVelocityY;
    private bool m_marioStopped;
    private MarioGame m_aiGame;
    private int m_gameOverHoldFrames;
    private int m_lastSeenBlockHitTick;
    private readonly List<BlockParticle> m_blockParticles = [];

    public MarioCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
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

        m_isActive = true;
        ResetMario();
        m_pixelScreen = m_windowManager.SetPixelScreen(ViewWidth, ViewHeight, GetDisplayPalette());
    }

    public override void StopScreensaver()
    {
        base.StopScreensaver();
        ClearPixelScreen();
    }

    public override void BuildScreen(ScreenData screen) =>
        ClearTextOverlay(screen);

    public override void UpdateFrame(ScreenData screen)
    {
        ClearTextOverlay(screen);
        if (ShouldTrainAi())
        {
            ClearPixelScreen();
            TrainAi(screen, brainBytes =>
            {
                Settings.Instance.MarioBrain = brainBytes;
                Settings.Instance.Save();
            });
            return;
        }

        if (!m_isActive || m_pixelScreen == null || m_level == null || m_tiles == null)
            return;

        PlayAiGame();
        var cameraX = GetCameraX();

        using (m_pixelScreen.Lock(out var pixels))
        {
            DrawLevelViewport(pixels, cameraX);
            UpdateBlockParticles();
            DrawBlockParticles(pixels, cameraX);
            DrawEnemies(pixels, cameraX);
            DrawMario(pixels, cameraX);
        }
    }

    private void ClearPixelScreen()
    {
        m_isActive = false;
        m_pixelScreen = null;
        m_aiGame = null;
        m_gameOverHoldFrames = 0;
        m_lastSeenBlockHitTick = 0;
        m_blockParticles.Clear();
        m_windowManager?.ClearPixelScreen();
    }

    private void PlayAiGame()
    {
        if (m_aiGame == null)
        {
            var brain = (MarioBrain)CreateBrain().Load(Settings.Instance.MarioBrain);
            m_aiGame = (MarioGame)CreateGame(brain).ResetGame();
            m_gameOverHoldFrames = 0;
            m_lastSeenBlockHitTick = 0;
            m_blockParticles.Clear();
        }

        if (m_gameOverHoldFrames > 0)
        {
            m_gameOverHoldFrames--;
            if (m_gameOverHoldFrames == 0)
            {
                m_aiGame.ResetGame();
                m_lastSeenBlockHitTick = 0;
                m_blockParticles.Clear();
            }
        }
        else
        {
            m_aiGame.Tick();
            SpawnBlockParticlesFromGameEvent();
            if (m_aiGame.IsGameOver)
                m_gameOverHoldFrames = 45;
        }

        m_marioX = m_aiGame.MarioX;
        m_marioY = m_aiGame.MarioY;
        m_marioVelocityX = m_aiGame.MarioVelocityX;
        m_marioVelocityY = m_aiGame.MarioVelocityY;
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
        var skyIndex = m_tiles[m_level.Map[0][0]][0];
        var skyTile = m_tiles[skyIndex];
        var tileSize = m_level.TileSize;

        for (var y = 0; y < screen.Height; y++)
        {
            var tileY = y / tileSize;
            var tileLocalY = y % tileSize;
            for (var x = 0; x < screen.Width; x++)
            {
                var worldX = cameraX + x;
                var tileX = worldX / tileSize;
                var tileLocalX = worldX % tileSize;
                var tile = skyTile;
                if (tileY >= 0 && tileY < m_level.HeightTiles && tileX >= 0 && tileX < m_level.WidthTiles)
                {
                    tile = m_tiles[m_level.Map[tileY][tileX]];
                    if (m_aiGame?.IsBlockBroken(tileX, tileY) == true)
                        tile = skyTile;
                    else if (m_aiGame?.IsQuestionBlockUsed(tileX, tileY) == true)
                    {
                        var blockTileX = tileX - tileX % 2;
                        var blockTileY = tileY - tileY % 2;
                        var blockLocalX = (tileX - blockTileX) * tileSize + tileLocalX;
                        var blockLocalY = (tileY - blockTileY) * tileSize + tileLocalY;
                        screen.Pixels[y * screen.Width + x] = m_usedQuestionBlockPixels[blockLocalY * 16 + blockLocalX];
                        continue;
                    }
                }

                screen.Pixels[y * screen.Width + x] = tile[tileLocalY * tileSize + tileLocalX];
            }
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

    private void UpdateBlockParticles()
    {
        for (var i = m_blockParticles.Count - 1; i >= 0; i--)
        {
            var particle = m_blockParticles[i];
            particle.X += particle.VelocityX;
            particle.Y += particle.VelocityY;
            particle.VelocityY += 0.22;
            particle.Life--;
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

    private sealed class BlockParticle(double x, double y, double velocityX, double velocityY, byte[] pixels, int size)
    {
        public double X = x;
        public double Y = y;
        public double VelocityX = velocityX;
        public double VelocityY = velocityY;
        public byte[] Pixels = pixels;
        public int Size = size;
        public int Life = 38;
    }

    private static int Clamp(int value, int min, int max) =>
        Math.Min(max, Math.Max(min, value));

    protected override AiGameBase CreateGame(AiBrainBase brain) =>
        new MarioGame(ArenaWidth, ArenaHeight, (MarioBrain)brain, useTrainingTimeouts: false, enableEnemies: true);

    protected override AiGameBase CreateTrainingGame(AiBrainBase brain) =>
        new MarioGame(ArenaWidth, ArenaHeight, (MarioBrain)brain, useTrainingTimeouts: true, enableEnemies: true);

    protected override AiGameBase CreateTrainingGame(AiBrainBase brain, int generation, bool isValidation, int candidateIndex, int gameIndex)
    {
        var phase = isValidation ? TrainingPhase.ShowRun : GetTrainingPhase(generation);
        return new MarioGame(
            ArenaWidth,
            ArenaHeight,
            (MarioBrain)brain,
            useTrainingTimeouts: true,
            enableEnemies: phase >= TrainingPhase.EnemyPractice,
            showRewardMultiplier: GetShowRewardMultiplier(phase));
    }

    protected override int GetGamesPerBrain() => 6;
    protected override int GetValidationGamesPerBrain() => 8;
    protected override int GetInitialPopulationSize() => 120;
    protected override int GetMinPopulationSize() => 60;
    protected override double GetMutationRate() => 0.08;
    protected override bool UseTrainingScoreForGoat() => true;
    protected override string GetTrainingStatusText(int generation)
    {
        var phase = GetTrainingPhase(generation);
        var phaseNumber = (int)phase + 1;
        var phaseName = GetTrainingPhaseName(phase);
        var phaseEndGeneration = GetTrainingPhaseEndGeneration(phase);
        return phaseEndGeneration.HasValue
            ? $"Phase {phaseNumber}/4: {phaseName} (until gen {phaseEndGeneration.Value})"
            : $"Phase {phaseNumber}/4: {phaseName}";
    }

    private static string GetTrainingPhaseName(TrainingPhase phase) =>
        phase switch
        {
            TrainingPhase.Route => "route finding",
            TrainingPhase.BlockHunt => "block hunting",
            TrainingPhase.EnemyPractice => "enemy practice",
            _ => "show run"
        };

    protected override byte[] GetSavedBrainBytes() => Settings.Instance.MarioBrain;

    protected override AiBrainBase CreateBrain() => new MarioBrain();

    private static TrainingPhase GetTrainingPhase(int generation)
    {
        if (generation < 20)
            return TrainingPhase.Route;
        if (generation < 45)
            return TrainingPhase.BlockHunt;
        if (generation < 75)
            return TrainingPhase.EnemyPractice;
        return TrainingPhase.ShowRun;
    }

    private static int? GetTrainingPhaseEndGeneration(TrainingPhase phase) =>
        phase switch
        {
            TrainingPhase.Route => 20,
            TrainingPhase.BlockHunt => 45,
            TrainingPhase.EnemyPractice => 75,
            _ => null
        };

    private static double GetShowRewardMultiplier(TrainingPhase phase) =>
        phase switch
        {
            TrainingPhase.Route => 0.45,
            TrainingPhase.BlockHunt => 1.25,
            TrainingPhase.EnemyPractice => 1.0,
            _ => 1.45
        };
}
