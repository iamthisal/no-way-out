using Lockdown.AI.Core.Hound;
using Lockdown.Combat;
using Lockdown.Player;
using Lockdown.Weapons;
using UnityEngine;

namespace Lockdown.AI.Agents
{
    // Optional adapter on a player instance. Player and weapon source files remain independent of AI.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(FirstPersonController), typeof(CharacterController))]
    public sealed class HoundPlayerNoise : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float sprintThreshold = 5f;
        [SerializeField, Min(0.1f)] private float crouchHeight = 1.4f;
        private FirstPersonController player;
        private CharacterController body;
        private Health health;
        private FirstPersonWeapon[] weapons;
        private int[] previousShots;
        private Vector3 previousPosition;
        private double nextStep;
        private double nextWeaponRefresh;

        private void Awake()
        {
            player = GetComponent<FirstPersonController>();
            body = GetComponent<CharacterController>();
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
            nextStep = Time.timeAsDouble + 0.2;
            RefreshWeapons();
        }

        private void RefreshWeapons()
        {
            var found = GetComponentsInChildren<FirstPersonWeapon>(true);
            var counts = new int[found.Length];
            for (int i = 0; i < found.Length; i++)
            {
                counts[i] = found[i].ShotsFired;
                if (weapons == null) continue;
                for (int j = 0; j < weapons.Length; j++)
                    if (found[i] == weapons[j]) { counts[i] = previousShots[j]; break; }
            }
            weapons = found;
            previousShots = counts;
            nextWeaponRefresh = Time.timeAsDouble + 1;
        }

        private void LateUpdate()
        {
            Vector3 displacement = transform.position - previousPosition;
            previousPosition = transform.position;
            if (Time.deltaTime <= 0f || !player.isActiveAndEnabled || (health != null && health.IsDead)) return;
            if (Time.timeAsDouble >= nextWeaponRefresh) RefreshWeapons();
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] == null) continue;
                int count = weapons[i].ShotsFired;
                if (count > previousShots[i]) HoundNoiseBus.Emit(NoisePosition(), HoundSoundKind.Gunshot, transform);
                previousShots[i] = count;
            }
            displacement.y = 0;
            // Ignore teleports, airborne movement, cursor-release pauses, and pushing against a wall.
            if (!body.isGrounded || !player.IsCursorCaptured || displacement.sqrMagnitude < 0.0001f
                || displacement.sqrMagnitude > 16f || Time.timeAsDouble < nextStep) return;
            float speed = displacement.magnitude / Time.deltaTime;
            HoundSoundKind kind = body.height <= crouchHeight ? HoundSoundKind.Crouch
                : speed >= sprintThreshold ? HoundSoundKind.Sprint : HoundSoundKind.Walk;
            HoundNoiseBus.Emit(NoisePosition(), kind, transform);
            nextStep = Time.timeAsDouble + (kind == HoundSoundKind.Sprint ? 0.28 : kind == HoundSoundKind.Crouch ? 0.8 : 0.55);
        }

        private Vector3 NoisePosition() => transform.position + Vector3.up * 0.3f;
    }
}
