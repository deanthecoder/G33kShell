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
using System.Diagnostics;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Screensavers.AI;

namespace G33kShell.Desktop.Console.Screensavers.RoadFighter;

[DebuggerDisplay("Score = {Score}, Distance = {Distance}, Overtakes = {Overtakes}")]
public class Game : AiGameBase
{
    public readonly struct TrainingProfile
    {
        public static TrainingProfile Default => new(1.0, 0.06, 1.0, "Full");

        public TrainingProfile(double curveStrength, double trafficSpawnChance, double roadWidthMultiplier, string stageLabel)
        {
            CurveStrength = curveStrength;
            TrafficSpawnChance = trafficSpawnChance;
            RoadWidthMultiplier = roadWidthMultiplier;
            StageLabel = stageLabel;
        }

        public double CurveStrength { get; }
        public double TrafficSpawnChance { get; }
        public double RoadWidthMultiplier { get; }
        public string StageLabel { get; }
    }

    private const int MaxTicksPerTrainingGame = 4500;
    private const int MaxTicksWithoutOvertake = 900;
    private const double CarSpriteWidth = 4.0;
    private const double CarSpriteHeight = 3.0;
    private const double CollisionInsetX = 0.45;
    private const double CollisionInsetY = 0.20;
    private const double RoadWidthRatio = 0.45;
    private const double PlayerYRatio = 0.82;
    private const double PlayerSpeed = 1.0;
    private const double LaneChangeStep = 1.8;
    private const int MinLane = -1;
    private const int MaxLane = 1;
    private readonly bool m_useTrainingTimeouts;
    private readonly TrainingProfile m_trainingProfile;
    private readonly List<TrafficCar> m_trafficCars = [];
    private double[] m_roadCenters;
    private GameState m_gameState;
    private double m_topRoadCenter;
    private double m_topRoadDrift;
    private int m_targetLane;
    private int m_ticks;
    private int m_ticksSinceOvertake;
    private int m_offRoadTicks;
    private int m_nearMisses;
    private int m_collisions;
    private int m_blockedTicks;
    private int m_directionChanges;
    private int m_steeringTicks;
    private int m_previousSteerDirection;
    private double m_cumulativeCenterError;
    private double m_cumulativeLookaheadError;
    private double m_cumulativeImmediateTrafficThreat;
    private double m_cumulativeNearTrafficThreat;
    private double m_cumulativeLaneQuality;
    private double m_cumulativeLaneOpportunityGap;

    public int Distance { get; private set; }
    public int Overtakes { get; private set; }
    public int Score => Distance + Overtakes * 120 + m_nearMisses * 25;
    public double PlayerX { get; private set; }
    public int PlayerY { get; private set; }
    public int PlayerLane => GetClosestLane(PlayerY, PlayerX);
    public bool Crashed { get; private set; }
    public IReadOnlyList<TrafficCar> TrafficCars => m_trafficCars;
    public int RoadWidth { get; private set; }
    public int NearMisses => m_nearMisses;
    public int OffRoadTicks => m_offRoadTicks;
    public int SteeringChanges => m_directionChanges;
    public int SteeringTicks => m_steeringTicks;
    public int Ticks => m_ticks;
    public int Collisions => m_collisions;
    public int BlockedTicks => m_blockedTicks;
    public double AverageCenterError => m_ticks == 0 ? 0.0 : m_cumulativeCenterError / m_ticks;
    public double AverageLookaheadError => m_ticks == 0 ? 0.0 : m_cumulativeLookaheadError / m_ticks;
    public double AverageImmediateTrafficThreat => m_ticks == 0 ? 0.0 : m_cumulativeImmediateTrafficThreat / m_ticks;
    public double AverageNearTrafficThreat => m_ticks == 0 ? 0.0 : m_cumulativeNearTrafficThreat / m_ticks;
    public double AverageLaneQuality => m_ticks == 0 ? 0.0 : m_cumulativeLaneQuality / m_ticks;
    public double AverageLaneOpportunityGap => m_ticks == 0 ? 0.0 : m_cumulativeLaneOpportunityGap / m_ticks;

