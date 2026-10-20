using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lockdown.AI.Core.Mummy
{
    public enum MummyState { Dormant, Patrol, Chase, Investigate, Search, Down, Dead }

    // Level query the decision layer needs; the Unity agent answers it with NavMesh samples.
    public interface IMummyWorld
    {
        bool TryChooseSearchPoint(Vector3 around, int index, out Vector3 point);
    }

    public struct MummySenses
    {
        public Vector3 Position;
        public bool SeesPlayer;
        public Vector3 PlayerPosition;
        public bool ArrivedAtGoal;
        public bool NavigationStalled;
        public bool HeardNoise;
        public Vector3 NoisePosition;
    }

    public sealed class MummySettings
    {
        // Out of sight this long during a chase, he stops following and searches.
        public float LoseSightGrace = 2f;
        public float SearchDuration = 14f;
        public float InvestigateSearchDuration = 8f;
    }

    // The Pursuer design from the prototype, for the Egypt gallery's mummies: patrol a route,
    // chase on sight, follow the last known position, search around it, then return to patrol.
    // Noise and alerts from other mummies send him to investigate.
    public sealed class MummyBrain
    {
        private readonly List<Vector3> route;
        private readonly MummySettings settings;
        private float unseen;
        private float searchLeft;
        private int searchIndex;
        private Vector3 searchCentre;

        public MummyState State { get; private set; } = MummyState.Dormant;
        public Vector3 Goal { get; private set; }
        public Vector3 LastKnown { get; private set; }
        public bool HasContact { get; private set; }
        public int PatrolIndex { get; private set; }
        // True for the one tick in which he first spots the player, so the agent can alert the others.
        public bool SpottedThisTick { get; private set; }
        public MummySettings Settings => settings;

        public MummyBrain(IEnumerable<Vector3> patrolRoute, MummySettings settings = null)
        {
            route = new List<Vector3>(patrolRoute ?? Array.Empty<Vector3>());
            this.settings = settings ?? new MummySettings();
        }

        public void Wake(Vector3 position)
        {
            if (State != MummyState.Dormant) return;
            ReturnToPatrol(position);
        }

        public void KnockDown()
        {
            if (State != MummyState.Dead) State = MummyState.Down;
        }

        // Back on his feet he hunts from where he last saw the player; without contact he patrols.
        public void GetUp(Vector3 position)
        {
            if (State != MummyState.Down) return;
            if (HasContact) StartSearch(LastKnown, settings.SearchDuration);
            else ReturnToPatrol(position);
        }

        public void Kill() => State = MummyState.Dead;

        // Another mummy saw the player: investigate unless already chasing.
        public void Alert(Vector3 position)
        {
            if (State == MummyState.Patrol || State == MummyState.Search || State == MummyState.Investigate)
                Investigate(position);
        }

        public void Tick(in MummySenses senses, float deltaTime, IMummyWorld world)
        {
            SpottedThisTick = false;
            if (State == MummyState.Dormant || State == MummyState.Down || State == MummyState.Dead || deltaTime <= 0f) return;
            if (senses.SeesPlayer)
            {
                LastKnown = senses.PlayerPosition;
                HasContact = true;
                unseen = 0f;
                if (State != MummyState.Chase) { State = MummyState.Chase; SpottedThisTick = true; }
            }
            else unseen += deltaTime;
            if (!senses.SeesPlayer && senses.HeardNoise && State != MummyState.Chase) Investigate(senses.NoisePosition);

            switch (State)
            {
                case MummyState.Chase:
                    Goal = senses.SeesPlayer ? senses.PlayerPosition : LastKnown;
                    if (!senses.SeesPlayer && (unseen > settings.LoseSightGrace || senses.ArrivedAtGoal || senses.NavigationStalled))
                        StartSearch(LastKnown, settings.SearchDuration);
                    break;
                case MummyState.Investigate:
                    if (senses.ArrivedAtGoal || senses.NavigationStalled) StartSearch(Goal, settings.InvestigateSearchDuration);
                    break;
                case MummyState.Search:
                    searchLeft -= deltaTime;
                    if (searchLeft <= 0f) ReturnToPatrol(senses.Position);
                    else if (senses.ArrivedAtGoal || senses.NavigationStalled) NextSearchPoint(world);
                    break;
                case MummyState.Patrol:
                    if (route.Count == 0) { Goal = senses.Position; break; }
                    if (senses.ArrivedAtGoal || senses.NavigationStalled)
                    {
                        PatrolIndex = (PatrolIndex + 1) % route.Count;
                        Goal = route[PatrolIndex];
                    }
                    break;
            }
        }

        private void Investigate(Vector3 position)
        {
            State = MummyState.Investigate;
            Goal = position;
        }

        private void StartSearch(Vector3 centre, float duration)
        {
            State = MummyState.Search;
            searchCentre = centre;
            searchLeft = duration;
            searchIndex = 0;
            Goal = centre;
        }

        private void NextSearchPoint(IMummyWorld world)
        {
            if (world != null && world.TryChooseSearchPoint(searchCentre, searchIndex++, out var point)) Goal = point;
            else Goal = searchCentre;
        }

        private void ReturnToPatrol(Vector3 position)
        {
            State = MummyState.Patrol;
            if (route.Count == 0) { Goal = position; return; }
            // Rejoin the route at the nearest point rather than walking back to the start.
            float best = float.PositiveInfinity;
            for (int i = 0; i < route.Count; i++)
            {
                float distance = (route[i] - position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                PatrolIndex = i;
            }
            Goal = route[PatrolIndex];
        }
    }
}
