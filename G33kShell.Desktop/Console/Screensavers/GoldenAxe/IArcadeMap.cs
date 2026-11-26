// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// for any purpose. The software is provided as is, without warranty of any kind.

using System;
using System.IO;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

internal interface IArcadeMap : IDisposable
{
    bool AdvanceTo(double seconds);
    void Draw(PixelScreenData screen, int[] shades, double elapsedSeconds);

    static IArcadeMap Load(ArcadeData data)
    {
        var fileName = $"map{data.AreaIndex + 1}.gmap";
        IArcadeMap map = ArcadeMapRecording.TryLoad(Path.Combine(ArcadeData.LocalAssetDirectory, fileName));
        if (map == null && data.AreaIndex == ArcadeData.AreaCount - 1)
            map = VictoryMap.Load(data);
        return map ?? ArcadeMapRecording.TryLoad(Path.Combine(ArcadeData.AssetDirectory, fileName));
    }
}
