using System;
using UnityEngine;

namespace Lockdown.AI.Core.Hound
{
    public enum HoundSoundKind { Custom, Walk, Sprint, Crouch, Gunshot, Distraction }

    // A snapshot of where and when a noise happened, never a moving target reference.
    public readonly struct HoundSound
    {
        public readonly Vector3 Position;
        public readonly HoundSoundKind Kind;
        public readonly float Loudness;
        public readonly float Radius;
        public readonly double EmittedAt;

        public HoundSound(Vector3 position, HoundSoundKind kind, float loudness, float radius, double emittedAt)
        {
            Position = position;
            Kind = kind;
            Loudness = loudness;
            Radius = radius;
            EmittedAt = emittedAt;
        }

        public bool IsValid => IsFinite(Position.x) && IsFinite(Position.y) && IsFinite(Position.z)
            && IsFinite(Loudness) && Loudness > 0f && IsFinite(Radius) && Radius > 0f
            && !double.IsNaN(EmittedAt) && !double.IsInfinity(EmittedAt);

        public HoundSound Attenuated(float multiplier) => new HoundSound(Position, Kind,
            Loudness * multiplier, Radius * multiplier, EmittedAt);

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static class HoundSoundProfiles
    {
        // These relative values match the team's HTML hound reference.
        public static HoundSound Create(HoundSoundKind kind, Vector3 position, double now)
        {
            switch (kind)
            {
                case HoundSoundKind.Gunshot: return new HoundSound(position, kind, 12f, 48f, now);
                case HoundSoundKind.Sprint: return new HoundSound(position, kind, 5f, 30f, now);
                case HoundSoundKind.Walk: return new HoundSound(position, kind, 0.8f, 10f, now);
                case HoundSoundKind.Crouch: return new HoundSound(position, kind, 0.08f, 3f, now);
                case HoundSoundKind.Distraction: return new HoundSound(position, kind, 6f, 24f, now);
                default: return new HoundSound(position, HoundSoundKind.Custom, 6f, 12f, now);
            }
        }
    }
}
