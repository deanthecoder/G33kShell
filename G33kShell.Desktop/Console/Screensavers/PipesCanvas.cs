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
using G33kShell.Desktop.Console.Controls;
using G33kShell.Desktop.Skins;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// Low-resolution, monochrome take on the classic 3D pipes screensaver.
/// </summary>
[UsedImplicitly]
public class PipesCanvas : ScreensaverBase
{
    private const int PixelWidth = 160;
    private const int PixelHeight = 120;
    private const int GridWidth = 22;
    private const int GridHeight = 16;
    private const int GridDepth = 18;
    private const int FramesPerSegment = 4;
    private const int SimultaneousPipes = 2;
    private const int MinStraightSegments = 4;
    private const double ContinueStraightChance = 0.84;
    private const int MaxPipes = 10;
    private const int SceneFps = 45;
    private const int SceneDurationFrames = 45 * SceneFps;
    private const int ResetDelayFrames = 60;
    private const float WorldSpacing = 1.15f;
    private const float FocalLength = 240.0f;
    private const double TubeRadius = 2.5;
    private const double JointRadius = 3.7;

    private static readonly Vector3 s_sceneCenter =
        new Vector3((GridWidth - 1) * 0.5f, (GridHeight - 1) * 0.5f, (GridDepth - 1) * 0.5f);
    private static readonly Vector3 s_cameraPosition = s_sceneCenter + new Vector3(24.0f, 20.0f, 27.0f);
    private static readonly Vector3 s_cameraForward = Vector3.Normalize(s_sceneCenter - s_cameraPosition);
    private static readonly Vector3 s_cameraRight = Vector3.Normalize(Vector3.Cross(s_cameraForward, Vector3.UnitY));
    private static readonly Vector3 s_cameraUp = Vector3.Normalize(Vector3.Cross(s_cameraRight, s_cameraForward));
    private static readonly float s_referenceDepth = Vector3.Distance(s_cameraPosition, s_sceneCenter);

    private static readonly Rgb[] s_basePalette =
    [
        new Rgb(0, 0, 0),
        new Rgb(32, 32, 32),
        new Rgb(68, 68, 68),
        new Rgb(100, 100, 100),
        new Rgb(136, 136, 136),
        new Rgb(174, 174, 174),
        new Rgb(214, 214, 214),
        new Rgb(255, 255, 255)
    ];

    private static readonly GridPoint[] s_directions =
    [
        new GridPoint(1, 0, 0),
        new GridPoint(-1, 0, 0),
        new GridPoint(0, 1, 0),
        new GridPoint(0, -1, 0),
        new GridPoint(0, 0, 1),
        new GridPoint(0, 0, -1)
    ];

    private readonly bool[,,] m_occupied = new bool[GridWidth, GridHeight, GridDepth];
    private readonly double[] m_depthBuffer = new double[PixelWidth * PixelHeight];
    private readonly List<PipeSegment> m_segments = [];
    private readonly List<PipeJoint> m_joints = [];
    private readonly List<PipeHead> m_activePipes = new List<PipeHead>(SimultaneousPipes);
    private readonly List<GridPoint> m_availableDirections = new List<GridPoint>(6);

    private WindowManager m_windowManager;
    private PixelScreenDataLock m_pixelScreen;
    private bool m_isActive;
    private int m_startedPipeCount;
    private int m_sceneFrame;
    private int m_resetFrames;

