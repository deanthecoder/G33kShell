// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
//  purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.
using System.Diagnostics;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

[DebuggerDisplay("PlasmaCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class PlasmaCanvas : ScreensaverBase
{
    public PlasmaCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 15)
    {
        Name = "plasma";
    }

    public override void UpdateFrame(ScreenData screen)
    {
        var time = Time;
        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Width; x++)
            {
                var f = (x * 1.5, y * 3.0, time * 12.0).SmoothNoise();
                screen.PrintAt(x, y, f.ToAscii());
            }
        }
    }
}