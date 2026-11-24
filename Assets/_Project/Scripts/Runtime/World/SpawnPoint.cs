using System;
using UnityEngine;

namespace Lockdown.World
{
    /// <summary>
    /// Marks a place where an enemy may enter. It only describes the place; the spawn director decides.
    /// The flag values match the director's own sets in Lockdown.AI.Core.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpawnPoint : MonoBehaviour
    {
        [Flags] public enum Zones { None = 0, Corridor = 1, OpenGallery = 2, Staff = 4 }
        [Flags] public enum Kinds { None = 0, Guard = 1, Archer = 2, Hound = 4, All = Guard | Archer | Hound }

        [SerializeField] private string roomId = "";
        [SerializeField] private Zones zones = Zones.None;
        [SerializeField] private Kinds allowed = Kinds.All;

        public string RoomId => roomId;
        public Zones Zone => zones;
        public Kinds Allowed => allowed;

        public void Configure(string room, Zones zone, Kinds kinds)
        {
            roomId = room;
            zones = zone;
            allowed = kinds;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = (zones & Zones.Staff) != 0 ? new Color(0.3f, 0.6f, 1f)
                : (zones & Zones.Corridor) != 0 ? new Color(0.7f, 0.4f, 1f) : new Color(0.3f, 0.9f, 0.5f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.4f, 0.35f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.4f, transform.forward * 0.8f);
        }
    }
}
