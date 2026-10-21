using Lockdown.AI.Core.Hound;
using UnityEngine;

namespace Lockdown.AI.Agents
{
    [DisallowMultipleComponent]
    public sealed class HoundNoiseEmitter : MonoBehaviour
    {
        [SerializeField] private HoundSoundKind kind = HoundSoundKind.Distraction;
        [SerializeField] private bool emitOnCollision;
        [SerializeField, Min(0.1f)] private float minimumImpactSpeed = 2f;
        [SerializeField, Min(0.1f)] private float cooldown = 0.5f;
        private double nextEmission;

        [ContextMenu("Emit Noise")]
        public void Emit()
        {
            if (!isActiveAndEnabled || !Application.isPlaying || Time.timeScale <= 0f
                || Time.timeAsDouble < nextEmission) return;
            HoundNoiseBus.Emit(transform.position, kind, transform);
            nextEmission = Time.timeAsDouble + cooldown;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (emitOnCollision && collision.relativeVelocity.sqrMagnitude >= minimumImpactSpeed * minimumImpactSpeed) Emit();
        }
    }
}
