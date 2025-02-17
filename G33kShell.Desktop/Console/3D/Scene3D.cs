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
    private readonly List<SceneObject> m_objects = new();

    public Scene3D([NotNull] ScreenData screen, [NotNull] SceneBackground sceneBackground)
    {
        m_screen = screen ?? throw new ArgumentNullException(nameof(screen));
        m_sceneBackground = sceneBackground ?? throw new ArgumentNullException(nameof(sceneBackground));
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
        
        foreach (var obj in m_objects.OrderByDescending(o => o.GetTransformedVertices().Average(v => v.Z)))
        {
            var vertices = obj.GetTransformedVertices().ToArray();
            foreach (var face in obj.Faces)
                FillTriangle(vertices[face.i0], vertices[face.i1], vertices[face.i2], face.material);
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
            v.Y
        ) * perspectiveFactor;
    }

    /// <summary>
    /// Fills a triangle on the screen using character-based rendering.
    /// </summary>
    private void FillTriangle(Vector3 v0, Vector3 v1, Vector3 v2, Attr material)
    {
        // Project points into the screen space.
        var majorAxis = Math.Min(m_screen.Width, m_screen.Height) / 2.0f;
        var worldToScreen = new Vector2(m_screen.Width / 2.0f, m_screen.Height / 2.0f);
        var p0 = GetProjectedXy(v0) * majorAxis + worldToScreen;
        var p1 = GetProjectedXy(v1) * majorAxis + worldToScreen;
        var p2 = GetProjectedXy(v2) * majorAxis + worldToScreen;

        // Bail early if triangle is completely off the screen.
        var left = MathF.Min(MathF.Min(p0.X, p1.X), p2.X);
        var right = MathF.Max(MathF.Max(p0.X, p1.X), p2.X);
        var top = MathF.Min(MathF.Min(p0.Y, p1.Y), p2.Y);
        var bottom = MathF.Max(MathF.Max(p0.Y, p1.Y), p2.Y);
        if (right < 0 || left >= m_screen.Width || bottom < 0 || top >= m_screen.Height)
            return;

        // Crop the triangle to the screen.
        left = MathF.Max(left, 0.0f);
        right = MathF.Min(right, m_screen.Width - 1.0f);
        top = MathF.Max(top, 0.0f);
        bottom = MathF.Min(bottom, m_screen.Height - 1.0f);

        // Triangle area (for barycentric weights)
        var areaInv = 1.0f / (p1 - p0).Cross(p2 - p0);

        // Draw each triangle point.
        for (var y = (int)top; y <= (int)bottom; y++)
        {
            for (var x = (int)left; x <= (int)right; x++)
            {
                var p = new Vector2(x, y);
                var edge0 = p1 - p;
                var edge1 = p2 - p;
                var edge2 = p0 - p;

                // Compute barycentric coordinates
                var w0 = edge0.Cross(edge1) * areaInv;
                var w1 = edge1.Cross(edge2) * areaInv;
                var w2 = 1.0f - w0 - w1;

                if (w0 >= 0 && w1 >= 0 && w2 >= 0)
                    m_screen.Chars[y][x].Set(material);
            }
        }
    }
}