    public static TrainingProfile GetCurriculumProfile(int generation)
    {
        if (generation <= 12)
            return new TrainingProfile(0.0, 0.0, 1.00, "Straight");
        if (generation <= 30)
            return new TrainingProfile(0.0, 0.035, 1.00, "Dodge");
        if (generation <= 48)
            return new TrainingProfile(0.85, 0.0, 1.00, "Curve");

        return TrainingProfile.Default;
    }

    public override double Rating
    {
        get
        {
            if (Distance < 80)
                return 0.0;

            var smoothness = m_ticks == 0
                ? 1.0
                : 1.0 - Math.Min(1.0, m_directionChanges / Math.Max(1.0, m_ticks / 5.0));
            var steeringRate = m_ticks == 0 ? 0.0 : m_steeringTicks / (double)m_ticks;
            var offRoadRate = m_ticks == 0 ? 0.0 : m_offRoadTicks / (double)m_ticks;
            var blockedRate = m_ticks == 0 ? 0.0 : m_blockedTicks / (double)m_ticks;
            var averageCenterError = AverageCenterError;
            var averageLookaheadError = AverageLookaheadError;
            var immediateThreat = AverageImmediateTrafficThreat;
            var nearThreat = AverageNearTrafficThreat;
            var laneQuality = ((AverageLaneQuality + 2.2) / 3.7).Clamp(0.0, 1.0);
            var laneOpportunityGap = AverageLaneOpportunityGap;
            var distanceScore = Distance * 2.10;
            var overtakeScore = Overtakes * 30.0;
            var nearMissScore = m_nearMisses * 20.0;
            var smoothnessScore = smoothness * 100.0;
            var centeringScore = (1.0 - Math.Min(1.0, averageCenterError)) * 160.0;
            var lookaheadScore = (1.0 - Math.Min(1.0, averageLookaheadError)) * 140.0;
            var trafficAvoidanceScore = (1.0 - Math.Min(1.0, nearThreat)) * 260.0;
            var laneQualityScore = laneQuality * 520.0;
            var steeringPenalty = Math.Max(0.0, steeringRate - 0.46) * m_ticks * 0.8;
            var stagnationPenalty = Math.Max(0, m_ticksSinceOvertake - 800) * 0.01;
            var oversteerPenalty = Math.Max(0.0, steeringRate - 0.72) * m_ticks * 0.45;
            var offRoadPenalty = offRoadRate * m_ticks * 6.5;
            var cleanDrivingFactor = 1.0 - Math.Min(0.35, offRoadRate * 1.5);
            var earlyCrashPenalty = Crashed ? Math.Max(0.0, 140.0 - Distance) * 6.0 : 0.0;
            var immediateThreatPenalty = immediateThreat * m_ticks * 4.5;
            var blockingPenalty = blockedRate * m_ticks * 4.0;
            var laneOpportunityPenalty = laneOpportunityGap * m_ticks * 3.6;

            return Math.Max(0.0,
                (distanceScore +
                 overtakeScore +
                 nearMissScore +
                 smoothnessScore +
                 centeringScore +
                 lookaheadScore +
                 trafficAvoidanceScore +
                 laneQualityScore) * cleanDrivingFactor -
                offRoadPenalty -
                immediateThreatPenalty -
                blockingPenalty -
                laneOpportunityPenalty -
                steeringPenalty -
                oversteerPenalty -
                stagnationPenalty -
                earlyCrashPenalty -
                m_collisions * 550.0);
        }
    }

    public override double DegeneracyScore
    {
        get
        {
            var wiggleRate = m_ticks == 0 ? 0.0 : m_directionChanges / (double)m_ticks;
            var scrapeRate = m_ticks == 0 ? 0.0 : m_offRoadTicks / (double)m_ticks;
            var steeringRate = m_ticks == 0 ? 0.0 : m_steeringTicks / (double)m_ticks;
            var wigglePenalty = Math.Min(1.0, Math.Max(0.0, wiggleRate - 0.10) / 0.18);
            var scrapePenalty = Math.Min(1.0, Math.Max(0.0, scrapeRate - 0.10) / 0.20);
            var oversteerPenalty = Math.Min(1.0, Math.Max(0.0, steeringRate - 0.55) / 0.30);
            return Math.Max(wigglePenalty, Math.Max(scrapePenalty, oversteerPenalty));
        }
    }

