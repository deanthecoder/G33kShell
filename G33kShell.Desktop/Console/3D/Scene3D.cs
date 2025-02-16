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

        foreach (var obj in m_objects.OrderByDescending(o => o.Position.Z))
        {
            var vertices =
                obj
                    .GetTransformedVertices()
                    .Select(ProjectVertex)
                    .ToArray();
            foreach (var (i0, i1, i2, ch) in obj.Faces)
                FillTriangle(vertices[i0], vertices[i1], vertices[i2], ch);
        }
    }

    /// <summary>
    /// Prepares a 3D vertex for display onto a 2D plane using a simple perspective transformation.
    /// </summary>
    private Vector3 ProjectVertex(Vector3 v)
    {
        var perspectiveFactor = 5.0f / (5.0f + v.Z);
        return new Vector3(
            v.X * perspectiveFactor * ((float)m_screen.Width / m_screen.Height),
            v.Y * perspectiveFactor,
            v.Z
        );
    }

    /// <summary>
    /// Fills a triangle on the screen using character-based rendering.
    /// </summary>
    private void FillTriangle(Vector3 v0, Vector3 v1, Vector3 v2, Attr fillChar)
    {
        var majorAxis = Math.Min(m_screen.Width, m_screen.Height) / 2.0f;
        var p0 = new Vector2(v0.X, v0.Y) * new Vector2(majorAxis) + new Vector2(m_screen.Width / 2.0f, m_screen.Height / 2.0f);
        var p1 = new Vector2(v1.X, v1.Y) * new Vector2(majorAxis) + new Vector2(m_screen.Width / 2.0f, m_screen.Height / 2.0f);
        var p2 = new Vector2(v2.X, v2.Y) * new Vector2(majorAxis) + new Vector2(m_screen.Width / 2.0f, m_screen.Height / 2.0f);

        var left = MathF.Min(MathF.Min(p0.X, p1.X), p2.X);
        var right = MathF.Max(MathF.Max(p0.X, p1.X), p2.X);
        var top = MathF.Min(MathF.Min(p0.Y, p1.Y), p2.Y);
        var bottom = MathF.Max(MathF.Max(p0.Y, p1.Y), p2.Y);

        left = MathF.Max(left, 0.0f);
        right = MathF.Min(right, m_screen.Width - 1.0f);
        top = MathF.Max(top, 0.0f);
        bottom = MathF.Min(bottom, m_screen.Height - 1.0f);

        for (var y = (int)top; y <= (int)bottom; y++)
        {
            for (var x = (int)left; x <= (int)right; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);

                var edge1 = (p1 - p0).Cross(p - p0);
                if (edge1 > 0.0f) continue;

                var edge2 = (p2 - p1).Cross(p - p1);
                if (edge2 > 0.0f) continue;

                var edge3 = (p0 - p2).Cross(p - p2);
                if (edge3 > 0.0f) continue;

                m_screen.PrintAt(x, y, fillChar);
                m_screen.SetForeground(x, y, m_sceneBackground.Foreground);
            }
        }
    }
}