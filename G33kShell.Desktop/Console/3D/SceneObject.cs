using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace G33kShell.Desktop.Console._3D;

/// <summary>
/// Represents a 3D object within a scene. Contains vertices, faces, and transformation properties such as
/// position, rotation, and scale. It can be rendered onto a screen using <see cref="Scene3D"/>.
/// </summary>
public class SceneObject
{
    public Vector3[] Vertices { get; }
    public (int, int, int, Attr)[] Faces { get; }
    
    public Vector3 Rotation { get; set; }
    public Vector3 Position { get; set; }
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
            .Select(v => RotateVertex(v, Rotation))
            .Select(v => v * Scale)
            .Select(v => v + Position);

    /// <summary>
    /// Rotates a vertex by the specified rotation vector.
    /// </summary>
    private static Vector3 RotateVertex(Vector3 v, Vector3 r)
    {
        var cosX = MathF.Cos(r.X);
        var sinX = MathF.Sin(r.X);
        var cosY = MathF.Cos(r.Y);
        var sinY = MathF.Sin(r.Y);
        var cosZ = MathF.Cos(r.Z);
        var sinZ = MathF.Sin(r.Z);

        var y1 = v.Y * cosX - v.Z * sinX;
        var z1 = v.Y * sinX + v.Z * cosX;
        var x2 = v.X * cosY + z1 * sinY;
        var z2 = -v.X * sinY + z1 * cosY;
        var x3 = x2 * cosZ - y1 * sinZ;
        var y3 = x2 * sinZ + y1 * cosZ;

        return new Vector3(x3, y3, z2);
    }
}