    public override string DegeneracyReason =>
        DegeneracyScore < 0.60
            ? string.Empty
            : m_offRoadTicks > Math.Max(20, m_ticks / 4)
                ? "edge-riding"
                : m_steeringTicks > m_ticks * 0.70
                    ? "oversteering"
                    : "wiggling";

    public override bool IsGameOver =>
        Crashed ||
        (m_useTrainingTimeouts && (m_ticks >= MaxTicksPerTrainingGame || m_ticksSinceOvertake >= MaxTicksWithoutOvertake));

    public override (string Name, double Value, string Format)? BestObservedMetric =>
        ("Distance", Distance, "0");

    public override IEnumerable<(string Name, string Value)> ExtraGameStats()
    {
        yield return ("Stage", m_trainingProfile.StageLabel);
        yield return ("Score", Score.ToString());
        yield return ("Distance", Distance.ToString());
        yield return ("Overtakes", Overtakes.ToString());
        yield return ("NearMiss", m_nearMisses.ToString());
        yield return ("OffRoad", m_offRoadTicks.ToString());
        yield return ("Blocked", m_blockedTicks.ToString());
        yield return ("SteerChanges", m_directionChanges.ToString());
        yield return ("CenterErr", AverageCenterError.ToString("F2"));
    }

    public Game(int arenaWidth, int arenaHeight, Brain brain, bool useTrainingTimeouts = true, TrainingProfile? trainingProfile = null) : base(arenaWidth, arenaHeight, brain)
    {
        m_useTrainingTimeouts = useTrainingTimeouts;
        m_trainingProfile = trainingProfile ?? TrainingProfile.Default;
    }

    public override AiGameBase ResetGame()
    {
        Brain.ResetTemporalState();
        RoadWidth = Math.Max(14, (int)Math.Round(ArenaWidth * RoadWidthRatio * m_trainingProfile.RoadWidthMultiplier));
        PlayerY = Math.Min(ArenaHeight - 3, (int)Math.Round(ArenaHeight * PlayerYRatio));
        m_roadCenters = new double[ArenaHeight];
        m_topRoadCenter = ArenaWidth / 2.0;
        m_topRoadDrift = 0.0;

        for (var y = 0; y < ArenaHeight; y++)
            m_roadCenters[y] = m_topRoadCenter;

        m_targetLane = 0;
        PlayerX = GetLaneCenter(m_targetLane, PlayerY);
        Distance = 0;
        Overtakes = 0;
        Crashed = false;
        m_ticks = 0;
        m_ticksSinceOvertake = 0;
        m_offRoadTicks = 0;
        m_nearMisses = 0;
        m_collisions = 0;
        m_blockedTicks = 0;
        m_directionChanges = 0;
        m_steeringTicks = 0;
        m_previousSteerDirection = 0;
        m_cumulativeCenterError = 0.0;
        m_cumulativeLookaheadError = 0.0;
        m_cumulativeImmediateTrafficThreat = 0.0;
        m_cumulativeNearTrafficThreat = 0.0;
        m_cumulativeLaneQuality = 0.0;
        m_cumulativeLaneOpportunityGap = 0.0;
        m_trafficCars.Clear();
        m_gameState = new GameState(this);

        for (var i = 0; i < ArenaHeight; i++)
            ScrollRoad();

        return this;
    }

    public override void Tick()
    {
        TickInternal(((Brain)Brain).ChooseMove(m_gameState));
    }

    public void Tick(int move)
    {
        TickInternal(move.Clamp(-1, 1));
    }

