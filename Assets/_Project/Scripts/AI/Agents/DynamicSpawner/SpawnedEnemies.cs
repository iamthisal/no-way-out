using System.Collections.Generic;
using Lockdown.AI.Core.Director;
using Lockdown.Combat;
using UnityEngine;

namespace Lockdown.AI.Agents
{
    /// <summary>Remembers only the director's own spawns, and removes those that are far away, idle and long unseen.</summary>
    public sealed class SpawnedEnemies
    {
        private sealed class Record { public GameObject Enemy; public float UnseenSeconds; }

        private readonly List<Record> records = new List<Record>();
        private readonly MuseumRoutes routes;
        private readonly DirectorTuning tuning;
        public int Count => records.Count;
        public int Removed { get; private set; }

        public SpawnedEnemies(MuseumRoutes routes, DirectorTuning tuning) { this.routes = routes; this.tuning = tuning; }

        public void Add(GameObject enemy) { records.Add(new Record { Enemy = enemy }); }

        public void Tick(DirectorPhase phase, PlayerObserver player, float deltaTime)
        {
            for (int i = records.Count - 1; i >= 0; i--)
            {
                var enemy = records[i].Enemy;
                if (enemy == null || (enemy.TryGetComponent<Health>(out var health) && health.IsDead)) { records.RemoveAt(i); continue; }
                Vector3 feet = enemy.transform.position;
                bool visible = PlayerSight.InView(player.View, feet)
                    || PlayerSight.HasLine(player.View.transform.position, feet, player.Player.transform, enemy.transform);
                records[i].UnseenSeconds = visible ? 0f : records[i].UnseenSeconds + deltaTime;
                if (visible || records[i].UnseenSeconds < tuning.despawnUnseenSeconds) continue;
                if (!DespawnRule.Allows(phase, routes.Length(feet, player.Position), records[i].UnseenSeconds,
                    EnemyRoster.IsEngaged(enemy), tuning)) continue;
                Object.Destroy(enemy);
                records.RemoveAt(i);
                Removed++;
            }
        }
    }
}
