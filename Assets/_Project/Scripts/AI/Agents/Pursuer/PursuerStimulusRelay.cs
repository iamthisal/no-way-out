using System.Collections.Generic;
using Lockdown.AI.Core.Hound;
using Lockdown.Player;
using Lockdown.Weapons;
using Lockdown.World;
using UnityEngine;
using UnityEngine.Events;

namespace Lockdown.AI.Agents
{
    /// <summary>One scene relay connects existing gameplay to the shared noise bus.</summary>
    public sealed class PursuerStimulusRelay : MonoBehaviour
    {
        private readonly Dictionary<BreakableGlass, UnityAction> subscriptions = new Dictionary<BreakableGlass, UnityAction>();
        private FirstPersonController player;
        private FirstPersonWeapon weapon;
        private CharacterController body;
        private int shots;
        private float nextStep, nextDiscovery;
        private void OnEnable() { Discover(); }
        private void Discover()
        {
            foreach (var glass in FindObjectsByType<BreakableGlass>(FindObjectsInactive.Exclude))
            {
                if (glass.gameObject.scene != gameObject.scene || subscriptions.ContainsKey(glass)) continue;
                var source = glass;
                UnityAction action = () => HoundNoiseBus.Emit(source.transform.position, HoundSoundKind.Distraction, source.transform);
                source.OnBroken.AddListener(action); subscriptions.Add(source, action);
            }
            player = FindAnyObjectByType<FirstPersonController>();
            body = player != null ? player.GetComponent<CharacterController>() : null;
            var discoveredWeapon = player != null ? player.GetComponentInChildren<FirstPersonWeapon>(true) : null;
            if (weapon != discoveredWeapon) { weapon = discoveredWeapon; shots = weapon != null ? weapon.ShotsFired : 0; }
        }
        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (Time.time >= nextDiscovery) { nextDiscovery = Time.time + 2f; Discover(); }
            if (player == null || !player.isActiveAndEnabled) return;
            if (weapon != null && weapon.ShotsFired != shots)
            { shots = weapon.ShotsFired; HoundNoiseBus.Emit(player.transform.position, HoundSoundKind.Gunshot, player.transform); }
            if (body == null || !body.isGrounded || Time.time < nextStep) return;
            Vector3 velocity = body.velocity; velocity.y = 0f;
            if (velocity.magnitude < 0.2f) return;
            bool sprint = velocity.magnitude > 4.5f;
            nextStep = Time.time + (sprint ? 0.3f : 0.55f);
            HoundNoiseBus.Emit(player.transform.position, sprint ? HoundSoundKind.Sprint : HoundSoundKind.Walk, player.transform);
        }
        private void OnDisable()
        {
            foreach (var entry in subscriptions) if (entry.Key != null) entry.Key.OnBroken.RemoveListener(entry.Value);
            subscriptions.Clear();
        }
    }
}
