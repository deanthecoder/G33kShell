// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Collections.Generic;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>Projects distance shading and ledge occlusion onto the captured arcade floor.</summary>
/// <remarks>
/// The geometry stays in world coordinates. An edge-aware blend lets the shading follow
/// visually continuous ground across foreground and background tiles.
/// </remarks>
internal sealed class TerrainShading
{
    private const int Columns = 256;
    private const int DepthCells = 64;
    private const int CellSize = 8;
    private const int ProjectedTop = -384;
    private const int ProjectedHeight = 1664;
    private const int BackEdgeReach = 12;
    private const int Width = ArcadeData.Width;
    private const int Height = ArcadeData.Height;
    private static readonly int[] s_weights = [1, 2, 3, 4, 3, 2, 1];

    private readonly byte[] m_worldShade = new byte[Columns * ProjectedHeight];
    private readonly byte[] m_worldContact = new byte[Columns * ProjectedHeight];
    private readonly byte[] m_similarity = new byte[256 * 256];
    private readonly byte[] m_projected = new byte[Width * Height];
    private readonly byte[] m_horizontal = new byte[Width * Height];
    private readonly byte[] m_mask = new byte[Width * Height];
    private (int X, int Y, int BackgroundX, int BackgroundY, byte[] Palette) m_cachedView;
    private int[] m_debugFrame;
    private int m_firstFloorColumn = Columns;
    private int m_lastFloorColumn = -1;

    public TerrainShading(ArcadeData data)
    {
        BuildTerrain(data.HeightMap);
        for (var i = 0; i < data.Palette.Length; i++)
        for (var j = 0; j < data.Palette.Length; j++)
        {
            var a = data.Palette[i];
            var b = data.Palette[j];
            var difference = Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
            m_similarity[i * 256 + j] = (byte)Math.Max(0, 24 - difference / 4);
        }
    }

    public void Draw(PixelScreenData screen, int x, int y, int backgroundX, int backgroundY, byte[] palette,
        byte opacity, bool debug, bool camp)
    {
        if (!camp)
        {
            var view = (x, y, backgroundX, backgroundY, palette);
            if (view != m_cachedView)
            {
                UpdateMask(screen.Pixels, x, y);
                m_cachedView = view;
            }
        }

        if (debug)
        {
            m_debugFrame ??= new int[m_mask.Length];
            for (var i = 0; i < m_mask.Length; i++)
            {
                var gray = camp ? 255 : 255 - Math.Min(255, 3 * m_mask[i]);
                m_debugFrame[i] = unchecked((int)0xff000000) | gray * 0x010101;
            }
            screen.SetBgraFrame(m_debugFrame);
        }
        else if (!camp && opacity != 0)
        {
            screen.SetTerrainShade(m_mask, opacity);
        }
    }

    private void BuildTerrain(byte[] heights)
    {
        var nearDepths = new List<int>();
        for (var column = 0; column < Columns; column++)
        {
            for (var cell = DepthCells - 1; cell >= 0; cell--)
            {
                if (!HasFloor(heights[column * DepthCells + cell]))
                    continue;
                m_firstFloorColumn = Math.Min(m_firstFloorColumn, column);
                m_lastFloorColumn = column;
                nearDepths.Add(cell * CellSize + CellSize - 1);
                break;
            }
        }
        if (nearDepths.Count == 0)
            return;
        nearDepths.Sort();
        // The usual foreground depth comes from this area, including unusually narrow stages.
        var nearDepth = nearDepths[nearDepths.Count / 2] - 8;
        var depthAtRow = new int[ProjectedHeight];
        var heightAtRow = new byte[ProjectedHeight];
        for (var column = m_firstFloorColumn; column <= m_lastFloorColumn; column++)
        {
            Array.Clear(heightAtRow);
            for (var depth = 0; depth < DepthCells * CellSize; depth++)
            {
                var ground = heights[column * DepthCells + depth / CellSize];
                if (!HasFloor(ground))
                    continue;
                var z = depth - 128;
                var row = z - (z >> 2) + ground * CellSize - 512 - ProjectedTop;
                depthAtRow[row] = depth;
                heightAtRow[row] = ground;
            }

            for (var row = 0; row < ProjectedHeight; row++)
            {
                var ground = heightAtRow[row];
                if (ground == 0)
                    continue;
                var depth = depthAtRow[row];
                var distance = Math.Clamp((nearDepth - depth) * 68 / 136, 0, 68);
                var (occlusion, contact) = LedgeOcclusion(heights, column * CellSize + CellSize / 2, depth, ground);

                // Taper towards a front-facing lip so the top's shade never makes a stripe down the face.
                var taper = 7;
                for (var offset = 1; offset < 7 && row + offset < ProjectedHeight; offset++)
                {
                    if (Math.Abs(heightAtRow[row + offset] - ground) <= 2)
                        continue;
                    taper = offset;
                    break;
                }
                var amount = distance + occlusion * (255 - distance) / 255;
                var index = column * ProjectedHeight + row;
                var lipFade = taper * taper * (21 - 2 * taper);
                m_worldShade[index] = (byte)(amount * lipFade / 343);
                m_worldContact[index] = (byte)(contact * lipFade / 343);
            }
        }
    }