    public PipesCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, targetFps: SceneFps)
    {
        Name = "pipes";
    }

    public override void OnLoaded(WindowManager windowManager)
    {
        m_windowManager = windowManager;
        base.OnLoaded(windowManager);
    }

    protected override void OnUnloaded()
    {
        base.OnUnloaded();
        ClearPixelScreen();
        m_windowManager = null;
    }

    public override void OnSkinChanged(SkinBase skin)
    {
        base.OnSkinChanged(skin);
        if (!m_isActive || m_pixelScreen == null)
            return;

        using (m_pixelScreen.Lock(out var pixels))
            pixels.SetPalette(GetPalette());
    }

    public override void StartScreensaver(ScreenData shellScreen)
    {
        base.StartScreensaver(shellScreen);
        if (m_windowManager == null)
            return;

        ResetScene();
        m_isActive = true;
        m_pixelScreen = m_windowManager.SetPixelScreen(PixelWidth, PixelHeight, GetPalette());
    }

    public override void StopScreensaver()
    {
        base.StopScreensaver();
        ClearPixelScreen();
    }

    public override void BuildScreen(ScreenData screen) =>
        ClearTextOverlay(screen);

    public override void UpdateFrame(ScreenData screen)
    {
        ClearTextOverlay(screen);
        if (!m_isActive || m_pixelScreen == null)
            return;

        AdvanceGrowth();
        using (m_pixelScreen.Lock(out var pixels))
        {
            pixels.Clear();
            DrawScene(pixels);
        }
    }

    private void AdvanceGrowth()
    {
        if (m_resetFrames > 0)
        {
            if (--m_resetFrames == 0)
                ResetScene();
            return;
        }

        if (++m_sceneFrame >= SceneDurationFrames)
        {
            m_resetFrames = ResetDelayFrames;
            return;
        }

        for (var i = m_activePipes.Count - 1; i >= 0; i--)
        {
            var pipe = m_activePipes[i];
            if (!pipe.HasTarget)
            {
                if (!ChooseNextTarget(pipe))
                {
                    FinishPipe(pipe);
                    m_activePipes.RemoveAt(i);
                }
                continue;
            }

            pipe.SegmentFrame++;
            if (pipe.SegmentFrame < FramesPerSegment)
                continue;

            AddCompletedSegment(pipe);
            pipe.Head = pipe.Target;
            pipe.SegmentFrame = 0;
            pipe.HasTarget = false;
        }

        StartAvailablePipes();
        if (m_activePipes.Count == 0)
            m_resetFrames = ResetDelayFrames;
    }

    private void AddCompletedSegment(PipeHead pipe)
    {
        if (pipe.LastSegmentIndex >= 0)
        {
            var previous = m_segments[pipe.LastSegmentIndex];
            if (previous.End == pipe.Head && GetDirection(previous) == pipe.Direction)
            {
                m_segments[pipe.LastSegmentIndex] = previous with { End = pipe.Target };
                return;
            }
        }

        m_segments.Add(new PipeSegment(pipe.Head, pipe.Target, pipe.ShadeOffset));
        pipe.LastSegmentIndex = m_segments.Count - 1;
    }

    private bool ChooseNextTarget(PipeHead pipe)
    {
        FindAvailableDirections(pipe.Head);
        if (m_availableDirections.Count == 0)
            return false;

        GridPoint nextDirection;
        var canContinueStraight = m_availableDirections.Contains(pipe.Direction);
        if (canContinueStraight &&
            (pipe.StraightSegments < MinStraightSegments || Random.Shared.NextDouble() < ContinueStraightChance))
        {
            nextDirection = pipe.Direction;
        }
        else
        {
            nextDirection = m_availableDirections[Random.Shared.Next(m_availableDirections.Count)];
        }

        if (pipe.Direction != default && nextDirection != pipe.Direction)
        {
            m_joints.Add(new PipeJoint(pipe.Head, pipe.ShadeOffset));
            pipe.StraightSegments = 1;
        }
        else
        {
            pipe.StraightSegments++;
        }

        pipe.Direction = nextDirection;
        pipe.Target = pipe.Head + pipe.Direction;
        m_occupied[pipe.Target.X, pipe.Target.Y, pipe.Target.Z] = true;
        pipe.HasTarget = true;
        return true;
    }

    private void FindAvailableDirections(GridPoint head)
    {
        m_availableDirections.Clear();
        foreach (var direction in s_directions)
        {
            var candidate = head + direction;
            if (IsInside(candidate) && !m_occupied[candidate.X, candidate.Y, candidate.Z])
                m_availableDirections.Add(direction);
        }
    }

    private void StartAvailablePipes()
    {
        while (m_activePipes.Count < SimultaneousPipes && m_startedPipeCount < MaxPipes)
        {
            if (!TryFindFreeCell(out var head))
                break;

            var pipe = new PipeHead
            {
                Head = head,
                ShadeOffset = GetUnusedShadeOffset(),
                LastSegmentIndex = -1
            };
            m_activePipes.Add(pipe);
            m_startedPipeCount++;
            m_occupied[head.X, head.Y, head.Z] = true;
            m_joints.Add(new PipeJoint(head, pipe.ShadeOffset));
        }
    }

    private int GetUnusedShadeOffset()
    {
        int[] offsets = [-1, 1, 0];
        for (var candidateIndex = 0; candidateIndex < offsets.Length; candidateIndex++)
        {
            var candidate = offsets[(m_startedPipeCount + candidateIndex) % offsets.Length];
            var isInUse = false;
            foreach (var pipe in m_activePipes)
                isInUse |= pipe.ShadeOffset == candidate;
            if (!isInUse)
                return candidate;
        }

        return offsets[m_startedPipeCount % offsets.Length];
    }

    private void FinishPipe(PipeHead pipe)
    {
        m_joints.Add(new PipeJoint(pipe.Head, pipe.ShadeOffset));
        pipe.HasTarget = false;
    }

    private bool TryFindFreeCell(out GridPoint cell)
    {
        var freeCells = GridWidth * GridHeight * GridDepth;
        var start = Random.Shared.Next(freeCells);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < freeCells; i++)
            {
                var index = (start + i) % freeCells;
                var x = index % GridWidth;
                var y = index / GridWidth % GridHeight;
                var z = index / (GridWidth * GridHeight);
                if (m_occupied[x, y, z])
                    continue;

                cell = new GridPoint(x, y, z);
                if (pass == 0 && !IsVisibleSpawn(cell))
                    continue;
                return true;
            }
        }

        cell = default;
        return false;
    }

    private static bool IsVisibleSpawn(GridPoint cell)
    {
        var point = Project(cell);
        const int margin = 8;
        return point.X is >= margin and < PixelWidth - margin &&
               point.Y is >= margin and < PixelHeight - margin;
    }

    private void DrawScene(PixelScreenData screen)
    {
        Array.Fill(m_depthBuffer, double.NegativeInfinity);

        for (var i = 0; i < m_segments.Count; i++)
        {
            var isMergedIntoGrowingSegment = false;
            foreach (var pipe in m_activePipes)
                isMergedIntoGrowingSegment |= ShouldMergeGrowingSegment(pipe) && pipe.LastSegmentIndex == i;
            if (isMergedIntoGrowingSegment)
                continue;

            var segment = m_segments[i];
            DrawTube(screen, segment.Start, segment.End, GetDirection(segment), segment.ShadeOffset);
        }

        foreach (var pipe in m_activePipes)
        {
            if (!pipe.HasTarget)
                continue;

            var mergeGrowingSegment = ShouldMergeGrowingSegment(pipe);
            var amount = Math.Clamp(pipe.SegmentFrame / (double)FramesPerSegment, 0.0, 1.0);
            DrawTube(
                screen,
                mergeGrowingSegment ? m_segments[pipe.LastSegmentIndex].Start : pipe.Head,
                Lerp(pipe.Head, pipe.Target, amount),
                pipe.Direction,
                pipe.ShadeOffset);
        }

        foreach (var joint in m_joints)
            DrawJoint(screen, joint.Position, joint.ShadeOffset);
    }

    private bool ShouldMergeGrowingSegment(PipeHead pipe)
    {
        if (!pipe.HasTarget || pipe.LastSegmentIndex < 0)
            return false;

        var previous = m_segments[pipe.LastSegmentIndex];
        return previous.End == pipe.Head && GetDirection(previous) == pipe.Direction;
    }

    private void DrawTube(
        PixelScreenData screen,
        WorldPoint start,
        WorldPoint end,
        GridPoint direction,
        int shadeOffset)
    {
        var a = Project(start);
        var b = Project(end);
        var baseShade = direction.Y != 0 ? 5 : 4;
        baseShade = Math.Clamp(baseShade + shadeOffset, 2, 6);

        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;
        var startRadius = GetPerspectiveScale(start) * TubeRadius;
        var endRadius = GetPerspectiveScale(end) * TubeRadius;
        var maxRadius = Math.Max(startRadius, endRadius);
        var minX = Math.Max(0, (int)Math.Floor(Math.Min(a.X, b.X) - maxRadius));
        var maxX = Math.Min(screen.Width - 1, (int)Math.Ceiling(Math.Max(a.X, b.X) + maxRadius));
        var minY = Math.Max(0, (int)Math.Floor(Math.Min(a.Y, b.Y) - maxRadius));
        var maxY = Math.Min(screen.Height - 1, (int)Math.Ceiling(Math.Max(a.Y, b.Y) + maxRadius));
        var startDepth = GetDepth(start);
        var endDepth = GetDepth(end);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var amount = lengthSquared < 0.001
                    ? 0.0
                    : Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / lengthSquared, 0.0, 1.0);
                var nearestX = a.X + dx * amount;
                var nearestY = a.Y + dy * amount;
                var offsetX = x - nearestX;
                var offsetY = y - nearestY;
                var distanceSquared = offsetX * offsetX + offsetY * offsetY;
                var radius = startRadius + (endRadius - startRadius) * amount;
                if (distanceSquared > radius * radius)
                    continue;

                var normalZ = Math.Sqrt(Math.Max(0.0, 1.0 - distanceSquared / (radius * radius)));
                var light = normalZ * 0.5 - offsetX / radius * 0.35 - offsetY / radius * 0.5;
                var shade = (byte)Math.Clamp(baseShade + (int)Math.Round(light * 2.0 - 0.35), 1, 7);
                var depth = startDepth + (endDepth - startDepth) * amount + normalZ * 0.4;
                SetDepthPixel(screen, x, y, depth, shade);
            }
        }
    }

    private void DrawJoint(PixelScreenData screen, WorldPoint position, int shadeOffset)
    {
        var center = Project(position);
        var radius = JointRadius * GetPerspectiveScale(position);
        var pixelRadius = (int)Math.Ceiling(radius);
        var baseShade = Math.Clamp(4 + shadeOffset, 2, 6);
        var centerDepth = GetDepth(position);

        for (var y = -pixelRadius; y <= pixelRadius; y++)
        {
            for (var x = -pixelRadius; x <= pixelRadius; x++)
            {
                var distanceSquared = x * x + y * y;
                if (distanceSquared > radius * radius)
                    continue;

                var normalX = x / radius;
                var normalY = y / radius;
                var normalZ = Math.Sqrt(Math.Max(0.0, 1.0 - distanceSquared / (radius * radius)));
                var light = -normalX * 0.35 - normalY * 0.5 + normalZ * 0.65;
                var shade = (byte)Math.Clamp(baseShade + (int)Math.Round(light * 2.0 - 0.35), 1, 7);
                SetDepthPixel(
                    screen,
                    (int)Math.Round(center.X + x),
                    (int)Math.Round(center.Y + y),
                    centerDepth + normalZ * 0.55,
                    shade);
            }
        }
    }

    private void SetDepthPixel(PixelScreenData screen, int x, int y, double depth, byte shade)
    {
        if (x < 0 || x >= screen.Width || y < 0 || y >= screen.Height)
            return;

        var index = y * screen.Width + x;
        if (depth < m_depthBuffer[index])
            return;

        m_depthBuffer[index] = depth;
        screen.Pixels[index] = shade;
    }

    private static ScreenPoint Project(WorldPoint point)
    {
        var relative = ToRenderPoint(point) - s_cameraPosition;
        var cameraDepth = Math.Max(0.01f, Vector3.Dot(relative, s_cameraForward));
        var perspective = FocalLength / cameraDepth;
        var x = PixelWidth * 0.5 + Vector3.Dot(relative, s_cameraRight) * perspective;
        var y = PixelHeight * 0.47 - Vector3.Dot(relative, s_cameraUp) * perspective;
        return new ScreenPoint(x, y);
    }

    private static WorldPoint Lerp(GridPoint a, GridPoint b, double amount) =>
        new WorldPoint(
            a.X + (b.X - a.X) * amount,
            a.Y + (b.Y - a.Y) * amount,
            a.Z + (b.Z - a.Z) * amount);

    private static double GetDepth(WorldPoint point) =>
        -GetCameraDepth(point);

    private static double GetPerspectiveScale(WorldPoint point) =>
        s_referenceDepth / Math.Max(0.01f, GetCameraDepth(point));

    private static float GetCameraDepth(WorldPoint point) =>
        Vector3.Dot(ToRenderPoint(point) - s_cameraPosition, s_cameraForward);

    private static Vector3 ToRenderPoint(WorldPoint point)
    {
        var world = new Vector3((float)point.X, (float)point.Y, (float)point.Z);
        return s_sceneCenter + (world - s_sceneCenter) * WorldSpacing;
    }

    private static GridPoint GetDirection(PipeSegment segment) =>
        new GridPoint(
            Math.Sign(segment.End.X - segment.Start.X),
            Math.Sign(segment.End.Y - segment.Start.Y),
            Math.Sign(segment.End.Z - segment.Start.Z));

    private static bool IsInside(GridPoint point) =>
        point.X is >= 0 and < GridWidth &&
        point.Y is >= 0 and < GridHeight &&
        point.Z is >= 0 and < GridDepth;

    private Rgb[] GetPalette()
    {
        var foreground = Foreground ?? Rgb.White;
        var background = Background ?? Rgb.Black;
        return PixelScreenData.CreateGreyscalePalette(s_basePalette, background, foreground);
    }

    private void ResetScene()
    {
        Array.Clear(m_occupied);
        m_segments.Clear();
        m_joints.Clear();
        m_activePipes.Clear();
        m_startedPipeCount = 0;
        m_sceneFrame = 0;
        m_resetFrames = 0;
        StartAvailablePipes();
    }

    private void ClearPixelScreen()
    {
        m_isActive = false;
        if (m_windowManager != null &&
            m_pixelScreen != null &&
            ReferenceEquals(m_windowManager.PixelScreen, m_pixelScreen))
            m_windowManager.ClearPixelScreen();
        m_pixelScreen = null;
    }

    private static void ClearTextOverlay(ScreenData screen)
    {
        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Width; x++)
            {
                var attr = screen.Chars[y][x];
                attr.Set(' ');
                attr.Foreground = null;
                attr.Background = null;
            }
        }
    }

    private readonly record struct GridPoint(int X, int Y, int Z)
    {
        public static GridPoint operator +(GridPoint a, GridPoint b) =>
            new GridPoint(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static implicit operator WorldPoint(GridPoint point) =>
            new WorldPoint(point.X, point.Y, point.Z);
    }

    private readonly record struct WorldPoint(double X, double Y, double Z);
    private readonly record struct ScreenPoint(double X, double Y);
    private readonly record struct PipeSegment(GridPoint Start, GridPoint End, int ShadeOffset);
    private readonly record struct PipeJoint(GridPoint Position, int ShadeOffset);

    private sealed class PipeHead
    {
        public GridPoint Head { get; set; }
        public GridPoint Target { get; set; }
        public GridPoint Direction { get; set; }
        public int StraightSegments { get; set; }
        public int SegmentFrame { get; set; }
        public int ShadeOffset { get; init; }
        public int LastSegmentIndex { get; set; }
        public bool HasTarget { get; set; }
    }
}
