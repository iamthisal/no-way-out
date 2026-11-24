using System;
using Lockdown.AI.Core.Director;
using Lockdown.AI.Core.Hound;
using Lockdown.Combat;
using Lockdown.Player;
using Lockdown.World;
using UnityEngine;

namespace Lockdown.AI.Agents
{
    /// <summary>Measures the player for the director: damage, health, noise, loot and whether play is running.</summary>
    public sealed class PlayerObserver : IDisposable
    {
        private readonly DecayingMeter damage, noise, seen;
        private readonly Health health;
        private readonly LootInventory inventory;
        private readonly PlayerVitals vitals;
        private readonly MuseumExit exit;
        private float lastHealth;

        public FirstPersonController Player { get; }
        public Transform Exit => exit != null ? exit.transform : null;
        public Vector3 Position => Player.transform.position;
        public Camera View => Player.PlayerCamera;

        public PlayerObserver(FirstPersonController player, MuseumExit exit, DirectorTuning tuning)
        {
            Player = player;
            this.exit = exit;
            health = player.GetComponent<Health>();
            inventory = player.GetComponent<LootInventory>();
            vitals = player.GetComponent<PlayerVitals>();
            damage = new DecayingMeter(tuning.damageHalfLife);
            noise = new DecayingMeter(tuning.noiseHalfLife);
            seen = new DecayingMeter(tuning.seenHalfLife);
            if (health != null) { lastHealth = health.CurrentHealth; health.OnDamaged.AddListener(OnDamaged); }
            HoundNoiseBus.NoiseEmitted += OnNoise;
        }

        public void Dispose()
        {
            if (health != null) health.OnDamaged.RemoveListener(OnDamaged);
            HoundNoiseBus.NoiseEmitted -= OnNoise;
        }

        public bool CanAct => Time.timeScale > 0f && Player != null && Player.gameObject.activeInHierarchy
            && (health == null || !health.IsDead) && (vitals == null || (!vitals.IsCaptured && !vitals.HasEscaped));

        public DirectorObservation Observe(EnemyRoster roster, bool alarmUnanswered, float deltaTime)
        {
            damage.Tick(deltaTime); noise.Tick(deltaTime); seen.Tick(deltaTime);
            if (roster.PlayerSeen) seen.RaiseTo(1f);
            return new DirectorObservation
            {
                CanAct = CanAct,
                RecentDamage = damage.Value,
                HealthFraction = health == null ? 1f : health.CurrentHealth / health.MaxHealth,
                EngagedEnemies = roster.Engaged,
                Seen = seen.Value,
                Noise = noise.Value,
                LootFraction = exit == null || exit.RequiredLootCount == 0 ? 0f
                    : (float)exit.CollectedLootCount(inventory) / exit.RequiredLootCount,
                AlarmUnanswered = alarmUnanswered,
                Census = roster.Census
            };
        }

        private void OnDamaged()
        {
            damage.Add((lastHealth - health.CurrentHealth) / health.MaxHealth);
            lastHealth = health.CurrentHealth;
        }

        private void OnNoise(HoundSound sound, Transform source)
        {
            if (source == null || Player == null || !source.IsChildOf(Player.transform)) return;
            noise.RaiseTo(sound.Kind == HoundSoundKind.Gunshot ? 1f : sound.Kind == HoundSoundKind.Sprint ? 0.7f
                : sound.Kind == HoundSoundKind.Walk ? 0.15f : 0f);
        }
    }
}