    private void TickInternal(int move)
    {
        if (IsGameOver)
            return;

        m_gameState.Reset(this);
        TrackSteering(move);
        m_targetLane = (m_targetLane + move).Clamp(MinLane, MaxLane);

        ScrollRoad();
        var targetX = GetLaneCenter(m_targetLane, PlayerY);
        var deltaX = targetX - PlayerX;
        if (Math.Abs(deltaX) <= LaneChangeStep)
            PlayerX = targetX;
        else
            PlayerX += Math.Sign(deltaX) * LaneChangeStep;
        MoveTraffic();
        MaybeSpawnTraffic();
        m_cumulativeCenterError += Math.Abs(GetPlayerRoadOffset());
        m_cumulativeLookaheadError += Math.Abs(GetUpcomingCenterOffset(6));
        m_cumulativeImmediateTrafficThreat += GetPlayerCorridorDanger(0, 4);
        m_cumulativeNearTrafficThreat += GetPlayerCorridorDanger(5, 10);
        TrackLaneDecisionQuality();
        if (GetLaneClearance(PlayerLane, 12) < 0.45)
            m_blockedTicks++;
        CheckRoadContact();
        CheckTrafficCollisions();

        Distance++;
        m_ticks++;
        m_ticksSinceOvertake++;
    }

    public double GetRoadCenter(int y) => m_roadCenters[Math.Clamp(y, 0, ArenaHeight - 1)];

    public double GetLaneCenter(int lane, int y)
    {
        var center = GetRoadCenter(y);
        var laneSpacing = Math.Max(3.0, RoadWidth / 4.2);
        return (center + lane * laneSpacing).Clamp(1.0, ArenaWidth - 2.0);
    }

    private int GetClosestLane(int y, double x)
    {
        var bestLane = 0;
        var bestDistance = double.MaxValue;
        for (var lane = MinLane; lane <= MaxLane; lane++)
        {
            var laneDistance = Math.Abs(GetLaneCenter(lane, y) - x);
            if (laneDistance >= bestDistance)
                continue;

            bestDistance = laneDistance;
            bestLane = lane;
        }

        return bestLane;
    }

    public (int Left, int Right) GetRoadBounds(int y)
    {
        var center = GetRoadCenter(y);
        var halfWidth = RoadWidth / 2.0;
        var left = (int)Math.Round(center - halfWidth);
        var right = (int)Math.Round(center + halfWidth);
        return (left, right);
    }

    public double GetPlayerRoadOffset()
    {
        var center = GetRoadCenter(PlayerY);
        return (PlayerX - center) / Math.Max(1.0, RoadWidth / 2.0 - 2.0);
    }

    public double GetLeftMargin()
    {
        var (left, _) = GetRoadBounds(PlayerY);
        return ((PlayerX - left - 1.5) / Math.Max(1.0, RoadWidth / 2.0)).Clamp(0.0, 1.0);
    }

    public double GetRightMargin()
    {
        var (_, right) = GetRoadBounds(PlayerY);
        return ((right - PlayerX - 1.5) / Math.Max(1.0, RoadWidth / 2.0)).Clamp(0.0, 1.0);
    }

    public double GetRoadCurveDelta(int fromAhead, int toAhead)
    {
        var fromY = Math.Max(0, PlayerY - fromAhead);
        var toY = Math.Max(0, PlayerY - toAhead);
        return (GetRoadCenter(toY) - GetRoadCenter(fromY)) / Math.Max(1.0, RoadWidth / 2.0);
    }

    public double GetUpcomingCenterOffset(int ahead)
    {
        var targetY = Math.Max(0, PlayerY - ahead);
        return (GetRoadCenter(targetY) - PlayerX) / Math.Max(1.0, RoadWidth / 2.0);
    }

    public bool TryGetNearestTraffic(out double dx, out double dy, out double speedDelta)
    {
        dx = 0.0;
        dy = 0.0;
        speedDelta = 0.0;
        var nearestDistance = double.MaxValue;

        foreach (var car in m_trafficCars)
        {
            if (car.Y > PlayerY + 1.5)
                continue;

            var localDx = (car.Lane - PlayerLane) / 2.0;
            var localDy = (PlayerY - car.Y) / Math.Max(1.0, ArenaHeight);
            var distance = localDx * localDx + localDy * localDy;
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            dx = localDx;
            dy = localDy;
            speedDelta = (car.Speed - PlayerSpeed).Clamp(-1.0, 1.0);
        }

        return nearestDistance < double.MaxValue;
    }

