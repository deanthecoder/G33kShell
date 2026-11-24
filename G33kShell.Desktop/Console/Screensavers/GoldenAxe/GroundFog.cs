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
using System.Linq;
using DTC.Core;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>Draws slowly drifting mist over the ground behind the actors.</summary>
internal static class GroundFog
{
    public static void Draw(PixelScreenData screen, Game game, double initialCameraY, Rgb tint = null, byte opacityScale = 255)
    {
        if (!game.IsCamp && game.AreaIndex != 0)
            return;

        var camp = game.IsCamp;
        var fire = camp ? game.Actors.FirstOrDefault(actor => actor.Kind == ActorKind.Campfire && !actor.IsGone) : null;
        (double X, double Y)? flame = fire == null ? null : game.ProjectView(fire.X, fire.Z, fire.Y);
        var height = camp ? (flame?.Y ?? 145) - 38 : 103 + game.ViewCamera.Y - initialCameraY;
        screen.FogColor = tint ?? (camp ? new Rgb(175, 188, 204) : new Rgb(190, 203, 206));
        screen.FogOpacity = opacityScale;

        var time = game.TickCount;
        var broadRadius = camp ? 17 : 26;
        var thinRadius = camp ? 8 : 13;
        var broadStrength = camp ? 60 : 88;
        var thinStrength = camp ? 29 : 36;
        for (var x = 0; x < screen.Width; x++)
        {
            var worldX = x - game.ViewCamera.X;
            var slow = worldX - time * 0.18;
            var fast = worldX - time * 0.33;
            var broadY = height + 7 * Math.Sin(slow * 0.028) + 3 * Math.Sin(slow * 0.083);
            var thinY = height + 15 + 4 * Math.Sin(fast * 0.046 + 0.8);
            var broad = broadStrength * (0.63 + 0.25 * Math.Sin(slow * 0.045) + 0.12 * Math.Sin(slow * 0.112 + 2));
            var thin = thinStrength * (0.55 + 0.45 * Math.Sin(fast * 0.051 + 1.2));
            var top = Math.Max(0, (int)Math.Floor(Math.Min(broadY - broadRadius, thinY - thinRadius)));
            var bottom = Math.Min(screen.Height - 1, (int)Math.Ceiling(Math.Max(broadY + broadRadius, thinY + thinRadius)));
            var fireDx = flame.HasValue ? (x - flame.Value.X) / 60 : 0;
            for (var y = top; y <= bottom; y++)
            {
                var opacity = broad * Falloff((y + 0.5 - broadY) / broadRadius) +
                              thin * Falloff((y + 0.5 - thinY) / thinRadius);
                if (flame.HasValue)
                {
                    var fireDy = (y - flame.Value.Y) / 61;
                    opacity *= Math.Clamp((fireDx * fireDx + fireDy * fireDy - 0.18) / 0.77, 0, 1);
                }

                if (opacity >= 1)
                    screen.SetFogPixel(y * screen.Width + x, (byte)opacity);
            }
        }
    }

    private static double Falloff(double distance)
    {
        var edge = Math.Clamp((1 - Math.Abs(distance)) / 0.52, 0, 1);
        return edge * edge * (3 - 2 * edge);
    }
}
