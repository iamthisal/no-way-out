using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Lockdown.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class GlassAlarm : MonoBehaviour
    {
        [SerializeField] private BreakableGlass[] glassSources = new BreakableGlass[0];
        [SerializeField, Min(0.01f)] private float durationSeconds = 15f;

        private AudioSource siren;
        private double stopTime;
        private readonly Dictionary<BreakableGlass, UnityAction> subscriptions = new Dictionary<BreakableGlass, UnityAction>();

        /// <summary>True from the moment any connected glass breaks until the siren stops.</summary>
        public bool IsRinging { get; private set; }
        /// <summary>Where the most recent glass broke. A snapshot, never the player's position.</summary>
        public Vector3 LatestPosition { get; private set; }
        /// <summary>Raised for every break, including one that restarts a ringing alarm.</summary>
        public event Action<Vector3> AlarmRaised;
        public event Action AlarmCleared;

        private void Awake()
        {
            siren = GetComponent<AudioSource>();
            siren.playOnAwake = false;
            siren.loop = true;
        }

        private void OnEnable()
        {
            foreach (var glass in glassSources)
            {
                if (glass == null || subscriptions.ContainsKey(glass)) continue;
                var source = glass;
                UnityAction callback = () => Trigger(source);
                source.OnBroken.AddListener(callback);
                subscriptions.Add(source, callback);
            }
        }

        private void OnDisable()
        {
            foreach (var entry in subscriptions)
                if (entry.Key != null) entry.Key.OnBroken.RemoveListener(entry.Value);
            subscriptions.Clear();

            StopAlarm();
        }

        private void Trigger(BreakableGlass glass)
        {
            if (!glass.IsBroken) return;
            stopTime = Time.unscaledTimeAsDouble + durationSeconds;
            if (!siren.isPlaying) siren.Play();
            IsRinging = true;
            LatestPosition = glass.transform.position;
            AlarmRaised?.Invoke(LatestPosition);
        }

        private void Update()
        {
            if (stopTime > 0d && Time.unscaledTimeAsDouble >= stopTime)
                StopAlarm();
        }

        private void StopAlarm()
        {
            stopTime = 0d;
            siren.Stop();
            if (!IsRinging) return;
            IsRinging = false;
            AlarmCleared?.Invoke();
        }
    }
}