    public double GetLaneDanger(int lane, int minAhead, int maxAhead)
    {
        var strongest = 0.0;
        foreach (var car in m_trafficCars)
        {
            var ahead = PlayerY - car.Y;
            if (ahead < minAhead || ahead > maxAhead)
                continue;
            if (car.Lane != lane)
                continue;

            var threat = 1.0 - ahead / (double)Math.Max(1, maxAhead);
            strongest = Math.Max(strongest, threat);
        }

        return strongest;
    }

    public double GetLaneClearance(int lane, int maxAhead)
    {
        var nearestAhead = maxAhead + 1.0;
        foreach (var car in m_trafficCars)
        {
            var ahead = PlayerY - car.Y;
            if (ahead < 0.0 || ahead > maxAhead)
                continue;
            if (car.Lane != lane)
                continue;

            nearestAhead = Math.Min(nearestAhead, ahead);
        }

        if (nearestAhead > maxAhead)
            return 1.0;

        return (nearestAhead / maxAhead).Clamp(0.0, 1.0);
    }

    private double GetPlayerCorridorDanger(int minAhead, int maxAhead)
    {
        var strongest = 0.0;
        foreach (var car in m_trafficCars)
        {
            var ahead = PlayerY - car.Y;
            if (ahead < minAhead || ahead > maxAhead)
                continue;
            if (car.Lane != PlayerLane)
                continue;

            var closeness = 1.0 - ahead / Math.Max(1.0, maxAhead + 1.0);
            strongest = Math.Max(strongest, closeness);
        }

        return strongest;
    }

    private void TrackLaneDecisionQuality()
    {
        var currentLaneScore = GetLaneDecisionScore(PlayerLane);
        var bestLaneScore = currentLaneScore;

        for (var lane = MinLane; lane <= MaxLane; lane++)
            bestLaneScore = Math.Max(bestLaneScore, GetLaneDecisionScore(lane));

        m_cumulativeLaneQuality += currentLaneScore;
        m_cumulativeLaneOpportunityGap += Math.Max(0.0, bestLaneScore - currentLaneScore);
    }

    private double GetLaneDecisionScore(int lane)
    {
        var clearance = GetLaneClearance(lane, 16);
        var immediateDanger = GetLaneDanger(lane, 0, 5);
        var nearDanger = GetLaneDanger(lane, 6, 12);
        var laneBias = lane == 0 ? 0.08 : 0.0;
        return clearance * 1.4 - immediateDanger * 1.5 - nearDanger * 0.6 + laneBias;
    }

    private void ScrollRoad()
    {
        for (var y = ArenaHeight - 1; y > 0; y--)
            m_roadCenters[y] = m_roadCenters[y - 1];

        var margin = RoadWidth / 2.0 + 3.0;
        var targetLeft = margin;
        var targetRight = ArenaWidth - margin;
        var driftStep = 0.14 * m_trainingProfile.CurveStrength;
        m_topRoadDrift += GameRand.NextDouble() * (driftStep * 2.0) - driftStep;
        if (m_topRoadCenter < targetLeft + 2.0)
            m_topRoadDrift += 0.18;
        else if (m_topRoadCenter > targetRight - 2.0)
            m_topRoadDrift -= 0.18;

        m_topRoadDrift = m_topRoadDrift.Clamp(-0.9, 0.9);
        m_topRoadCenter = (m_topRoadCenter + m_topRoadDrift).Clamp(targetLeft, targetRight);
        m_roadCenters[0] = m_topRoadCenter;
    }

    private void MoveTraffic()
    {
        for (var i = m_trafficCars.Count - 1; i >= 0; i--)
        {
            var car = m_trafficCars[i];
            car.Y += car.Speed * 0.85;
            car.X = GetLaneCenter(car.Lane, (int)Math.Round(car.Y).Clamp(0, ArenaHeight - 1));

            if (!car.PassedPlayer && car.Y > PlayerY + 1.0)
            {
                car.PassedPlayer = true;
                Overtakes++;
                m_ticksSinceOvertake = 0;
            }

            if (IsNearMiss(car))
                m_nearMisses++;

            if (car.Y > ArenaHeight + 2)
                m_trafficCars.RemoveAt(i);
        }
    }

