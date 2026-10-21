using Lockdown.AI.Core.Hound;
using Lockdown.World;
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
        private BreakableGlass breakable;

        private void OnEnable()
        {
            nextEmission = 0;
            breakable = GetComponentInParent<BreakableGlass>();
            if (breakable != null) breakable.OnBroken.AddListener(EmitSmash);
        }

        private void OnDisable()
        {
            if (breakable != null) breakable.OnBroken.RemoveListener(EmitSmash);
        }

        private void EmitSmash()
        {
            // A real break must not be suppressed by a recent collision's cooldown.
            HoundNoiseBus.Emit(transform.position, HoundSoundKind.Distraction, transform);
            nextEmission = Time.timeAsDouble + cooldown;
        }

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
