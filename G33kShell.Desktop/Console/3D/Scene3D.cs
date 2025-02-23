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
//#define VIEW_Z_BUFFER

#if VIEW_Z_BUFFER
using CSharp.Core;
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CSharp.Core.Extensions;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console._3D;

/// <summary>
/// Represents a 3D scene containing multiple objects that can be rendered onto a screen.
/// Handles transformations, projections, and rendering of all scene objects.
/// </summary>
public class Scene3D
{
    private readonly ScreenData m_screen;
    private readonly SceneBackground m_sceneBackground;
    private readonly List<SceneObject> m_objects = [];
    private float[,] m_depthBuffer;
#if VIEW_Z_BUFFER
    private static float m_maxDepth = float.MinValue; // Used for debugging.
    private static float m_minDepth = float.MaxValue; // Used for debugging.
#endif

    public Scene3D([NotNull] ScreenData screen, [NotNull] SceneBackground sceneBackground)
    {
        m_screen = screen ?? throw new ArgumentNullException(nameof(screen));
        m_sceneBackground = sceneBackground ?? throw new ArgumentNullException(nameof(sceneBackground));
        
        ClearDepthBuffer();
    }

    /// <summary>
    /// Adds a 3D object to the scene.
    /// </summary>
    public void AddObject(SceneObject obj) => m_objects.Add(obj);

    /// <summary>
    /// Renders the scene onto the given screen, applying transformations and projections to all objects.
    /// </summary>
    public void Render(double time)
    {
        m_sceneBackground.Clear(m_screen, time);
        ClearDepthBuffer();

        foreach (var obj in m_objects)
        {
            var vertices =
                obj
                    .Vertices
                    .Select(v => Vector3.Transform(v, obj.Transform)).ToArray();
            foreach (var face in obj.Faces)
                FillTriangle(vertices[face.I0], vertices[face.I1], vertices[face.I2], face.Material);
        }
    }

    private void ClearDepthBuffer()
    {
        // Ensure the depth buffer is allocated.
        m_depthBuffer ??= new float[m_screen.Width, m_screen.Height];

        // Reset content.
        for (var y = 0; y < m_screen.Height; y++)
        {
            for (var x = 0; x < m_screen.Width; x++)
                m_depthBuffer[x, y] = float.MaxValue;
        }
    }

    /// <summary>
    /// Prepares a 3D vertex for display onto a 2D plane using a simple perspective transformation.
    /// </summary>
    private Vector2 GetProjectedXy(Vector3 v)
    {
        var perspectiveFactor = 4.0f / (4.0f + v.Z);
        return new Vector2(
            v.X * ((float)m_screen.Width / m_screen.Height),
            -v.Y
        ) * perspectiveFactor;
    }

    /// <summary>
    /// Fills a triangle on the screen using character-based rendering.
    /// </summary>
    private void FillTriangle(Vector3 v0, Vector3 v1, Vector3 v2, Attr material)
    {
        var maxZ = MathF.Max(MathF.Max(v0.Z, v1.Z), v2.Z);
        if (maxZ < 0.0f)
            return; // Behind the camera plane.

        var screenWidth = m_screen.Width;
        var screenHeight = m_screen.Height;
        
        // Project points into the screen space.
        var majorAxis = Math.Min(screenWidth, screenHeight) / 2.0f;
        var worldToScreen = new Vector2(screenWidth / 2.0f, screenHeight / 2.0f);
        var p0 = GetProjectedXy(v0) * majorAxis + worldToScreen;
        var p1 = GetProjectedXy(v1) * majorAxis + worldToScreen;
        var p2 = GetProjectedXy(v2) * majorAxis + worldToScreen;

        // Bail early if triangle is completely off the screen.
        var left = MathF.Min(MathF.Min(p0.X, p1.X), p2.X);
        var right = MathF.Max(MathF.Max(p0.X, p1.X), p2.X);
        var top = MathF.Min(MathF.Min(p0.Y, p1.Y), p2.Y);
        var bottom = MathF.Max(MathF.Max(p0.Y, p1.Y), p2.Y);
        if (right < 0 || left >= screenWidth || bottom < 0 || top >= screenHeight)
            return;

        // Crop the triangle to the screen.
        left = MathF.Max(left, 0.0f);
        right = MathF.Min(right, screenWidth - 1.0f);
        top = MathF.Max(top, 0.0f);
        bottom = MathF.Min(bottom, screenHeight - 1.0f);

        // Triangle area (for barycentric weights)
        var areaInv = 1.0f / (p1 - p0).Cross(p2 - p0);

        // Draw each triangle point.
        for (var y = (int)top; y <= (int)bottom; y++)
        {
            for (var x = (int)left; x <= (int)right; x++)
            {
                var p = new Vector2(x, y);
                var edge1 = p2 - p;

                // Compute barycentric coordinates
                var w0 = (p1 - p).Cross(edge1) * areaInv;
                var w1 = edge1.Cross(p0 - p) * areaInv;
                var w2 = 1.0f - w0 - w1;

                if (w0 >= 0 && w1 >= 0 && w2 >= 0)
                {
                    var z = w0 * v0.Z + w1 * v1.Z + w2 * v2.Z;
                    
                    if (z < 0.0f)
                        continue; // Behind the camera plane.

                    if (z < m_depthBuffer[x, y])
                    {
                        m_depthBuffer[x, y] = z;
                        m_screen.Chars[y][x].Set(material);
#if VIEW_Z_BUFFER
                        m_minDepth = Math.Min(z, m_minDepth);
                        m_maxDepth = Math.Max(z, m_maxDepth);
#endif
                    }
                }
            }
        }

#if VIEW_Z_BUFFER
        // View the depth buffer.
        for (var y = 0; y < screenHeight; y++)
        {
            for (var x = 0; x < screenWidth; x++)
            {
                if (m_depthBuffer[x, y] < float.MaxValue)
                {
                    var f = (double)m_depthBuffer[x, y].InverseLerp(m_minDepth, m_maxDepth);
                    m_screen.SetBackground(x, y, f.Lerp(Rgb.Black, Rgb.White));
                }
            }
        }
#endif
    }

    public void Plot(Vector3 p, Attr material)
    {
        if (p.Z < 0.0f)
            return;

        var screenWidth = m_screen.Width;
        var screenHeight = m_screen.Height;

        // Project points into the screen space.
        var majorAxis = Math.Min(screenWidth, screenHeight) / 2.0f;
        var worldToScreen = new Vector2(screenWidth / 2.0f, screenHeight / 2.0f);
        var xy = GetProjectedXy(p) * majorAxis + worldToScreen;

        var px = (int)Math.Round(xy.X);
        var py = (int)Math.Round(xy.Y);
        if (px < 0 || px >= m_screen.Width || py < 0 || py >= m_screen.Height)
            return; // Off screen.

        if (p.Z >= m_depthBuffer[px, py])
            return; // Point obscured by nearer content.
        
        m_depthBuffer[px, py] = p.Z;
        m_screen.Chars[py][px].Set(material);
    }
}