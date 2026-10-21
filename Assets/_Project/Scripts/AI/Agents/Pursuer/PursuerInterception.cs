using UnityEngine;

namespace Lockdown.AI.Agents
{
    public delegate bool PursuerRouteMeasure(Vector3 from, Vector3 to, out float length);

    /// <summary>Observes visible movement only; evaluates predicted destinations using actual route costs.</summary>
    public sealed class PursuerInterception
    {
        public Vector3 Velocity { get; private set; }
        public Vector3 LastPosition { get; private set; }
        private float observedAt = float.NegativeInfinity;
        private bool continuous;
        public void Observe(Vector3 position, float now)
        {
            float dt = now - observedAt;
            Vector3 displacement = position - LastPosition; displacement.y = 0f;
            // Never treat a teleport or movement during an unseen interval as observed velocity.
            Velocity = continuous && dt > 0.001f && dt < 0.5f && displacement.magnitude / dt <= 8f
                ? Vector3.ClampMagnitude(displacement / dt, 7f) : Vector3.zero;
            LastPosition = position; observedAt = now; continuous = true;
        }
        public bool IsFresh(float now, float lifetime) => now - observedAt <= lifetime;
        public void LoseSight() { continuous = false; }
        public void Reset() { Velocity = Vector3.zero; observedAt = float.NegativeInfinity; continuous = false; }

        public bool TryChoose(Vector3 guard, float now, PursuerSettings settings, PursuerRouteMeasure route, out Vector3 goal)
        {
            goal = LastPosition;
            float speed = Velocity.magnitude;
            if (!IsFresh(now, settings.predictionLifetime) || speed < settings.fleeingSpeed
                || !route(guard, LastPosition, out float direct)) return false;
            float best = settings.minimumShortcutSaving;
            bool found = false;
            for (int i = 1; i <= 3; i++)
            {
                Vector3 candidate = LastPosition + Velocity * (settings.interceptLeadTime * i / 3f);
                if (!route(guard, candidate, out float guardRoute) || !route(LastPosition, candidate, out float playerRoute)) continue;
                float saving = direct + playerRoute - guardRoute;
                float lateness = Mathf.Max(0f, guardRoute / settings.chaseSpeed - playerRoute / speed);
                float score = saving - lateness * settings.chaseSpeed;
                if (lateness > 0.75f || score <= best) continue;
                best = score; goal = candidate; found = true;
            }
            return found;
        }
    }
}
