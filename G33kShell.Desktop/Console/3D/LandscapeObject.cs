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
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console._3D;

/// <summary>
/// A 3D landscape that can be added to a <see cref="Scene3D"/>.
/// </summary>
public class LandscapeObject : SceneObject
{
    private readonly Func<float, Vector3, float> m_heightFunc;
    private readonly Func<Vector3, Attr> m_materialFunc;

    /// <summary>
    /// Initializes a new instance of the <see cref="LandscapeObject"/> class, which represents a 3D terrain mesh in a scene.
    /// </summary>
    /// <param name="gridSize">The number of points along each axis (e.g., 15x15).</param>
    /// <param name="spacing">The distance between grid points.</param>
    /// <param name="heightFunc">A function that returns the height (Y) given time and surface point.</param>
    /// <param name="materialFunc">A function that returns the material <see cref="Attr"/> for a given surface point.</param>
    public LandscapeObject(int gridSize, float spacing, [NotNull] Func<float, Vector3, float> heightFunc, [NotNull] Func<Vector3, Attr> materialFunc)
        : base(CreateVertices(gridSize, spacing), CreateFaces(gridSize))
    {
        m_heightFunc = heightFunc ?? throw new ArgumentNullException(nameof(heightFunc));
        m_materialFunc = materialFunc ?? throw new ArgumentNullException(nameof(materialFunc));
    }


    /// <summary>
    /// Updates the Y-coordinates of the object's vertices according to the height function. 
    /// This allows dynamic changes to the landscape's height map at runtime.
    /// </summary>
    public void Update(float time)
    {
        for (var i = 0; i < Vertices.Count; i++)
            Vertices[i] = new Vector3(Vertices[i].X, m_heightFunc(time, Vertices[i]), Vertices[i].Z);
        
        foreach (var face in Faces)
        {
            var vCenter = (Vertices[face.I0] + Vertices[face.I1] + Vertices[face.I2]) / 3.0f;
            face.Material = m_materialFunc(vCenter);
        }
    }

    private static List<Vector3> CreateVertices(int gridSize, float spacing)
    {
        List<Vector3> vertices = new();

        var cx = (gridSize - 1) * spacing / 2.0f;
        for (var z = 0; z < gridSize; z++)
        {
            for (var x = 0; x < gridSize; x++)
            {
                var worldX = x * spacing;
                var worldZ = z * spacing;
                vertices.Add(new Vector3(worldX - cx, 0, worldZ - cx));
            }
        }

        return vertices;
    }

    private static List<Face3D> CreateFaces(int gridSize)
    {
        List<Face3D> triangles = new();

        var defaultMaterial = new Attr('.');
        for (var z = 0; z < gridSize - 1; z++)
        {
            for (var x = 0; x < gridSize - 1; x++)
            {
                var v00 = x + z * gridSize;
                var v01 = x + (z + 1) * gridSize;
                var v10 = x + 1 + z * gridSize;
                var v11 = x + 1 + (z + 1) * gridSize;

                // First triangle
                triangles.Add(new Face3D(v00, v10, v11, defaultMaterial));

                // Second triangle
                triangles.Add(new Face3D(v00, v11, v01, defaultMaterial));
            }
        }

        return triangles;
    }
}