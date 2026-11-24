using UnityEngine;

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

        private void Awake()
        {
            siren = GetComponent<AudioSource>();
            siren.playOnAwake = false;
            siren.loop = true;
        }

        private void OnEnable()
        {
            foreach (var glass in glassSources)
                if (glass != null) glass.OnBroken.AddListener(Trigger);
        }

        private void OnDisable()
        {
            foreach (var glass in glassSources)
                if (glass != null) glass.OnBroken.RemoveListener(Trigger);

            StopAlarm();
        }

        private void Trigger()
        {
            stopTime = Time.unscaledTimeAsDouble + durationSeconds;
            if (!siren.isPlaying) siren.Play();
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
        }
    }
}
