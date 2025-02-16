using System;
using CSharp.Core;
using CSharp.Core.Extensions;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console._3D;

/// <summary>
/// Chequered screen background which can be used for rendering a <see cref="Scene3D"/>.
/// </summary>
public class ChequeredSceneBackground : SceneBackground
{
    private readonly double m_colorDensity;

    public ChequeredSceneBackground([NotNull] Rgb foreground, [NotNull] Rgb background, double colorDensity) : base(foreground, background)
    {
        m_colorDensity = colorDensity;
    }

    public override void Clear(ScreenData screen, double time)
    {
        base.Clear(screen, time);

        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Width; x++)
            {
                var xx = (int)(100.0 + x + Math.Sin(3.0 * time) * 5.0);
                var yy = (int)(100.0 + y + (time + 0.5) * 8.0);

                var sizeX = (int)(screen.Width / 8.0);
                var sizeY = (int)(sizeX / 2.0);
                var isOnX = ((xx / sizeX) & 1) == 0;
                var isOnY = ((yy / sizeY) & 1) == 1;
                if (isOnX ^ isOnY)
                    screen.SetBackground(x, y, m_colorDensity.Lerp(Background, Foreground));
            }
        }
    }
}