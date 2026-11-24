using System.Collections.Generic;
using Lockdown.AI.Core.Director;
using Lockdown.Enemies;
using Lockdown.World;
using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.AI.Agents
{
    /// <summary>Creates enemies and gives each one an in-world reason to move. Never passes on the player's position.</summary>
    public sealed class EnemySpawner
    {
        private const int PatrolLength = 3;
        private readonly GameObject guardPrefab, archerPrefab, houndPrefab;
        private readonly IReadOnlyList<SpawnPoint> points;
        private readonly MuseumRoutes routes;
        private readonly AlarmStimulusRelay alarm;
        private readonly Transform parent;

        public EnemySpawner(GameObject guardPrefab, GameObject archerPrefab, GameObject houndPrefab,
            IReadOnlyList<SpawnPoint> points, MuseumRoutes routes, AlarmStimulusRelay alarm, Transform parent)
        {
            this.guardPrefab = guardPrefab; this.archerPrefab = archerPrefab; this.houndPrefab = houndPrefab;
            this.points = points; this.routes = routes; this.alarm = alarm; this.parent = parent;
        }

        public bool TrySpawn(SpawnOption option, Transform player, out GameObject enemy)
        {
            enemy = null;
            var prefab = option.Kind == SpawnKind.Guard ? guardPrefab : option.Kind == SpawnKind.Archer ? archerPrefab : houndPrefab;
            if (prefab == null || !NavMesh.SamplePosition(option.Position, out var floor, 1f, NavMesh.AllAreas)) return false;
            var instance = Object.Instantiate(prefab, floor.position, points[option.PointId].transform.rotation, parent);
            instance.name = "Director_" + option.Kind;
            if (!instance.TryGetComponent<NavMeshAgent>(out var body) || !body.isOnNavMesh)
            {
                Object.Destroy(instance);
                return false;
            }
            if (option.Kind == SpawnKind.Guard) BriefGuard(instance, floor.position, player);
            else if (option.Kind == SpawnKind.Hound) BriefHound(instance);
            enemy = instance;
            return true;
        }

        /// <summary>An archer arrives as a pile of bones; wake it once its own Start has run.</summary>
        public static bool TryWakeArcher(GameObject enemy)
        {
            if (!enemy.TryGetComponent<SkeletonDormancy>(out var dormancy)) return true;
            dormancy.Wake();
            return dormancy.IsWaking;
        }

        private void BriefGuard(GameObject instance, Vector3 position, Transform player)
        {
            if (!instance.TryGetComponent<PursuerAgent>(out var guard)) return;
            guard.Assign(player, PatrolAround(position));
            if (alarm != null && alarm.IsUnanswered) alarm.TryAnswerWith(guard);
        }

        private void BriefHound(GameObject instance)
        {
            if (alarm != null && alarm.HasRecentAlarm && instance.TryGetComponent<HoundInvestigator>(out var hound))
                hound.Investigate(alarm.LatestPosition);
        }

        /// <summary>The nearest reachable spawn points make a patrol that stays in this part of the museum.</summary>
        private Transform[] PatrolAround(Vector3 position)
        {
            var ranked = new List<KeyValuePair<float, Transform>>();
            foreach (var point in points)
            {
                if (point == null) continue;
                float length = routes.Length(position, point.transform.position);
                if (!float.IsInfinity(length)) ranked.Add(new KeyValuePair<float, Transform>(length, point.transform));
            }
            ranked.Sort((left, right) => left.Key.CompareTo(right.Key));
            var patrol = new Transform[Mathf.Min(PatrolLength, ranked.Count)];
            for (int i = 0; i < patrol.Length; i++) patrol[i] = ranked[i].Value;
            return patrol;
        }
    }
}
