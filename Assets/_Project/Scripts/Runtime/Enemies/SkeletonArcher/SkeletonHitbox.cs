using Lockdown.Interfaces;
using UnityEngine;

namespace Lockdown.Enemies
{
    public enum SkeletonRegion { Body, Head, BowArm, Gear }

    // One rigid bone piece. Weapons find this before the root Health, so hits keep their location.
    [DisallowMultipleComponent]
    public sealed class SkeletonHitbox : MonoBehaviour, IDamageable
    {
        [SerializeField] private SkeletonRegion region;
        [SerializeField] private SkeletonBody body;

        public SkeletonRegion Region => region;
        public SkeletonBody Body => body;

        public void TakeDamage(float amount)
        {
            if (body != null) body.ReceiveHit(this, amount);
        }
    }
}
