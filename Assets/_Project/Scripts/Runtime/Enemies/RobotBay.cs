using Lockdown.World;
using UnityEngine;

namespace Lockdown.Enemies
{
    /// <summary>
    /// The robots' way in and out of the Security room. Their door stands open while any robot is away
    /// from its charger, and an invisible barrier in the doorway keeps the player from slipping through.
    /// The barrier is on the Ignore Raycast layer, so shots and sight still pass.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RobotBay : MonoBehaviour
    {
        [SerializeField] private Door door;
        [SerializeField] private Collider barrier;
        [SerializeField] private SecurityRobot[] robots = new SecurityRobot[0];
        private bool wereAway;

        public Door BayDoor => door;
        public bool RobotsAway
        {
            get
            {
                foreach (var robot in robots) if (robot != null && robot.IsAway) return true;
                return false;
            }
        }

        // Moves the door only when the robots leave or all come home, so nothing else is overridden.
        private void Update()
        {
            bool away = RobotsAway;
            if (away != wereAway && door != null) door.SetOpen(away);
            wereAway = away;
            if (barrier != null) barrier.enabled = away || (door != null && door.IsMoving && !door.IsOpen);
        }
    }
}
