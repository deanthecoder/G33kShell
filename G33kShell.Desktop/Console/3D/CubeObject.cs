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
using System.Collections.Generic;
using System.Numerics;

namespace G33kShell.Desktop.Console._3D;

/// <summary>
/// A cube that can be added to a <see cref="Scene3D"/>.
/// </summary>
public class CubeObject : SceneObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CubeObject"/> class which represents a 3D cube in a scene.
    /// </summary>
    /// <param name="radius">The distance from the cube's center to any of its vertices.</param>
    /// <param name="materials">An array of six <see cref="Attr"/> objects, each describing the appearance of one face of the cube.</param>
    public CubeObject(float radius, Attr[] materials) : this(radius * 2.0f, radius * 2.0f, radius * 2.0f, materials)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CubeObject"/> class which represents a 3D cube in a scene.
    /// </summary>
    /// <param name="width">The width of the cube.</param>
    /// <param name="height">The height of the cube.</param>
    /// <param name="depth">The depth of the cube.</param>
    /// <param name="materials">An array of six <see cref="Attr"/> objects, each describing the appearance of one face of the cube.</param>
    public CubeObject(float width, float height, float depth, Attr[] materials) : base(CreateVertices(width, height, depth), CreateFaces(materials))
    {
    }

    private static IEnumerable<Vector3> CreateVertices(float width, float height, float depth)
    {
        width /= 2.0f;
        height /= 2.0f;
        depth /= 2.0f;
        return
        [
            new Vector3(-width, -height, -depth), // 0: Bottom-left-back
            new Vector3(width, -height, -depth),  // 1: Bottom-right-back
            new Vector3(width, height, -depth),   // 2: Top-right-back
            new Vector3(-width, height, -depth),  // 3: Top-left-back
            new Vector3(-width, -height, depth),  // 4: Bottom-left-front
            new Vector3(width, -height, depth),   // 5: Bottom-right-front
            new Vector3(width, height, depth),    // 6: Top-right-front
            new Vector3(-width, height, depth)    // 7: Top-left-front
        ];
    }

    private static IEnumerable<Face3D> CreateFaces(Attr[] materials) =>
    [
        // Font face
        new Face3D(4, 5, 6, materials[0]),
        new Face3D(4, 6, 7, materials[0]),

        // Back face
        new Face3D(0, 2, 1, materials[1]),
        new Face3D(0, 3, 2, materials[1]),

        // Left face
        new Face3D(0, 4, 7, materials[2]),
        new Face3D(0, 7, 3, materials[2]),

        // Right face
        new Face3D(1, 2, 6, materials[3]),
        new Face3D(1, 6, 5, materials[3]),

        // Top face
        new Face3D(3, 7, 6, materials[4]),
        new Face3D(3, 6, 2, materials[4]),

        // Bottom face
        new Face3D(0, 1, 5, materials[5]),
        new Face3D(0, 5, 4, materials[5])
    ];
}