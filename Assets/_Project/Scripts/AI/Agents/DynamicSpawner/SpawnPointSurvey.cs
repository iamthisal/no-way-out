using System.Collections.Generic;
using Lockdown.AI.Core.Director;
using Lockdown.World;
using UnityEngine;

namespace Lockdown.AI.Agents
{
    /// <summary>Answers the brain's questions about spawn points with raycasts, overlap checks and NavMesh routes.</summary>
    public sealed class SpawnPointSurvey : IDirectorWorld
    {
        private readonly IReadOnlyList<SpawnPoint> points;
        private readonly PlayerObserver player;
        private readonly AlarmStimulusRelay alarm;
        private readonly MuseumRoutes routes;
        private readonly DirectorTuning tuning;
        private readonly string safeRoomId;
        private readonly float[] usedAt;

        public SpawnPointSurvey(IReadOnlyList<SpawnPoint> points, PlayerObserver player, AlarmStimulusRelay alarm,
            MuseumRoutes routes, DirectorTuning tuning, string safeRoomId)
        {
            this.points = points; this.player = player; this.alarm = alarm;
            this.routes = routes; this.tuning = tuning; this.safeRoomId = safeRoomId;
            usedAt = new float[points.Count];
            for (int i = 0; i < usedAt.Length; i++) usedAt[i] = float.NegativeInfinity;
        }

        public void MarkUsed(int pointId) { usedAt[pointId] = Time.time; }

        public void CollectCandidates(List<SpawnCandidate> results)
        {
            float playerToExit = player.Exit != null ? routes.Length(player.Position, player.Exit.position, 2.5f) : float.PositiveInfinity;
            for (int i = 0; i < points.Count; i++)
                if (points[i] != null && points[i].isActiveAndEnabled) results.Add(Measure(i, playerToExit));
        }

        private SpawnCandidate Measure(int id, float playerToExit)
        {
            var point = points[id];
            Vector3 position = point.transform.position;
            float route = routes.Length(position, player.Position);
            return new SpawnCandidate
            {
                PointId = id,
                RoomId = point.RoomId,
                Position = position,
                Zone = (SpawnZone)(int)point.Zone,
                Allowed = (SpawnKindSet)(int)point.Allowed,
                InView = PlayerSight.InView(player.View, position),
                HasLineOfSight = PlayerSight.HasLine(player.View.transform.position, position, player.Player.transform),
                Reachable = !float.IsInfinity(route),
                RouteLength = route,
                ClearSpace = IsClear(position),
                InSafeRoom = !string.IsNullOrEmpty(safeRoomId) && point.RoomId == safeRoomId,
                OnExitRoute = !float.IsInfinity(playerToExit) && SpawnPointFilter.LiesOnRoute(route,
                    routes.Length(position, player.Exit.position, 2.5f), playerToExit, tuning.exitRouteSlack),
                SecondsSinceUsed = Time.time - usedAt[id],
                AlarmDistance = alarm != null && alarm.HasRecentAlarm
                    ? Vector3.Distance(position, alarm.LatestPosition) : float.PositiveInfinity
            };
        }

        /// <summary>Room for the tallest enemy to stand.</summary>
        public static bool IsClear(Vector3 feet) => !Physics.CheckCapsule(feet + Vector3.up * 0.5f, feet + Vector3.up * 1.55f, 0.32f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }
}
