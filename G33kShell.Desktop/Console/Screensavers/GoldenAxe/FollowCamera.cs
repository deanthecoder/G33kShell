// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;

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
            if (!game.IsCamp)
                m_y = data.KeepDisplayViewCovered(X, m_y, data.CameraRange(X));
            return;
        }

        // At 60 Hz this settles 95% of a stationary offset in about 0.6 seconds.
        const double follow = 0.08;
        var previousY = m_y;
        m_x += (game.CameraX - m_x) * follow;
        m_y += (game.CameraY - m_y) * follow;
        var feet = game.Project(game.Hero.X, game.Hero.Z, game.Hero.Y);
        // Never lead the logical camera into a locked stage, or leave a moving hero off-screen.
        m_x = Math.Clamp(m_x, game.CameraX, game.CameraX + Math.Clamp(304 - feet.X, 0, 28));
        m_y = Math.Clamp(m_y, game.CameraY + Math.Max(-16, Math.Min(0, 32 - feet.Y)),
            game.CameraY + Math.Min(16, Math.Max(0, game.BottomFeetLimit + 2 - feet.Y)));
        var range = data.CameraRange(X);
        m_y = Math.Clamp(m_y, range.Low, range.High);
        // A changing terrain range or feet guard must not bypass the smooth follow.
        m_y = Math.Clamp(m_y, previousY - 2, previousY + 2);
        if (data.UsesCastleOpeningFraming(4089 - X))
            m_y = Math.Max(m_y, range.Low);
        m_y = data.KeepDisplayViewCovered(X, m_y, range);
    }
}
