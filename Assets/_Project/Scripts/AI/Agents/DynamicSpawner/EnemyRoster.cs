using System.Collections.Generic;
using Lockdown.AI.Core.Ambusher;
using Lockdown.AI.Core.Director;
using Lockdown.AI.Core.Hound;
using Lockdown.Combat;
using UnityEngine;

namespace Lockdown.AI.Agents
{
    /// <summary>Counts the living enemies and how many of them are after the player.</summary>
    public sealed class EnemyRoster
    {
        private PursuerAgent[] guards = new PursuerAgent[0];
        private SkeletonArcherAgent[] archers = new SkeletonArcherAgent[0];
        private HoundAgent[] hounds = new HoundAgent[0];

        public EnemyCensus Census { get; private set; }
        public int Engaged { get; private set; }
        public bool PlayerSeen { get; private set; }

        public void Refresh(Vector3 player, float engagedRange)
        {
            guards = Object.FindObjectsByType<PursuerAgent>();
            archers = Object.FindObjectsByType<SkeletonArcherAgent>();
            hounds = Object.FindObjectsByType<HoundAgent>();
            var census = new EnemyCensus();
            int engaged = 0;
            bool seen = false;
            foreach (var guard in guards)
            {
                if (!IsAlive(guard)) continue;
                census.Add(SpawnKind.Guard);
                seen |= guard.IsPlayerVisible;
                if (guard.IsEngaged && Near(guard, player, engagedRange)) engaged++;
            }
            foreach (var archer in archers)
            {
                if (!IsAlive(archer)) continue;
                // A sleeping archer is a pile of bones with its brain switched off.
                census.Add(SpawnKind.Archer, !archer.enabled);
                if (!archer.enabled) continue;
                seen |= archer.SeesPlayer;
                if (IsEngaged(archer) && Near(archer, player, engagedRange)) engaged++;
            }
            foreach (var hound in hounds)
            {
                if (!IsAlive(hound)) continue;
                census.Add(SpawnKind.Hound);
                if (IsEngaged(hound) && Near(hound, player, engagedRange)) engaged++;
            }
            Census = census;
            Engaged = engaged;
            PlayerSeen = seen;
        }

        /// <summary>Whether one enemy is pursuing, investigating or fighting right now.</summary>
        public static bool IsEngaged(GameObject enemy)
        {
            if (enemy.TryGetComponent<PursuerAgent>(out var guard)) return guard.IsEngaged;
            if (enemy.TryGetComponent<SkeletonArcherAgent>(out var archer)) return archer.enabled && IsEngaged(archer);
            return enemy.TryGetComponent<HoundAgent>(out var hound) && IsEngaged(hound);
        }

        public static bool IsAlive(Component enemy) => enemy != null && enemy.gameObject.activeInHierarchy
            && (!enemy.TryGetComponent<Health>(out var health) || !health.IsDead);

        private static bool IsEngaged(SkeletonArcherAgent archer) =>
            archer.State != AmbusherState.Unaware && archer.State != AmbusherState.Dead;
        private static bool IsEngaged(HoundAgent hound) => hound.State == HoundState.Track;
        private static bool Near(Component enemy, Vector3 player, float range) =>
            (enemy.transform.position - player).sqrMagnitude <= range * range;
    }
}
