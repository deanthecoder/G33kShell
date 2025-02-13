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
using System.Linq;
using CSharp.Core;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A canvas to displaying an animated incompressible fluid.
/// </summary>
[DebuggerDisplay("FluidCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class FluidCanvas : ScreensaverBase
{
    private readonly Random m_random = new Random();
    private FluidCube m_fluid;
    private bool m_isLit;
    private int m_dyeX;

    public FluidCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight)
    {
        Name = "fluid";
    }

    protected override void BuildScreen(ScreenData screen)
    {
        // Seed screen with invisible characters.
        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Width; x++)
            {
                screen.PrintAt(x, y, new Attr('▄')
                {
                    Foreground = Background
                });
            }
        }
            
        // Define the initial state of the fluid.
        m_fluid = new FluidCube(Math.Min(Width, Height), 0.0, 0.0010, 0.15);
        RefreshDyeX();
        m_isLit = true;
    }

    protected override void UpdateFrame(ScreenData screen)
    {
        // Inject more dye and velocity.
        var dyeToAdd = 0.5 + m_random.NextDouble() * 2.5;
        var angle = -((m_random.NextDouble() - 0.5) * Math.PI - Math.PI / 2);
        var speed = 0.5 + 1.5 * m_random.NextDouble();
        var velocityX = Math.Cos(angle) * speed;
        var velocityY = Math.Sin(angle) * speed;
        
        if (!m_isLit)
        {
            dyeToAdd = 0.0;
            velocityY *= 0.5;
        }

        m_fluid.AddDensity(m_dyeX, 2, dyeToAdd);
        m_fluid.AddVelocity(m_dyeX, 3, velocityX, velocityY);

        // Iterate the simulation.
        m_fluid.Step();

        // Update the screen.
        var left = (Width >> 1) - m_fluid.Size;
        for (var y = 0; y < m_fluid.Size - 1; y++)
        {
            // Every other row.
            for (var x = 0; x < m_fluid.Size - 1; x++)
            {
                var leftSide = GetDensityRgb(x, y);
                screen.SetBackground(left + x * 2, y, leftSide);

                var rightSide = GetDensityRgb(x + 1, y);
                screen.SetBackground(left + x * 2 + 1, y, 0.5.Lerp(leftSide, rightSide));
            }
        }

        // Fill the missing rows.
        for (var y = 0; y < m_fluid.Size - 2; y++)
        {
            for (var x = 0; x < (m_fluid.Size - 1) * 2; x++)
            {
                var topSide = screen.Chars[y][left + x].Background;
                var bottomSide = screen.Chars[y + 1][left + x].Background;
                screen.SetForeground(left + x, y, 0.5.Lerp(topSide, bottomSide));
            }
        }

        // Turn the flame on/off to prevent too much 'smoke'.
        var averageDensity = m_fluid.Density.Average();
        if (m_isLit)
        {
            if (averageDensity > 0.45)
            {
                // Turn off source until the smoke clears.
                m_isLit = false;
            }
        }
        else
        {
            m_fluid.Dim(0.001);
            if (averageDensity < 0.18)
            {
                // Light up again.
                m_isLit = true;
                RefreshDyeX();
            }
        }
    }

    private void RefreshDyeX()
    {
        m_dyeX = (m_fluid.Size >> 1) + (int)((m_random.NextDouble() * 2.0 - 1.0) * m_fluid.Size / 3.0);
    }

    private Rgb GetDensityRgb(int x, int y)
    {
        var density = m_fluid.Density[m_fluid.Ix(x, m_fluid.Size - y - 1)];

        // Apply some contrast.
        const double contrast = 1.75;
        density = (density - 0.5) * contrast + 0.5;

        return density.Clamp(0.0, 1.0).Lerp(Background, Foreground);
    }

    private class FluidCube
    {
        private readonly double m_dt;
        private readonly double m_diff;
        private readonly double m_visc;
        private readonly double[] m_s;
        private readonly double[] m_vx;
        private readonly double[] m_vy;
        private readonly double[] m_vx0;
        private readonly double[] m_vy0;

        private enum Boundary
        {
            None = 0,
            Horizontal = 1,
            Vertical = 2
        }

        public readonly int Size;
        public readonly double[] Density;

        public FluidCube(int size, double diffusion, double viscosity, double dt)
        {
            Size = size;
            m_s = new double[size * size];
            Density = new double[size * size];

            m_vx = new double[size * size];
            m_vy = new double[size * size];

            m_vx0 = new double[size * size];
            m_vy0 = new double[size * size];
            
            m_diff = diffusion;
            m_visc = viscosity;
            m_dt = dt;
        }

        public int Ix(int x, int y) =>
            y * Size + x;

        public void AddDensity(int x, int y, double amount) =>
            Density[Ix(x, y)] += amount;

        public void AddVelocity(int x, int y, double amountX, double amountY)
        {
            var index = Ix(x, y);
            m_vx[index] += amountX;
            m_vy[index] += amountY;
        }

        private void SetBoundary(Boundary b, double[] x)
        {
            // Set top and bottom boundaries
            for (var i = 1; i < Size - 1; i++)
            {
                x[Ix(i, 0)] = b == Boundary.Vertical ? -x[Ix(i, 1)] : x[Ix(i, 1)];
                x[Ix(i, Size - 1)] = b == Boundary.Vertical ? -x[Ix(i, Size - 2)] : x[Ix(i, Size - 2)];
            }

            // Set left and right boundaries
            for (var j = 1; j < Size - 1; j++)
            {
                x[Ix(0, j)] = b == Boundary.Horizontal ? -x[Ix(1, j)] : x[Ix(1, j)];
                x[Ix(Size - 1, j)] = b == Boundary.Horizontal ? -x[Ix(Size - 2, j)] : x[Ix(Size - 2, j)];
            }

            // Set corners
            x[Ix(0, 0)] = 0.5 * (x[Ix(1, 0)] + x[Ix(0, 1)]);
            x[Ix(0, Size - 1)] = 0.5 * (x[Ix(1, Size - 1)] + x[Ix(0, Size - 2)]);
            x[Ix(Size - 1, 0)] = 0.5 * (x[Ix(Size - 2, 0)] + x[Ix(Size - 1, 1)]);
            x[Ix(Size - 1, Size - 1)] = 0.5 * (x[Ix(Size - 2, Size - 1)] + x[Ix(Size - 1, Size - 2)]);
        }

        private void LinSolve(Boundary b, double[] x, double[] x0, double a, double c, int iter)
        {
            var cRecip = 1.0 / c;
            for (var k = 0; k < iter; k++)
            {
                for (var j = 1; j < Size - 1; j++)
                {
                    for (var i = 1; i < Size - 1; i++)
                    {
                        x[Ix(i, j)] = (x0[Ix(i, j)] +
                                          a * (x[Ix(i + 1, j)] +
                                               x[Ix(i - 1, j)] +
                                               x[Ix(i, j + 1)] +
                                               x[Ix(i, j - 1)])) * cRecip;
                    }
                }
                SetBoundary(b, x);
            }
        }

        private void Diffuse(Boundary b, double[] x, double[] x0, double diff, double dt, int iter)
        {
            var a = dt * diff * (Size - 2) * (Size - 2);
            LinSolve(b, x, x0, a, 1 + 4 * a, iter);
        }

        private void Project(double[] velocX, double[] velocY, double[] p, double[] div, int iter)
        {
            for (var j = 1; j < Size - 1; j++)
            {
                for (var i = 1; i < Size - 1; i++)
                {
                    div[Ix(i, j)] = -0.5 * (
                        velocX[Ix(i + 1, j)] - velocX[Ix(i - 1, j)] +
                        velocY[Ix(i, j + 1)] - velocY[Ix(i, j - 1)]
                    ) / Size;

                    p[Ix(i, j)] = 0;
                }
            }

            SetBoundary(Boundary.None, div);
            SetBoundary(Boundary.None, p);
            LinSolve(Boundary.None, p, div, 1, 4, iter);

            for (var j = 1; j < Size - 1; j++)
            {
                for (var i = 1; i < Size - 1; i++)
                {
                    velocX[Ix(i, j)] -= 0.5 * (p[Ix(i + 1, j)] - p[Ix(i - 1, j)]) * Size;
                    velocY[Ix(i, j)] -= 0.5 * (p[Ix(i, j + 1)] - p[Ix(i, j - 1)]) * Size;
                }
            }

            SetBoundary(Boundary.Horizontal, velocX);
            SetBoundary(Boundary.Vertical, velocY);
        }

        private void Advect(Boundary b, double[] d, double[] d0, double[] velocX, double[] velocY, double dt)
        {
            var dtx = dt * (Size - 2);
            var dty = dt * (Size - 2);

            for (var j = 1; j < Size - 1; j++)
            {
                for (var i = 1; i < Size - 1; i++)
                {
                    var tmp1 = dtx * velocX[Ix(i, j)];
                    var tmp2 = dty * velocY[Ix(i, j)];
                    var x = i - tmp1;
                    var y = j - tmp2;

                    x = Math.Clamp(x, 0.5, Size - 0.5);
                    y = Math.Clamp(y, 0.5, Size - 0.5);

                    var i0 = (int)Math.Floor(x);
                    var i1 = i0 + 1;
                    var j0 = (int)Math.Floor(y);
                    var j1 = j0 + 1;

                    var s1 = x - i0;
                    var s0 = 1.0 - s1;
                    var t1 = y - j0;
                    var t0 = 1.0 - t1;

                    d[Ix(i, j)] =
                        s0 * (t0 * d0[Ix(i0, j0)] + t1 * d0[Ix(i0, j1)]) +
                        s1 * (t0 * d0[Ix(i1, j0)] + t1 * d0[Ix(i1, j1)]);
                }
            }
            
            SetBoundary(b, d);
        }

        public void Step()
        {
            Diffuse(Boundary.Horizontal, m_vx0, m_vx, m_visc, m_dt, 4);
            Diffuse(Boundary.Vertical, m_vy0, m_vy, m_visc, m_dt, 4);

            Project(m_vx0, m_vy0, m_vx, m_vy, 4);

            Advect(Boundary.Horizontal, m_vx, m_vx0, m_vx0, m_vy0, m_dt);
            Advect(Boundary.Vertical, m_vy, m_vy0, m_vx0, m_vy0, m_dt);

            Project(m_vx, m_vy, m_vx0, m_vy0, 4);

            Diffuse(Boundary.None, m_s, Density, m_diff, m_dt, 4);
            Advect(Boundary.None, Density, m_s, m_vx, m_vy, m_dt);
        }

        public void Dim(double factor)
        {
            for (var i = 0; i < Density.Length; i++)
                Density[i] = Math.Max(Density[i] - factor, 0.0);
        }
    }
}