    private static bool HasFloor(int height) => height is > 32 and < 208;

    private (int Ambient, int Contact) LedgeOcclusion(byte[] heights, int x, int depth, int ground)
    {
        var ambient = 0;
        var contact = 0;
        for (var distance = 4; distance <= 20; distance += 4)
        {
            Accumulate(x, depth - distance, distance);
            Accumulate(x - distance, depth, distance);
            Accumulate(x + distance, depth, distance);
        }
        return (ambient, contact);

        void Accumulate(int neighborX, int neighborDepth, int distance)
        {
            if (neighborX < m_firstFloorColumn * CellSize || neighborX >= (m_lastFloorColumn + 1) * CellSize ||
                (uint)neighborDepth >= DepthCells * CellSize)
                return;
            var neighbor = heights[(neighborX / CellSize) * DepthCells + neighborDepth / CellSize];
            var rise = ground - neighbor;
            if (rise < 3)
                return;

            var shelf = HasFloor(neighbor);
            ambient = Math.Max(ambient, (24 - distance) * (shelf ? 45 : 32) / 24);
            // A raised walkable shelf casts a tighter shadow onto the floor at its foot.
            // Let it fall off faster than the broad shade around the ledge.
            var reach = Math.Max(0, 16 - distance);
            if (shelf)
                contact = Math.Max(contact, Math.Clamp(rise - 3, 0, 6) * reach * reach / 18);
        }
    }

    private void UpdateMask(byte[] colors, int x, int y)
    {
        // Allow the outermost painted tiles and the camera's one-pixel margin.
        var worldLeft = (m_firstFloorColumn - 1) * CellSize - 1;
        var worldRight = (m_lastFloorColumn + 2) * CellSize + 1;
        for (var sy = 0; sy < Height; sy++)
        {
            var row = y + sy - ProjectedTop;
            for (var sx = 0; sx < Width; sx++)
            {
                var worldX = x + sx;
                var index = sy * Width + sx;
                var shade = 0;
                var contact = 0;
                if ((uint)row < ProjectedHeight && worldX >= worldLeft && worldX < worldRight)
                {
                    var worldIndex = Math.Clamp(worldX / CellSize, m_firstFloorColumn, m_lastFloorColumn) * ProjectedHeight + row;
                    shade = m_worldShade[worldIndex];
                    contact = m_worldContact[worldIndex];
                }
                // Only the broad shade is eligible to extend onto nearby background art.
                m_projected[index] = (byte)shade;
                m_mask[index] = (byte)(shade + contact * (255 - shade) / 255);
            }
        }
        ExtendBackEdges(colors);
        Blend(colors, m_mask, m_horizontal, across: true);
        Blend(colors, m_horizontal, m_mask, across: false);
    }

    private void ExtendBackEdges(byte[] colors)
    {
        for (var x = 0; x < Width; x++)
        {
            var floorY = Height;
            for (var y = Height - 1; y >= 0; y--)
            {
                var index = y * Width + x;
                if (m_projected[index] != 0)
                {
                    floorY = y;
                    continue;
                }

                var distance = floorY - y;
                if (floorY == Height || distance > BackEdgeReach)
                    continue;

                // Use the first few floor rows as color references. Keep the shade at
                // the join, then taper upwards through paint that matches the floor.
                var edgeShade = m_projected[floorY * Width + x];
                var paletteOffset = colors[index] * 256;
                var similarity = 0;
                for (var sampleY = floorY; sampleY <= floorY + 6 && sampleY < Height; sampleY += 2)
                {
                    var sample = sampleY * Width + x;
                    if (m_projected[sample] == 0)
                        break;
                    similarity = Math.Max(similarity, m_similarity[paletteOffset + colors[sample]]);
                }

                var coverage = Math.Clamp(similarity - 6, 0, 10);
                var taper = Math.Min(8, BackEdgeReach + 1 - distance);
                m_mask[index] = (byte)(edgeShade * coverage * taper / 80);
            }
        }
    }

    private void Blend(byte[] colors, byte[] source, byte[] destination, bool across)
    {
        var stride = across ? 1 : Width;
        var bound = across ? Width : Height;
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var index = y * Width + x;
            var coordinate = across ? x : y;
            var paletteOffset = colors[index] * 256;
            var total = source[index] * 96;
            var weightTotal = 96;
            for (var tap = 0; tap < s_weights.Length; tap++)
            {
                var offset = (tap - 3) * 2;
                if (offset == 0 || (uint)(coordinate + offset) >= bound)
                    continue;
                var neighbor = index + offset * stride;
                var weight = s_weights[tap] * m_similarity[paletteOffset + colors[neighbor]];
                total += weight * source[neighbor];
                weightTotal += weight;
            }
            destination[index] = (byte)(total / weightTotal);
        }
    }
}
