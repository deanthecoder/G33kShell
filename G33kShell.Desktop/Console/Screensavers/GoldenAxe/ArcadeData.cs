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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DTC.Core;
using DTC.Core.Extensions;
using Newtonsoft.Json.Linq;
using SkiaSharp;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Reads the set-6 arcade data exported by atomic14's extraction tools.
/// Coordinates, frame offsets and animation addresses retain the ROM's units.
/// </summary>
/// <remarks>
/// Private assets are loaded once per screensaver instance and never downloaded at runtime.
/// </remarks>
internal sealed class ArcadeData
{
    public const int AreaCount = 5;
    public const int Width = 320;
    public const int Height = 224;
    public static string AssetDirectory => Path.Combine(AppContext.BaseDirectory, "Assets", "GoldenAxe");
    public static string LocalAssetDirectory => Path.Combine(Assembly.GetEntryAssembly().GetAppSettingsPath().FullName, "GoldenAxe");
    public static readonly string[] RequiredFiles = ["sprites.json", "sprites-0.png", "tiles.png", "level1.json", "level2.json", "level3.json", "level4.json", "level5.json", "magic.json"];
    public static bool IsAvailable => RequiredFiles.All(f => File.Exists(Path.Combine(AssetDirectory, f)));


    public SpriteFrame[] Frames { get; }
    public Dictionary<int, AnimationStep[]> Animations { get; }
    public Dictionary<int, int[]> Attacks { get; }
    public Dictionary<int, int[][]> Riders { get; }
    public Dictionary<string, int[][][][]> Magic { get; }
    public int[][] SpriteColors { get; }
    public byte[][] SpritePalette { get; }
    public byte[] SpritePens { get; }
    public int SpriteStride { get; }
    public byte[] TilePens { get; }
    public int TileStride { get; }
    public TileLayer Foreground { get; }
    public TileLayer Background { get; }
    public byte[] HeightMap { get; }
    public SpawnDefinition[] Spawns { get; }
    public SpawnDefinition[] CampSpawns { get; }
    public int[] Stops { get; }
    public double[][] Parallax { get; }
    public int InitialCameraX { get; }
    public int CameraY { get; }
    public int AreaIndex { get; }
    public int StageNumber => AreaIndex * 2 + 1;
    public string DayPalette => StageNumber.ToString(CultureInfo.InvariantCulture);
    public string BossPalette => (100 + StageNumber).ToString(CultureInfo.InvariantCulture);
    public Dictionary<int, int[]> CameraPath { get; }
    public Dictionary<string, uint[]> TileColors { get; }
    public Dictionary<string, byte[]> TilePalettes { get; }
    public Dictionary<int, byte[]> PortraitPalettes { get; }
    public Rgb[] Palette { get; }
    public int StaticPaletteCount => AreaIndex == 3 ? EagleSky.PaletteStart : 256;
    public byte White { get; }
    public byte Black { get; }

