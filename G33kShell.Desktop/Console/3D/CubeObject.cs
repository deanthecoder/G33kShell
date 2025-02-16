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
    // todo Document the constructor args
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CubeObject"/> class which represents a 3D cube in a scene.
    /// </summary>
    /// <param name="radius">The distance from the cube's center to any of its vertices.</param>
    /// <param name="faces">An array of six <see cref="Attr"/> objects, each describing the appearance of one face of the cube.</param>
    public CubeObject(float radius, Attr[] faces) : base(CreateVertices(radius), CreateFaces(faces))
    {
    }

    private static IEnumerable<Vector3> CreateVertices(float radius) =>
    [
        new Vector3(-radius, -radius, -radius), // 0: Bottom-left-back
        new Vector3(radius, -radius, -radius),  // 1: Bottom-right-back
        new Vector3(radius, radius, -radius),   // 2: Top-right-back
        new Vector3(-radius, radius, -radius),  // 3: Top-left-back
        new Vector3(-radius, -radius, radius),  // 4: Bottom-left-front
        new Vector3(radius, -radius, radius),   // 5: Bottom-right-front
        new Vector3(radius, radius, radius),    // 6: Top-right-front
        new Vector3(-radius, radius, radius)    // 7: Top-left-front
    ];

    private static IEnumerable<(int, int, int, Attr)> CreateFaces(Attr[] faces) =>
    [
        // Font face
        (4, 5, 6, faces[0]),
        (4, 6, 7, faces[0]),

        // Back face
        (0, 2, 1, faces[1]),
        (0, 3, 2, faces[1]),

        // Left face
        (0, 4, 7, faces[2]),
        (0, 7, 3, faces[2]),

        // Right face
        (1, 2, 6, faces[3]),
        (1, 6, 5, faces[3]),

        // Top face
        (3, 7, 6, faces[4]),
        (3, 6, 2, faces[4]),

        // Bottom face
        (0, 1, 5, faces[5]),
        (0, 5, 4, faces[5])
    ];
}