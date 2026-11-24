using System;
using UnityEngine;

namespace Lockdown.AI.Core.Director
{
    /// <summary>Every hand-tuned number the director uses, kept in one place so it can be justified and tested.</summary>
    [Serializable]
    public sealed class DirectorTuning
    {
        [Header("Instant stress weights (sum to 1)")]
        [Range(0f, 1f)] public float damageWeight = 0.35f;
        [Range(0f, 1f)] public float injuryWeight = 0.25f;
        [Range(0f, 1f)] public float engagedWeight = 0.25f;
        [Range(0f, 1f)] public float seenWeight = 0.15f;
        [Min(0.1f)] public float damageHalfLife = 5f;
        [Min(0.1f)] public float noiseHalfLife = 5f;
        [Min(0.1f)] public float seenHalfLife = 1f;
        [Min(1)] public int engagedForFullStress = 4;
        [Min(1f)] public float engagedRange = 25f;

        [Header("Intensity")]
        [Min(0.01f)] public float riseRate = 1.5f;
        [Min(0.01f)] public float fallRate = 0.08f;
        [Min(0.02f)] public float intensityInterval = 0.25f;

        [Header("Pacing")]
        [Range(0f, 1f)] public float baseTarget = 0.35f;
        [Range(0f, 1f)] public float lootTarget = 0.3f;
        [Range(0f, 1f)] public float peakEnter = 0.75f;
        [Range(0f, 1f)] public float peakExit = 0.6f;
        [Min(0.1f)] public float peakMaximum = 20f;
        [Min(0f)] public float relaxMinimum = 25f;
        [Range(0f, 1f)] public float relaxExit = 0.3f;

        [Header("Decision")]
        [Min(0.1f)] public float decisionInterval = 2f;
        [Min(0f)] public float spawnCooldown = 10f;
        [Min(0f)] public float openingGrace = 20f;
        [Range(0f, 1f)] public float waitUtility = 0.15f;
        [Range(0f, 1f)] public float threatWeight = 0.4f;
        [Range(0f, 1f)] public float fitWeight = 0.35f;
        [Range(0f, 1f)] public float pointWeight = 0.25f;
        [Range(0f, 1f)] public float alarmBonus = 0.2f;
        [Range(0f, 1f)] public float guardThreat = 0.8f;
        [Range(0f, 1f)] public float archerThreat = 0.7f;
        [Range(0f, 1f)] public float houndThreat = 0.6f;

        [Header("Population caps")]
        [Min(0)] public int maximumTotal = 6;
        [Min(0)] public int maximumGuards = 2;
        [Min(0)] public int maximumArchers = 2;
        [Min(0)] public int maximumHounds = 4;

        [Header("Spawn points")]
        [Min(0f)] public float minimumRoute = 8f;
        [Min(0f)] public float comfortMinimum = 15f;
        [Min(0f)] public float comfortMaximum = 25f;
        [Min(0f)] public float maximumRoute = 40f;
        [Min(0.1f)] public float noveltySeconds = 30f;
        [Min(0.1f)] public float alarmRelevanceDistance = 12f;
        [Range(0f, 1f)] public float woundedHealth = 0.3f;
        [Min(1f)] public float exitRouteSlack = 1.15f;

        [Header("Despawning")]
        [Min(0f)] public float despawnRoute = 45f;
        [Min(0f)] public float despawnUnseenSeconds = 20f;
    }
}
