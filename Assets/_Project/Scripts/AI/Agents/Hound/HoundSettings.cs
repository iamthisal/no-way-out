using Lockdown.AI.Core.Hound;
using UnityEngine;

namespace Lockdown.AI.Agents
{
    [CreateAssetMenu(menuName = "No Way Out/Hound Settings")]
    public sealed class HoundSettings : ScriptableObject
    {
        [Header("Sound memory and utility")]
        [Range(1, 256)] public int memoryCapacity = 64;
        [Min(0.01f)] public float decayRate = 0.55f;
        [Min(0.1f)] public float distanceScale = 5f;
        [Min(0.001f)] public float memoryFloor = 0.08f;
        [Min(0.1f)] public float maximumMemoryAge = 12f;
        [Min(0.01f)] public float trackingThreshold = 1.2f;
        [Min(1f)] public float targetSwitchRatio = 1.3f;
        [Min(0.05f)] public float decisionInterval = 0.5f;
        [Range(0f, 1f)] public float obstructedSoundMultiplier;

        [Header("Movement and investigation")]
        [Min(0.1f)] public float wanderSpeed = 2.4f;
        [Min(0.1f)] public float trackingSpeed = 4.8f;
        [Min(1f)] public float wanderRadius = 6f;
        [Min(0f)] public float wanderPause = 1.2f;
        [Min(0.1f)] public float sniffDuration = 3f;
        [Min(0.1f)] public float searchDuration = 6f;
        [Min(0.5f)] public float searchRadius = 2.5f;
        [Min(0.5f)] public float stuckTimeout = 2.5f;

        [Header("Bite")]
        [Min(0.1f)] public float biteRange = 1.45f;
        [Min(0.1f)] public float biteDamage = 12f;
        [Min(0.1f)] public float biteCooldown = 1.2f;

        public HoundMemory CreateMemory() => new HoundMemory(memoryCapacity, decayRate, distanceScale,
            memoryFloor, maximumMemoryAge);

        public HoundDecision CreateDecision(HoundMemory memory) => new HoundDecision(memory,
            trackingThreshold, targetSwitchRatio, decisionInterval, sniffDuration, searchDuration);

        private void OnValidate()
        {
            trackingThreshold = Mathf.Max(memoryFloor, trackingThreshold);
            trackingSpeed = Mathf.Max(wanderSpeed, trackingSpeed);
        }
    }
}
