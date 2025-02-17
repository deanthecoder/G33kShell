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

namespace G33kShell.Desktop.Console._3D;

/// <summary>
/// Represents a 3D object within a scene. Contains vertices, faces, and transformation properties such as
/// position, rotation, and scale. It can be rendered onto a screen using <see cref="Scene3D"/>.
/// </summary>
public class SceneObject
{
    public Vector3[] Vertices { get; }
    public (int i0, int i1, int i2, Attr material)[] Faces { get; }

    public Vector3 LocalPosition { get; set; } = Vector3.Zero;
    public Vector3 Rotation { get; set; } = Vector3.Zero;
    public Vector3 WorldPosition { get; set; } = Vector3.Zero;
    public float Scale { get; set; } = 1.0f;

    /// <summary>
    /// Initializes a new instance of a 3D object with specified vertices and faces.
    /// </summary>
    public SceneObject(IEnumerable<Vector3> vertices, IEnumerable<(int, int, int, Attr)> faces)
    {
        Vertices = vertices?.ToArray() ?? throw new ArgumentNullException(nameof(vertices));
        Faces = faces?.ToArray() ?? throw new ArgumentNullException(nameof(faces));
    }

    /// <summary>
    /// Retrieves the transformed vertices of the object after applying the current position, rotation,
    /// and scale. The transformations are applied based on the given time (e.g., for animations).
    /// </summary>
    public IEnumerable<Vector3> GetTransformedVertices() =>
        Vertices
            .Select(v => v + LocalPosition)
            .Select(v => v.Rotate(Rotation))
            .Select(v => v * Scale)
            .Select(v => v + WorldPosition);
}