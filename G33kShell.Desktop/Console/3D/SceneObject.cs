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
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console._3D;

/// <summary>
/// Represents a 3D object within a scene. Contains vertices, faces, and transformation properties such as
/// position, rotation, and scale. It can be rendered onto a screen using <see cref="Scene3D"/>.
/// </summary>
public class SceneObject
{
    public List<Vector3> Vertices { get; } = [];
    public List<Face3D> Faces { get; } = [];

    /// <summary>
    /// Note: Operations appended to the transform chain occur sooner.
    /// </summary>
    public Matrix4x4 Transform { get; set; } = Matrix4x4.Identity;

    /// <summary>
    /// Initializes a new instance of a 3D object with specified vertices and faces.
    /// </summary>
    public SceneObject(IEnumerable<Vector3> vertices, IEnumerable<Face3D> faces) : this()
    {
        Add(vertices, faces);
    }

    public SceneObject()
    {
    }

    public SceneObject Add(IEnumerable<Vector3> vertices, IEnumerable<Face3D> faces)
    {
        var startIndex = Vertices.Count;
        Vertices.AddRange(vertices);
        Faces.AddRange(faces.Select(o => new Face3D(o.I0 + startIndex, o.I1 + startIndex, o.I2 + startIndex, o.Material)));
        return this;
    }

    public void Add(SceneObject other, Matrix4x4? transform = null)
    {
        transform ??= Matrix4x4.Identity;
        
        Add(other.Vertices.Select(v => Vector3.Transform(v, (Matrix4x4)transform)), other.Faces);
    }
}

public class Face3D
{
    public int I0 { get; }
    public int I1 { get; }
    public int I2 { get; }
    public Attr Material { get; set; }

    public Face3D(int i0, int i1, int i2, [NotNull] Attr material)
    {
        I0 = i0;
        I1 = i1;
        I2 = i2;
        Material = material ?? throw new ArgumentNullException(nameof(material));
    }
}
