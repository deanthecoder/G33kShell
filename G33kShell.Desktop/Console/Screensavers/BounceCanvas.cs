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
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using G33kShell.Desktop.Console.Extensions;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

[DebuggerDisplay("BounceCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class BounceCanvas : ScreensaverBase
{
    private readonly Attr[] m_brickAttr = new Attr[2];
    private readonly Ball[] m_balls = new Ball[4];
    private bool[,] m_bricks;

    public BounceCanvas(int width, int height) : base(width, height)
    {
        Name = "bounce";
    }

    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);

        screen.DrawBox(0, 0, screen.Width - 1, screen.Height - 1);
        m_brickAttr[0] = new Attr('░', Foreground);
        m_brickAttr[1] = new Attr(' ', Foreground);

        var quarterWidth = screen.Width / 4.0;
        m_balls[0] = SpawnBall(screen, quarterWidth * 1.5);
        m_balls[1] = SpawnBall(screen, quarterWidth * 0.5);
        m_balls[2] = SpawnBall(screen, quarterWidth * 3.5);
        m_balls[3] = SpawnBall(screen, quarterWidth * 2.5);

        m_bricks = new bool[screen.Width, screen.Height];
        for (var y = 1; y < screen.Height - 1; y++)
        {
            for (var x = 1; x < screen.Width - 1; x++)
            {
                var isBrick = ((x - 1) / (screen.Width / 4)) % 2 == 0;
                m_bricks[x, y] = isBrick;
                screen.PrintAt(x, y, m_brickAttr[isBrick ? 0 : 1]);
            }
        }
    }
    
    private static Ball SpawnBall(ScreenData screen, double x) =>
        new Ball(x, Random.Shared.Next(2, screen.Height - 2), Random.Shared.NextBool() ? -1 : 1, Random.Shared.NextBool() ? -1 : 1);

    public override void UpdateFrame(ScreenData screen)
    {
        for (var i = 0; i < m_balls.Length; i++)
        {
            for (var speedStep = 0; speedStep < 4; speedStep++)
            {
                for (var step = 0; step < 2; step++)
                {
                    // Advance position.
                    var ox = m_balls[i].X;
                    var oy = m_balls[i].Y;
                    var newBall = m_balls[i] with { X = ox + m_balls[i].Dx * 0.5 * (step == 0 ? 1 : 0), Y = oy + m_balls[i].Dy * 0.5 * (step == 1 ? 1 : 0) };

                    // Detect wall bounces.
                    if (newBall.X < 1)
                        newBall = newBall with { Dx = -newBall.Dx, X = 1 };
                    else if (newBall.X >= Width - 1)
                        newBall = newBall with { Dx = -newBall.Dx, X = Width - 1 - 0.5 };
                    if (newBall.Y < 1)
                        newBall = newBall with { Dy = -newBall.Dy, Y = 1 };
                    else if (newBall.Y >= Height - 1)
                        newBall = newBall with { Dy = -newBall.Dy, Y = Height - 1 - 0.5 };
                
                    // Detect brick collision.
                    var hitCondition = (i & 1) == 0;
                    (int X, int Y) brickXy = ((int)newBall.X, (int)newBall.Y);
                    var didHit = m_bricks[brickXy.X, brickXy.Y] == hitCondition;
                    if (didHit)
                    {
                        // Remove brick.
                        m_bricks[brickXy.X, brickXy.Y] = !m_bricks[brickXy.X, brickXy.Y];
                    
                        // Bounce off brick.
                        if ((int)ox < brickXy.X || (int)ox > brickXy.X)
                            newBall = newBall with { Dx = -newBall.Dx };
                        if ((int)oy < brickXy.Y || (int)oy > brickXy.Y)
                            newBall = newBall with { Dy = -newBall.Dy };
                    }
            
                    m_balls[i] = newBall;
            
                    // Update display.
                    if ((int)ox != (int)m_balls[i].X || (int)oy != (int)m_balls[i].Y)
                    {
                        screen.PrintAt((int)ox, (int)oy, m_brickAttr[1 - (i & 1)]);
                        screen.PrintAt((int)m_balls[i].X, (int)m_balls[i].Y, "☺☻"[i & 1]);
                    }
                }
            }
        }

        var filled = 0.0;
        for (var y = 1; y < screen.Height - 1; y++)
        {
            for (var x = 1; x < screen.Width - 1; x++)
            {
                if (m_bricks[x, y])
                    filled++;
            }
        }
        
        filled /= screen.Width * screen.Height;
        screen.PrintAt(4, screen.Height - 1, $"╡ ☻ {filled,3:P0} ╞");
        screen.PrintAt(screen.Width - 13, screen.Height - 1, $"╡ ☺ {1.0 - filled,3:P0} ╞");
    }
    
    private record Ball(double X, double Y, int Dx, int Dy);
}