    private void MaybeSpawnTraffic()
    {
        var trafficRamp = Math.Clamp(Distance / 700.0, 0.0, 1.0);
        var effectiveSpawnChance = m_trainingProfile.TrafficSpawnChance * (0.30 + trafficRamp * 0.70);
        var maxActiveTraffic = 1 + (int)Math.Round(trafficRamp * 2.0);
        if (m_trafficCars.Count >= maxActiveTraffic)
            return;
        if (m_trafficCars.Count > 0 && m_trafficCars.Exists(o => o.Y < 6))
            return;
        if (GameRand.NextDouble() > effectiveSpawnChance)
            return;

        var y = -2.0;
        var candidateLanes = new List<int>(3);
        for (var candidateLane = MinLane; candidateLane <= MaxLane; candidateLane++)
        {
            var blocked = m_trafficCars.Exists(o => o.Lane == candidateLane && o.Y < ArenaHeight * 0.65);
            if (!blocked)
                candidateLanes.Add(candidateLane);
        }

        if (candidateLanes.Count == 0)
            return;

        var lane = candidateLanes[GameRand.Next(candidateLanes.Count)];
        var x = GetLaneCenter(lane, 0);
        var speed = 0.62 + GameRand.NextDouble() * 0.22;
        m_trafficCars.Add(new TrafficCar
        {
            Lane = lane,
            X = x.Clamp(2.0, ArenaWidth - 3.0),
            Y = y,
            Speed = speed,
        });
    }

    private void CheckRoadContact()
    {
        var (left, right) = GetRoadBounds(PlayerY);
        var outsideBy = Math.Max(left - (PlayerX - 1.0), (PlayerX + 1.0) - right);
        if (outsideBy <= 0.0)
            return;

        m_offRoadTicks++;
        if (outsideBy > 1.5 || m_offRoadTicks > 18)
        {
            Crashed = true;
            m_collisions++;
        }
    }

    private void CheckTrafficCollisions()
    {
        foreach (var car in m_trafficCars)
        {
            if (!DoCarsOverlap(car))
                continue;

            Crashed = true;
            m_collisions++;
            return;
        }
    }

    private void TrackSteering(int move)
    {
        if (move != 0)
            m_steeringTicks++;

        if (move != 0 && m_previousSteerDirection != 0 && move != m_previousSteerDirection)
            m_directionChanges++;

        if (move != 0)
            m_previousSteerDirection = move;
    }

    private bool IsNearMiss(TrafficCar car)
    {
        var playerRect = GetPlayerCollisionRect();
        var trafficRect = GetTrafficCollisionRect(car);
        var verticalGap = Math.Min(
            Math.Abs(playerRect.top - trafficRect.bottom),
            Math.Abs(trafficRect.top - playerRect.bottom));
        var horizontalGap = Math.Min(
            Math.Abs(playerRect.left - trafficRect.right),
            Math.Abs(trafficRect.left - playerRect.right));

        return !DoCarsOverlap(car) &&
               verticalGap < 1.0 &&
               horizontalGap < 1.2;
    }

    private bool DoCarsOverlap(TrafficCar car)
    {
        var playerRect = GetPlayerCollisionRect();
        var trafficRect = GetTrafficCollisionRect(car);
        return playerRect.left < trafficRect.right &&
               playerRect.right > trafficRect.left &&
               playerRect.top < trafficRect.bottom &&
               playerRect.bottom > trafficRect.top;
    }

    private (double left, double top, double right, double bottom) GetPlayerCollisionRect()
    {
        var left = PlayerX - CarSpriteWidth / 2.0 + CollisionInsetX;
        var top = PlayerY - 2.0 + CollisionInsetY;
        return (left, top, left + CarSpriteWidth - CollisionInsetX * 2.0, top + CarSpriteHeight - CollisionInsetY * 2.0);
    }

    private static (double left, double top, double right, double bottom) GetTrafficCollisionRect(TrafficCar car)
    {
        var left = car.X - CarSpriteWidth / 2.0 + CollisionInsetX;
        var top = car.Y - 1.0 + CollisionInsetY;
        return (left, top, left + CarSpriteWidth - CollisionInsetX * 2.0, top + CarSpriteHeight - CollisionInsetY * 2.0);
    }
}
