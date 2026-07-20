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
using System.Numerics;
using DTC.Core;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// Rotating wireframe spheres and cylinders inspired by the computer display in Weird Science.
/// </summary>
[UsedImplicitly]
public class WeirdScienceCanvas : PixelScreensaverBase
{
    private const int PixelWidth = 320;
    private const int PixelHeight = 240;
    private const int SceneFps = 30;
    private const double HoldSeconds = 6.0;
    private const double FadeSeconds = 2.0;
    private const double CycleSeconds = HoldSeconds + FadeSeconds;
    private const int ShapeCount = 3;
    private const float SphereRadius = 1.0f;
    private const float CylinderRadius = SphereRadius / 5.0f;
    private const float TriangleSide = 5.0f;
    private const float FocalLength = 295.0f;
    private const float TranslationSpeed = 1.44f;
    private const float NearDepth = 8.5f;
    private const float FarDepth = 40.0f;
    private const float FarBrightness = 0.20f;
    private const float SpawnJitterX = 18.0f;
    private const float SpawnJitterY = 12.0f;
    private const int PaletteSize = 64;
    private const byte SphereShade = PaletteSize - 1;
    private const byte CylinderShade = 29;
    private const int SphereLongitudes = 8;
    private const int SphereLatitudes = 5;
    private const int SphereRingSegments = 20;
    private const int SphereMeridianSegments = 12;
    private const int CylinderSides = 8;
    private const int CylinderRings = 5;
    private const int CylinderRingSegments = 12;

    private static readonly Rgb[] s_basePalette = CreateBasePalette();
    private static readonly Vector3[] s_sphereCenters =
    [
        new Vector3(0.0f, 2.0f * MathF.Sqrt(3.0f), 0.0f),
        new Vector3(-TriangleSide * 0.5f, -MathF.Sqrt(3.0f), 0.0f),
        new Vector3(TriangleSide * 0.5f, -MathF.Sqrt(3.0f), 0.0f)
    ];
    private static readonly (int Start, int End)[] s_cylinderConnections = [(0, 1), (1, 2), (2, 0)];
    private static readonly Vector2[] s_shapeScreenOffsets =
    [
        new Vector2(-85.0f, 30.0f),
        new Vector2(0.0f, -45.0f),
        new Vector2(85.0f, 30.0f)
    ];

    private readonly List<WireSegment> m_segments = new List<WireSegment>(1800);
    private readonly Shape[] m_shapes = new Shape[ShapeCount];
    private Shape m_nextShape;
    private int m_transitionShapeIndex;
    private double m_cycleTime;

