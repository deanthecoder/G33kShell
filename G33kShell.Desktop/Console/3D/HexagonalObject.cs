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
using System.Collections.Generic;
using System.Numerics;

namespace G33kShell.Desktop.Console._3D;

/// <summary>
/// A hexagonal prism that can be added to a <see cref="Scene3D"/>.
/// </summary>
public class HexagonalObject : SceneObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HexagonalObject"/> class which represents a 3D hexagonal prism in a scene.
    /// </summary>
    /// <param name="radius">The radius of the hexagonal base (distance from center to a vertex).</param>
    /// <param name="height">The height of the hexagonal prism.</param>
    /// <param name="materials">An array of eight <see cref="Attr"/> objects, each describing the appearance of one face of the hexagon (6 sides + 2 caps).</param>
    public HexagonalObject(float radius, float height, Attr[] materials)
        : base(CreateVertices(radius, height), CreateFaces(materials))
    {
    }

    private static IEnumerable<Vector3> CreateVertices(float radius, float height)
    {
        List<Vector3> vertices = new();

        for (var i = 0; i < 2; i++)
        {
            // Center point.
            vertices.Add(new Vector3(0, height / 2, 0));
            
            for (var j = 0; j < 6; j++)
            {
                var angle = j * MathF.PI / 3.0f; // 60-degree increments
                var x = radius * MathF.Cos(angle);
                var z = radius * MathF.Sin(angle);

                vertices.Add(new Vector3(x, height / 2 * (i == 0 ? 1 : -1), z));
            }
        }

        return vertices;
    }

    private static IEnumerable<(int, int, int, Attr)> CreateFaces(Attr[] materials)
    {
        List<(int, int, int, Attr)> triangles = new();

        // Top hexagon (triangle fan)
        for (var i = 0; i < 6; i++)
        {
            var next = (i + 1) % 6 + 1; // Ensure wraparound
            triangles.Add((0, i + 1, next, materials[0]));
        }

        // Bottom hexagon (triangle fan)
        for (var i = 0; i < 6; i++)
        {
            var next = (i + 1) % 6 + 8; // Ensure wraparound
            triangles.Add((7, next, i + 8, materials[1]));
        }

        // Side faces (connecting edges)
        for (var i = 0; i < 6; i++)
        {
            var topA = i + 1;
            var topB = (i + 1) % 6 + 1;
            var bottomA = i + 8;
            var bottomB = (i + 1) % 6 + 8;

            // Two triangles per side
            triangles.Add((topA, bottomA, bottomB, materials[2 + i])); // First triangle
            triangles.Add((topA, bottomB, topB, materials[2 + i]));    // Second triangle
        }

        return triangles;
    }
}