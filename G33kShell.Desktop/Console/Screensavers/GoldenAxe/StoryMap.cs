// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// for any purpose. The software is provided as is, without warranty of any kind.

using System;
using System.IO;
using Newtonsoft.Json;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

internal static class StoryMap
{
    private sealed record Timeline(int Ticks, int[][] InkTiles, ArcadeParchment.TextLine[] Text,
        int FirstQuill, int[][] Quill);

    internal static ArcadeParchment Load(ArcadeData data)
    {
        var name = $"story-map{data.AreaIndex + 1}";
        try
        {
            var json = File.ReadAllText(Path.Combine(ArcadeData.AssetDirectory, name + ".json"));
            var timeline = JsonConvert.DeserializeObject<Timeline>(json);
            if (timeline == null)
                return null;

            var ink = Array.ConvertAll(timeline.InkTiles, tile => (tile[0], tile[1]));
            var origins = Array.ConvertAll(timeline.Quill, point => (point[0], point[1]));
            var quillEnd = timeline.FirstQuill + origins.Length;
            return ArcadeParchment.Load(data, name + ".png", timeline.Ticks, ink, timeline.Text,
                tick => tick >= timeline.FirstQuill && tick < quillEnd ? origins[tick - timeline.FirstQuill] : null,
                quillEnd);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
