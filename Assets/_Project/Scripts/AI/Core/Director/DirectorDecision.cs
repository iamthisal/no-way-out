using System.Collections.Generic;
using UnityEngine;

namespace Lockdown.AI.Core.Director
{
    /// <summary>One scored spawn action.</summary>
    public struct SpawnOption
    {
        public SpawnKind Kind;
        public int PointId;
        public string RoomId;
        public Vector3 Position;
        public float Utility;
    }

    /// <summary>The outcome of one decision tick, with enough detail to explain it afterwards.</summary>
    public sealed class DirectorDecision
    {
        public bool Spawn;
        public SpawnOption Choice;
        public float WaitUtility;
        public string Reason = "";
        /// <summary>The best three options, best first.</summary>
        public readonly List<SpawnOption> Top = new List<SpawnOption>(4);
    }
}