    public ArcadeData(string directory, int areaIndex = 0)
    {
        AreaIndex = areaIndex is >= 0 and < AreaCount ? areaIndex : throw new ArgumentOutOfRangeException(nameof(areaIndex));
        var sprites = JObject.Parse(File.ReadAllText(Path.Combine(directory, "sprites.json")));
        var level = JObject.Parse(File.ReadAllText(Path.Combine(directory, $"level{areaIndex + 1}.json")));
        Frames = sprites["frames"].Select(f => new SpriteFrame((int)f[1], (int)f[2], (int)f[3], (int)f[4], (int)f[5], (int)f[6])).ToArray();
        Animations = ((JObject)sprites["anims"]).Properties().ToDictionary(p => Hex(p.Name), p => p.Value.Select(s =>
            new AnimationStep((int)s[0], Math.Max(1, (int)s[1]), (int)s[2], (int)s[3], (int)s[4], s[5].ToObject<int[]>())).ToArray());
        FixMaceSwingPoses();
        Attacks = ((JObject)sprites["attacks"]).Properties().ToDictionary(p => Hex(p.Name), p => p.Value.ToObject<int[]>());
        Riders = ((JObject)sprites["riders"]).Properties().ToDictionary(p => Hex(p.Name), p => p.Value.ToObject<int[][]>());
        SpriteColors = sprites["palettes"].Select(p => Enumerable.Range(0, 16).Select(i => Hex(p.ToString().Substring(i * 6, 6))).ToArray()).ToArray();
        (SpritePens, SpriteStride) = ReadPens(Path.Combine(directory, "sprites-0.png"), 17);
        (TilePens, TileStride) = ReadPens(Path.Combine(directory, "tiles.png"), 32);
        Foreground = ReadLayer(level["fg"]);
        Background = ReadLayer(level["bg"]);
        HeightMap = Convert.FromBase64String((string)level["height"]);
        Spawns = level["spawns"].Select(s => new SpawnDefinition((int)s[0], (int)s[1], (int)s[2], (int)s[3], (int)s[4])).OrderBy(s => s.Trigger).ToArray();
        CampSpawns = level["camp"].Select(s => new SpawnDefinition((int)s[0], (int)s[1], (int)s[2], (int)s[3], (int)s[4])).ToArray();
        Stops = level["stops"].ToObject<int[]>();
        Parallax = level["parallax"].ToObject<double[][]>();
        InitialCameraX = (int)level["cam"][1][0];
        CameraY = (int)level["cam"][1][1];
        CameraPath = ((JObject)level["camPath"]).Properties().ToDictionary(p => int.Parse(p.Name, CultureInfo.InvariantCulture), p => p.Value.ToObject<int[]>());
        TileColors = ((JObject)level["palettes"]).Properties().Where(p => p.Name == DayPalette || p.Name == BossPalette || p.Name == "0").ToDictionary(p => p.Name, p => DecodeWords((string)p.Value));
        Magic = JObject.Parse(File.ReadAllText(Path.Combine(directory, "magic.json"))).Properties().ToDictionary(p => p.Name, p => p.Value.ToObject<int[][][][]>());

        // Weight visible tiles and actor colors, rather than unused ROM palette entries.
        var histogram = new Dictionary<int, int>();
        void Count(int rgb, int weight) => histogram[rgb] = histogram.GetValueOrDefault(rgb) + weight;
        foreach (var colors in TileColors.Values)
        foreach (var layer in new[] { Background, Foreground })
        foreach (var cell in layer.Cells)
        {
            if (cell == uint.MaxValue)
                continue;
            var tile = (int)(cell & 0xffff);
            var group = (int)((cell >> 16) & 127) * 8;
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                var pen = TilePens[(tile / 128 * 8 + y) * TileStride + tile % 128 * 8 + x];
                if (pen != 0 || layer == Background)
                    Count((int)colors[group + pen], 1);
            }
        }
        var actorPalettes = new HashSet<int> { 0x13, 0x14, 0x15, 0x18, 0x1a, 0x1b, 0x77, 0x78, 0x79, 0x38, 0x3d, 0x4a, 0x4d, 0x63, 0x64, 0x65, 0x7b, 0x23, 0x43, 0x44, 0x45, 0x1c, 0x1d, 0x86, 0x87, 0x73, 0x6f, 0x70, 0x50, 0x52, 0x39, 0x7a, 0x3c, 0x7f };
        foreach (var effect in Magic.Values.SelectMany(h => h).SelectMany(l => l).SelectMany(t => t)) actorPalettes.Add(effect[1]);
        actorPalettes.Add(0x40); // GO! sword and lettering.
        actorPalettes.UnionWith(new[] { 0x4b, 0x4c, 0x4d, 0x4e }); // Dean-selected civilian palettes.
        actorPalettes.Add(0x42); // Injured villager: grey clothes and red wounds, matched to Dean's MAME image.
        actorPalettes.Add(SkeletonEmergence.GroundPalette);
        actorPalettes.Add(WeaponSwipe.SilverPalette);
        actorPalettes.Add(0x97); // MAME-confirmed warm stone for fallen humanoids.
        actorPalettes.Add(0x72); // MAME-confirmed ochre bone for fallen skeletons.
        if (AreaIndex == 2)
            actorPalettes.Add(SceneryEyes.TurtlePalette);
        if (AreaIndex == 4)
            actorPalettes.Add(SceneryEyes.EaglePalette);
        if (AreaIndex == 3)
            actorPalettes.Add(EagleFeathers.Palette);
        foreach (var p in actorPalettes)
        for (var i = 1; i < 15; i++) Count(SpriteColors[p][i], 500);
        Count(0, 10000);
        Count(0xffffff, 10000);
        // Keep the arcade lives color exact within the 256-color palette.
        Palette = Quantize(histogram, StaticPaletteCount - 1);
        Palette[StaticPaletteCount - 1] = new Rgb(255, 203, 0);
        var cache = new Dictionary<int, byte>();
        byte Map(int rgb)
        {
            if (cache.TryGetValue(rgb, out var found))
                return found;
            var best = 0;
            var distance = int.MaxValue;
            for (var i = 0; i < StaticPaletteCount; i++)
            {
                var dr = (rgb >> 16 & 255) - Palette[i].R;
                var dg = (rgb >> 8 & 255) - Palette[i].G;
                var db = (rgb & 255) - Palette[i].B;
                var d = dr * dr * 2 + dg * dg * 3 + db * db;
                if (d < distance)
                {
                    distance = d;
                    best = i;
                }
            }
            return cache[rgb] = (byte)best;
        }
        SpritePalette = SpriteColors.Select(p => p.Select(Map).ToArray()).ToArray();
        TilePalettes = TileColors.ToDictionary(p => p.Key, p => p.Value.Select(c => Map((int)c)).ToArray());
        if (AreaIndex == 3)
        {
            for (var pen = 0; pen < 8; pen++)
            {
                Palette[EagleSky.PaletteStart + pen] = EagleSky.Color(EagleSky.Blue[pen], 0, 5055);
                foreach (var key in new[] { DayPalette, BossPalette })
                foreach (var group in new[] { 114, 115 })
                    TilePalettes[key][group * 8 + pen] = (byte)(EagleSky.PaletteStart + pen);
            }
        }
        PortraitPalettes = HeroDefinition.All.ToDictionary(hero => hero.PortraitTile, hero => hero.PortraitColors.Select(Map).ToArray());
        Black = Map(0);
        White = Map(0xffffff);
        FillBackgroundGaps();
    }

    private void FixMaceSwingPoses()
    {
        // Hold the follow-through where these scripts briefly replay the strike.
        HoldPreviousPose(Moves.Heninger.Attacks[0], 7, 126, 127);
        HoldPreviousPose(Moves.StrongHeninger.Attacks[0], 7, 126, 127);

        foreach (var moves in new[] { Moves.Longmoan, Moves.FastLongmoan, Moves.StrongLongmoan })
        {
            var address = moves.Attacks[0];
            // The idle pose interrupts Longmoan's wind-up for two ticks.
            HoldPreviousPose(address, 1, 985, 1009);
            HoldPreviousPose(address, 9, 1011, 1012);
        }
    }

    private void HoldPreviousPose(int address, int index, int frame, int previousFrame)
    {
        if (Animations.TryGetValue(address, out var steps) && steps.Length > index &&
            steps[index].Frame == frame && steps[index - 1].Frame == previousFrame)
            steps[index] = steps[index - 1] with { Duration = steps[index].Duration };
    }

    private void FillBackgroundGaps()
    {
        // Captures omit tiles that were only partly on screen. Extend the nearest
        // captured background into those holes without changing intentional black tiles
        // or the foreground's transparency. Do this after measuring palette usage.
        var cells = Background.Cells;
        var pending = new Queue<int>();
        for (var i = 0; i < cells.Length; i++)
            if (cells[i] != uint.MaxValue)
                pending.Enqueue(i);
        while (pending.TryDequeue(out var index))
        {
            var column = index % Background.Width;
            if (column > 0) Extend(index - 1);
            if (column + 1 < Background.Width) Extend(index + 1);
            if (index >= Background.Width) Extend(index - Background.Width);
            if (index + Background.Width < cells.Length) Extend(index + Background.Width);

            void Extend(int neighbour)
            {
                if (cells[neighbour] != uint.MaxValue)
                    return;
                cells[neighbour] = cells[index];
                pending.Enqueue(neighbour);
            }
        }
    }

    private static int Hex(string text) => int.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    private static TileLayer ReadLayer(JToken t) => new((int)t["x"], (int)t["y"], (int)t["w"], (int)t["h"], DecodeWords((string)t["cells"]));

    private static uint[] DecodeWords(string text)
    {
        var bytes = Convert.FromBase64String(text);
        var words = new uint[bytes.Length / 4];
        for (var i = 0; i < words.Length; i++)
            words[i] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * 4, 4));
        return words;
    }

    private static (byte[], int) ReadPens(string path, int scale)
    {
        using var bitmap = SKBitmap.Decode(path) ?? throw new InvalidDataException($"Cannot decode {path}.");
        var pixels = bitmap.Pixels;
        return (pixels.Select(c => c.Alpha == 0 ? (byte)0 : (byte)Math.Round(c.Red / (double)scale)).ToArray(), bitmap.Width);
    }

    /// <summary>
    /// Weighted median-cut palette shared by day, sunset and sprites, avoiding palette flicker.
    /// Black and white are reserved for fades and the HUD.
    /// </summary>
    private static Rgb[] Quantize(Dictionary<int, int> histogram, int colorCount)
    {
        histogram.Remove(0);
        histogram.Remove(0xffffff);
        var boxes = new List<List<KeyValuePair<int, int>>> { histogram.OrderBy(p => p.Key).ToList() };
        int Channel(int rgb, int shift) => rgb >> shift & 255;
        while (boxes.Count < colorCount - 2)
        {
            var candidate = boxes.Where(b => b.Count > 1).Select(b =>
            {
                var shift = new[] { 16, 8, 0 }.OrderByDescending(s => b.Max(p => Channel(p.Key, s)) - b.Min(p => Channel(p.Key, s))).First();
                var range = b.Max(p => Channel(p.Key, shift)) - b.Min(p => Channel(p.Key, shift));
                return (Box: b, Shift: shift, Score: range * Math.Sqrt(b.Sum(p => (double)p.Value)));
            }).OrderByDescending(b => b.Score).FirstOrDefault();
            if (candidate.Box == null)
                break;
            var sorted = candidate.Box.OrderBy(p => Channel(p.Key, candidate.Shift)).ThenBy(p => p.Key).ToList();
            var half = sorted.Sum(p => (long)p.Value) / 2;
            long sum = 0;
            var split = 0;
            while (split < sorted.Count - 1 && sum < half) sum += sorted[split++].Value;
            split = Math.Max(1, split);
            boxes.Remove(candidate.Box);
            boxes.Add(sorted.GetRange(0, split));
            boxes.Add(sorted.GetRange(split, sorted.Count - split));
        }
        var result = new Rgb[256];
        result[0] = Rgb.Black;
        result[1] = Rgb.White;
        for (var i = 0; i < boxes.Count; i++)
        {
            var box = boxes[i];
            var total = box.Sum(p => (long)p.Value);
            byte Average(int shift) => (byte)(box.Sum(p => (long)Channel(p.Key, shift) * p.Value) / total);
            result[i + 2] = new Rgb(Average(16), Average(8), Average(0));
        }
        for (var i = boxes.Count + 2; i < result.Length; i++) result[i] = Rgb.Black;
        return result;
    }
}
