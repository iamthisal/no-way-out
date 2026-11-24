using System.Collections;
using Lockdown.AI.Core.Director;
using Lockdown.Enemies;
using Lockdown.Player;
using Lockdown.World;
using UnityEngine;

namespace Lockdown.AI.Agents
{
    /// <summary>
    /// Unity side of the Dynamic Spawning Agent: gathers percepts, lets the brain decide and carries the
    /// decision out. Enemies act only on their own senses afterwards.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpawnDirectorAgent : MonoBehaviour
    {
        [SerializeField] private DirectorSettings settings;
        [SerializeField] private GameObject guardPrefab;
        [SerializeField] private GameObject archerPrefab;
        [SerializeField] private GameObject houndPrefab;
        [SerializeField] private string safeRoomId = "";
        [SerializeField, Min(0.05f)] private float rosterInterval = 0.5f;
        [SerializeField, Min(8)] private int logCapacity = 256;

        private readonly EnemyRoster roster = new EnemyRoster();
        private PlayerObserver player;
        private AlarmStimulusRelay alarm;
        private SpawnPointSurvey survey;
        private EnemySpawner spawner;
        private GlassHoundResponse glassResponse;
        private float nextRoster;

        public DirectorBrain Brain { get; private set; }
        public DirectorLog Log { get; private set; }
        public DirectorObservation Observation { get; private set; }
        public SpawnedEnemies Spawned { get; private set; }
        public int SpawnCount { get; private set; }
        public string SafeRoomId => safeRoomId;
        public event System.Action<GameObject, SpawnOption> EnemySpawned;

        private void OnEnable() { Log = new DirectorLog(logCapacity); }

        private void Start() { TryBind(); }

        private bool TryBind()
        {
            if (player != null && player.Player != null) return true;
            Release();
            var controller = FindAnyObjectByType<FirstPersonController>();
            if (controller == null || controller.PlayerCamera == null) return false;
            var tuning = settings != null ? settings.tuning : new DirectorTuning();
            var points = FindObjectsByType<SpawnPoint>();
            // A fixed order keeps point numbers stable between runs, so logs can be compared.
            System.Array.Sort(points, (left, right) => string.CompareOrdinal(left.name, right.name));
            var routes = new MuseumRoutes();
            alarm = GetComponent<AlarmStimulusRelay>();
            player = new PlayerObserver(controller, FindAnyObjectByType<MuseumExit>(), tuning);
            survey = new SpawnPointSurvey(points, player, alarm, routes, tuning, safeRoomId);
            spawner = new EnemySpawner(guardPrefab, archerPrefab, houndPrefab, points, routes, alarm, transform);
            Spawned = new SpawnedEnemies(routes, tuning);
            Brain = new DirectorBrain(tuning);
            glassResponse = FindAnyObjectByType<GlassHoundResponse>();
            if (glassResponse != null) glassResponse.DispatchAllowed = MayDispatchHound;
            return true;
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f || !TryBind()) return;
            if (Time.time >= nextRoster)
            {
                nextRoster = Time.time + rosterInterval;
                roster.Refresh(player.Position, Brain.Tuning.engagedRange);
                if (player.CanAct) Spawned.Tick(Brain.Phase, player, rosterInterval);
            }
            Observation = player.Observe(roster, alarm != null && alarm.IsUnanswered, Time.deltaTime);
            var decision = Brain.Tick(Observation, Time.deltaTime, survey);
            if (decision == null) return;
            Log.Add(new DirectorLog.Entry
            {
                Time = Time.time, Stress = Brain.Stress, Intensity = Brain.Intensity, Target = Brain.Target,
                Phase = Brain.Phase, Observation = Observation, Decision = decision
            });
            if (decision.Spawn) Spawn(decision.Choice);
        }

        private void Spawn(SpawnOption option)
        {
            // Marked even when it fails, so a blocked point is not picked again straight away.
            survey.MarkUsed(option.PointId);
            if (!spawner.TrySpawn(option, player.Player.transform, out var enemy)) return;
            Brain.ConfirmSpawn();
            Spawned.Add(enemy);
            SpawnCount++;
            nextRoster = 0f;
            if (option.Kind == SpawnKind.Archer) StartCoroutine(WakeArcher(enemy));
            EnemySpawned?.Invoke(enemy, option);
        }

        private static IEnumerator WakeArcher(GameObject enemy)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                yield return null;
                if (enemy == null || EnemySpawner.TryWakeArcher(enemy)) yield break;
            }
        }

        private bool MayDispatchHound()
        {
            if (player == null || !player.CanAct) return false;
            roster.Refresh(player.Position, Brain.Tuning.engagedRange);
            return Brain.Permits(SpawnKind.Hound, roster.Census);
        }

        private void OnDisable() { Release(); }

        private void Release()
        {
            if (glassResponse != null) glassResponse.DispatchAllowed = null;
            glassResponse = null;
            player?.Dispose();
            player = null;
        }
    }
}
