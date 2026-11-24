// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Linq;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Eases only the displayed view; the arcade camera still owns progression and collisions.
/// </summary>
internal sealed class FollowCamera
{
    private double m_x, m_y;
    public double X => Math.Round(m_x);
    public double Y => Math.Round(m_y);

    public void Reset(double x, double y) => (m_x, m_y) = (x, y);

    public void Tick(Game game, ArcadeData data)
    {
        if (game.IsCamp || Math.Abs(game.CameraX - m_x) > 96 || Math.Abs(game.CameraY - m_y) > 96)
        {
            Reset(game.CameraX, game.CameraY);
            return;
        }

        // At 60 Hz this settles 95% of a stationary offset in about 0.6 seconds.
        const double follow = 0.08;
        m_x += (game.CameraX - m_x) * follow;
        m_y += (game.CameraY - m_y) * follow;
        var feet = game.Project(game.Hero.X, game.Hero.Z, game.Hero.Y);
        // Never lead the logical camera into a locked stage, or leave a moving hero off-screen.
        m_x = Math.Clamp(m_x, game.CameraX, game.CameraX + Math.Clamp(304 - feet.X, 0, 28));
        m_y = Math.Clamp(m_y, game.CameraY + Math.Max(-16, Math.Min(0, 32 - feet.Y)),
            game.CameraY + Math.Min(16, Math.Max(0, 198 - feet.Y)));
        var range = data.CameraPath.OrderBy(p => Math.Abs(p.Key - (int)Math.Floor(X / 8))).First().Value;
        m_y = Math.Clamp(m_y, range[0], range[1]);
    }
}
