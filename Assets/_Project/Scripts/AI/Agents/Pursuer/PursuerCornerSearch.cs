using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lockdown.AI.Agents
{
    /// <summary>Ranks bounded escape hypotheses from an observation and map geometry, never a hidden target.</summary>
    public static class PursuerCornerSearch
    {
        private struct Candidate { public Vector3 Point; public float Score; }
        public static List<Vector3> Build(Vector3 guard, Vector3 lastSeen, Vector3 observedVelocity,
            PursuerSettings settings, PursuerRouteMeasure route, Func<Vector3, Vector3, bool> screened)
        {
            var ranked = new List<Candidate>();
            float speed = observedVelocity.magnitude;
            if (speed >= settings.fleeingSpeed)
            {
                Vector3 heading = Vector3.ProjectOnPlane(observedVelocity, Vector3.up).normalized;
                float lead = Mathf.Clamp(speed * settings.interceptLeadTime, 2f, settings.cornerSearchDistance);
                foreach (float angle in new[] { 0f, -45f, 45f, -90f, 90f })
                for (int step = 1; step <= 3; step++)
                {
                    Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * heading;
                    Vector3 point = lastSeen + direction * (lead * step / 3f);
                    if (!route(lastSeen, point, out float playerRoute) || playerRoute > lead * 1.8f
                        || !route(guard, point, out float guardRoute) || guardRoute > settings.chaseSpeed * settings.lostTravelTimeout) continue;
                    // Heading is evidence; occlusion suggests a corner; route cost rewards a practical shortcut.
                    float score = Vector3.Dot(heading, direction) * 4f + (screened(lastSeen, point) ? 3f : 0f)
                        + Vector3.Distance(lastSeen, point) * 0.15f - playerRoute * 0.15f - guardRoute * 0.25f;
                    ranked.Add(new Candidate { Point = point, Score = score });
                }
            }
            ranked.Sort((a, b) => b.Score.CompareTo(a.Score));
            var result = new List<Vector3>();
            foreach (var candidate in ranked)
            {
                if (result.Exists(point => Vector3.Distance(point, candidate.Point) < 1.5f)) continue;
                result.Add(candidate.Point);
                if (result.Count == 3) break;
            }
            // Even an unhelpful heading must not discard the actual last confirmed location.
            if (!result.Exists(point => Vector3.Distance(point, lastSeen) < 0.2f)) result.Add(lastSeen);
            return result;
        }
    }
}
