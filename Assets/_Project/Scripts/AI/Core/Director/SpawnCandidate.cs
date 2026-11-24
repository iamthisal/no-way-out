using UnityEngine;

namespace Lockdown.AI.Core.Director
{
    /// <summary>World facts about one spawn point, measured by the Unity side for the current decision.</summary>
    public struct SpawnCandidate
    {
        public int PointId;
        public string RoomId;
        public Vector3 Position;
        public SpawnZone Zone;
        public SpawnKindSet Allowed;
        public bool InView;
        public bool HasLineOfSight;
        public bool Reachable;
        public bool ClearSpace;
        public bool InSafeRoom;
        /// <summary>Spawning here would put an enemy on the player's way to the exit.</summary>
        public bool OnExitRoute;
        /// <summary>Walking distance to the player, not straight-line distance.</summary>
        public float RouteLength;
        public float SecondsSinceUsed;
        /// <summary>Distance to the latest alarm, or infinity when there is no recent alarm.</summary>
        public float AlarmDistance;
    }
}
