using System;
using UnityEngine;

namespace Lockdown.AI.Agents
{
    [Serializable]
    public sealed class PursuerSettings
    {
        [Min(0.1f)] public float patrolSpeed = 2f, chaseSpeed = 4f;
        [Header("Arrest first")]
        [Min(0.1f)] public float approachSpeed = 2.8f, arrestRange = 1.6f, arrestDuration = 1.5f;
        [Min(0.1f)] public float fleeingSpeed = 1.2f;
        [Header("Observed movement interception")]
        [Min(0.1f)] public float interceptLeadTime = 1.5f, predictionLifetime = 2f, replanInterval = 0.5f;
        [Min(0f)] public float minimumShortcutSaving = 0.6f;
        [Header("Aggressive retaliation")]
        [Min(0.1f)] public float combatStopDistance = 2.8f, coverDuration = 1.1f, coverCooldown = 6f;
        [Min(0.1f)] public float detectionRange = 20f;
        [Range(1f, 360f)] public float fieldOfView = 100f;
        [Min(0f)] public float sightMemoryDuration = 3f;
        [Min(0.1f)] public float hearingRange = 24f, clueLifetime = 6f;
        [Range(0f, 1f)] public float obstructedHearing = 0.5f;
        [Min(0.1f)] public float investigateTimeout = 12f, inspectionDuration = 1.5f;
        [Min(0.1f)] public float searchDuration = 14f, searchRadius = 3f;
        [Min(0.1f)] public float attackRange = 8f, aimWarning = 0.65f;
        [Min(0.05f)] public float burstInterval = 0.18f, burstCooldown = 1.5f;
        [Min(1)] public int burstSize = 3;
        [Min(0.1f)] public float alertRange = 20f, alertInterval = 1f;
    }
}
