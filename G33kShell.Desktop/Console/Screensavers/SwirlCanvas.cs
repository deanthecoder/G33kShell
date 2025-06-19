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

using System;
using System.Diagnostics;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

[DebuggerDisplay("SwirlCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class SwirlCanvas : ScreensaverBase
{
    private const double Pi2 = Math.PI * 2.0;

    public SwirlCanvas(int width, int height) : base(width, height)
    {
        Name = "swirl";
    }

    public override void UpdateFrame(ScreenData screen)
    {
        var timeSecs = Time;
        var centerX = screen.Width * 0.5;
        var centerY = screen.Height * 0.5;

        for (var y = 0; y < screen.Height; y++)
        {
            var dy = (y - centerY) / centerY;
            for (var x = 0; x < screen.Width; x++)
            {
                var dx = (x - centerX) / centerX;
                var radius = Math.Sqrt(dx * dx + dy * dy);
                var angle = Math.Atan2(dy, dx) + timeSecs;

                var modAngle = (angle + Math.Sin(-radius + timeSecs) * 2.0) % Pi2;
                var gradient = Math.Sin(modAngle * 6.0) * 0.5 + 0.5;

                screen.PrintAt(x, y, gradient.ToAscii());
            }
        }
        
        screen.PrintAt((int)(centerX - 1), (int)centerY, "DTC");
    }
}