    public WeirdScienceCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, targetFps: SceneFps)
    {
        Name = "weirdscience";
    }

    public override void StartScreensaver(ScreenData shellScreen)
    {
        base.StartScreensaver(shellScreen);
        if (WindowManager == null)
            return;

        ResetScene();
        StartPixelScreen(PixelWidth, PixelHeight);
    }

    public override void UpdateFrame(ScreenData screen)
    {
        ClearTextOverlay(screen);
        if (!IsPixelScreenActive || PixelScreen == null)
            return;

        AdvanceScene(1.0 / SceneFps);
        using (PixelScreen.Lock(out var pixels))
            DrawScene(pixels);
    }

    protected override Rgb[] GetPixelPalette()
    {
        var foreground = Foreground ?? Rgb.White;
        var background = Background ?? Rgb.Black;
        return PixelScreenData.CreateGreyscalePalette(s_basePalette, background, foreground);
    }

    private void ResetScene()
    {
        m_cycleTime = 0.0;
        m_transitionShapeIndex = 0;
        for (var i = 0; i < m_shapes.Length; i++)
            m_shapes[i] = Shape.CreateRandom(i);
        m_nextShape = Shape.CreateRandom(m_transitionShapeIndex);
    }

    private void AdvanceScene(double elapsedSeconds)
    {
        m_cycleTime += elapsedSeconds;
        foreach (var shape in m_shapes)
            shape.Advance(elapsedSeconds);
        m_nextShape.Advance(elapsedSeconds);

        if (m_cycleTime < CycleSeconds)
            return;

        m_cycleTime %= CycleSeconds;
        m_shapes[m_transitionShapeIndex] = m_nextShape;
        m_transitionShapeIndex = (m_transitionShapeIndex + 1) % m_shapes.Length;
        m_nextShape = Shape.CreateRandom(m_transitionShapeIndex);
    }

    private void DrawScene(PixelScreenData screen)
    {
        screen.Clear();
        m_segments.Clear();

        var fadeAmount = Math.Clamp((m_cycleTime - HoldSeconds) / FadeSeconds, 0.0, 1.0);
        for (var i = 0; i < m_shapes.Length; i++)
            AddShape(m_shapes[i], i == m_transitionShapeIndex ? 1.0 - fadeAmount : 1.0);
        AddShape(m_nextShape, fadeAmount);

        // The wireframe has no solid surfaces, so depth-sorted line fragments give the intended
        // movie-era painter's-algorithm look at crossings and during the overlapping crossfade.
        m_segments.Sort(static (a, b) => b.Depth.CompareTo(a.Depth));
        foreach (var segment in m_segments)
        {
            var a = Project(segment.Start);
            var b = Project(segment.End);
            if (!a.IsVisible || !b.IsVisible)
                continue;
            screen.DrawAntialiasedLine(a.X, a.Y, b.X, b.Y, ApplyDepthShading(segment));
        }
    }

    private static byte ApplyDepthShading(WireSegment segment)
    {
        var depthAmount = Math.Clamp((segment.Depth - NearDepth) / (FarDepth - NearDepth), 0.0f, 1.0f);
        var brightness = 1.0f - depthAmount * (1.0f - FarBrightness);
        return (byte)Math.Clamp((int)MathF.Round(segment.Shade * brightness), 1, segment.Shade);
    }

    private void AddShape(Shape shape, double opacity)
    {
        if (opacity <= 0.0)
            return;

        var sphereShade = (byte)Math.Clamp((int)Math.Round(SphereShade * opacity), 1, SphereShade);
        var cylinderShade = (byte)Math.Clamp((int)Math.Round(CylinderShade * opacity), 1, CylinderShade);
        var rotation = shape.Rotation;
        foreach (var sphere in s_sphereCenters)
            AddSphere(sphere, rotation, shape.Center, sphereShade);

        foreach (var (start, end) in s_cylinderConnections)
            AddCylinder(s_sphereCenters[start], s_sphereCenters[end], rotation, shape.Center, cylinderShade);
    }

    private void AddSphere(Vector3 center, Quaternion rotation, Vector3 translation, byte shade)
    {
        for (var latitudeIndex = 1; latitudeIndex <= SphereLatitudes; latitudeIndex++)
        {
            var latitude = -MathF.PI * 0.5f + MathF.PI * latitudeIndex / (SphereLatitudes + 1);
            var ringRadius = MathF.Cos(latitude) * SphereRadius;
            var y = MathF.Sin(latitude) * SphereRadius;
            for (var segment = 0; segment < SphereRingSegments; segment++)
            {
                var angleA = MathF.Tau * segment / SphereRingSegments;
                var angleB = MathF.Tau * (segment + 1) / SphereRingSegments;
                var a = center + new Vector3(MathF.Cos(angleA) * ringRadius, y, MathF.Sin(angleA) * ringRadius);
                var b = center + new Vector3(MathF.Cos(angleB) * ringRadius, y, MathF.Sin(angleB) * ringRadius);
                AddSegment(Transform(a, rotation, translation), Transform(b, rotation, translation), shade);
            }
        }

        for (var longitudeIndex = 0; longitudeIndex < SphereLongitudes; longitudeIndex++)
        {
            var longitude = MathF.Tau * longitudeIndex / SphereLongitudes;
            for (var segment = 0; segment < SphereMeridianSegments; segment++)
            {
                var latitudeA = -MathF.PI * 0.5f + MathF.PI * segment / SphereMeridianSegments;
                var latitudeB = -MathF.PI * 0.5f + MathF.PI * (segment + 1) / SphereMeridianSegments;
                var a = SpherePoint(center, latitudeA, longitude);
                var b = SpherePoint(center, latitudeB, longitude);
                AddSegment(Transform(a, rotation, translation), Transform(b, rotation, translation), shade);
            }
        }
    }

    private void AddCylinder(Vector3 centerA, Vector3 centerB, Quaternion rotation, Vector3 translation, byte shade)
    {
        var axis = Vector3.Normalize(centerB - centerA);
        var start = centerA + axis * SphereRadius;
        var end = centerB - axis * SphereRadius;
        var reference = Math.Abs(Vector3.Dot(axis, Vector3.UnitZ)) < 0.9f ? Vector3.UnitZ : Vector3.UnitY;
        var sideA = Vector3.Normalize(Vector3.Cross(axis, reference));
        var sideB = Vector3.Normalize(Vector3.Cross(axis, sideA));

        for (var side = 0; side < CylinderSides; side++)
        {
            var angle = MathF.Tau * side / CylinderSides;
            var radial = CylinderRadius * (sideA * MathF.Cos(angle) + sideB * MathF.Sin(angle));
            AddSegment(
                Transform(start + radial, rotation, translation),
                Transform(end + radial, rotation, translation),
                shade);
        }

        for (var ring = 0; ring < CylinderRings; ring++)
        {
            var amount = ring / (float)(CylinderRings - 1);
            var ringCenter = Vector3.Lerp(start, end, amount);
            for (var segment = 0; segment < CylinderRingSegments; segment++)
            {
                var angleA = MathF.Tau * segment / CylinderRingSegments;
                var angleB = MathF.Tau * (segment + 1) / CylinderRingSegments;
                var a = ringCenter + CylinderRadius * (sideA * MathF.Cos(angleA) + sideB * MathF.Sin(angleA));
                var b = ringCenter + CylinderRadius * (sideA * MathF.Cos(angleB) + sideB * MathF.Sin(angleB));
                AddSegment(Transform(a, rotation, translation), Transform(b, rotation, translation), shade);
            }
        }
    }

    private void AddSegment(Vector3 start, Vector3 end, byte shade) =>
        m_segments.Add(new WireSegment(start, end, (start.Z + end.Z) * 0.5f, shade));

    private static Vector3 SpherePoint(Vector3 center, float latitude, float longitude) =>
        center + SphereRadius * new Vector3(
            MathF.Cos(latitude) * MathF.Cos(longitude),
            MathF.Sin(latitude),
            MathF.Cos(latitude) * MathF.Sin(longitude));

    private static Vector3 Transform(Vector3 point, Quaternion rotation, Vector3 translation) =>
        Vector3.Transform(point, rotation) + translation;

    private static ScreenPoint Project(Vector3 point)
    {
        if (point.Z <= 1.0f)
            return default;

        var scale = FocalLength / point.Z;
        return new ScreenPoint(
            PixelWidth * 0.5 + point.X * scale,
            PixelHeight * 0.5 - point.Y * scale,
            true);
    }

    private static Rgb[] CreateBasePalette()
    {
        var palette = new Rgb[PaletteSize];
        for (var i = 0; i < palette.Length; i++)
        {
            var value = (byte)Math.Round(255.0 * i / (palette.Length - 1));
            palette[i] = new Rgb(value, value, value);
        }
        return palette;
    }

    private readonly record struct WireSegment(Vector3 Start, Vector3 End, float Depth, byte Shade);
    private readonly record struct ScreenPoint(double X, double Y, bool IsVisible);

    private sealed class Shape
    {
        private readonly Vector3 m_rotationAxis;
        private readonly float m_rotationSpeed;
        private readonly Vector3 m_velocity;
        private float m_rotationAngle;

        private Shape(
            Vector3 center,
            Vector3 velocity,
            Vector3 rotationAxis,
            float rotationSpeed,
            float rotationAngle)
        {
            Center = center;
            m_velocity = velocity;
            m_rotationAxis = rotationAxis;
            m_rotationSpeed = rotationSpeed;
            m_rotationAngle = rotationAngle;
        }

        public Vector3 Center { get; private set; }
        public Quaternion Rotation => Quaternion.CreateFromAxisAngle(m_rotationAxis, m_rotationAngle);

        public void Advance(double elapsedSeconds)
        {
            Center += m_velocity * (float)elapsedSeconds;
            m_rotationAngle += m_rotationSpeed * (float)elapsedSeconds;
        }

        public static Shape CreateRandom(int regionIndex)
        {
            var axis = RandomVector(-1.0f, 1.0f);
            if (axis.LengthSquared() < 0.01f)
                axis = new Vector3(0.4f, 0.8f, 0.3f);

            var zVelocity = TranslationSpeed * RandomSignedFloat(0.30f, 0.65f);
            var maximumLifetime = (ShapeCount + 1) * CycleSeconds;
            var depthTravel = MathF.Abs(zVelocity) * (float)maximumLifetime;
            var z = zVelocity < 0.0f
                ? RandomFloat(NearDepth + depthTravel, FarDepth)
                : RandomFloat(NearDepth, FarDepth - depthTravel);
            var screenOffset = s_shapeScreenOffsets[regionIndex] + new Vector2(
                RandomFloat(-SpawnJitterX, SpawnJitterX),
                RandomFloat(-SpawnJitterY, SpawnJitterY));
            var center = new Vector3(
                screenOffset.X * z / FocalLength,
                -screenOffset.Y * z / FocalLength,
                z);
            var target = new Vector2(
                RandomFloat(-1.5f, 1.5f),
                RandomFloat(-1.0f, 1.0f));
            var toTarget = target - new Vector2(center.X, center.Y);
            var horizontalVelocity = Vector2.Normalize(toTarget) *
                                     (TranslationSpeed * RandomFloat(0.35f, 0.55f));

            return new Shape(
                center,
                new Vector3(horizontalVelocity.X, horizontalVelocity.Y, zVelocity),
                Vector3.Normalize(axis),
                RandomFloat(0.35f, 0.65f) * (Random.Shared.Next(2) == 0 ? -1.0f : 1.0f),
                RandomFloat(0.0f, MathF.Tau));
        }

        private static Vector3 RandomVector(float min, float max) =>
            new Vector3(RandomFloat(min, max), RandomFloat(min, max), RandomFloat(min, max));

        private static float RandomFloat(float min, float max) =>
            min + (float)Random.Shared.NextDouble() * (max - min);

        private static float RandomSignedFloat(float minMagnitude, float maxMagnitude) =>
            RandomFloat(minMagnitude, maxMagnitude) * (Random.Shared.Next(2) == 0 ? -1.0f : 1.0f);
    }
}
