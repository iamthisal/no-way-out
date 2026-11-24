#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Lockdown.AI.Core.Director;
using Lockdown.Combat;
using Lockdown.Player;
using Lockdown.World;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace Lockdown.Tests.Director
{
    /// <summary>
    /// Repeatable 90-second scenarios with the saved settings. Run by name; it measures rather than asserts behaviour.
    /// Writes Logs/DirectorScenarios.txt and one CSV per scenario.
    /// </summary>
    [Explicit]
    public sealed class DirectorScenarioReport
    {
        private const float Seconds = 90f;
        private const string Folder = "Logs/Director";

        [TearDown]
        public void TearDown() { Time.timeScale = 1f; }

        [UnityTest, Timeout(300000)] public IEnumerator CalmWithoutLoot() { yield return Run("calm-no-loot", 0f, 0f); }
        [UnityTest, Timeout(300000)] public IEnumerator CalmWithAllLoot() { yield return Run("calm-all-loot", 1f, 0f); }
        [UnityTest, Timeout(300000)] public IEnumerator UnderSteadyAttack() { yield return Run("under-attack", 0f, 9f); }

        private static IEnumerator Run(string name, float loot, float damageEveryFiveSeconds)
        {
            yield return MuseumFixture.Load();
            // Far corner facing the wall: nothing sees the player unless it walks up to them.
            MuseumFixture.MovePlayer(new Vector3(-14.6f, 0.05f, 3.6f), Quaternion.Euler(0f, 270f, 0f));
            if (loot > 0f) GiveLoot();
            var director = MuseumFixture.Director;
            var health = MuseumFixture.Player.GetComponent<Health>();
            var spawns = new StringBuilder();
            float start = Time.time, peak = 0f, nextHit = Time.time + 5f;
            director.EnemySpawned += (enemy, option) =>
                spawns.Append($"  {Time.time - start:F0}s {option.Kind} at {option.RoomId} (utility {option.Utility:F2})\n");
            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Director.Decision", 20000);
            Time.timeScale = 3f;
            while (Time.time - start < Seconds && director.Observation.CanAct | Time.time - start < 1f)
            {
                if (damageEveryFiveSeconds > 0f && Time.time >= nextHit)
                { nextHit = Time.time + 5f; health.TakeDamage(damageEveryFiveSeconds); }
                peak = Mathf.Max(peak, director.Brain != null ? director.Brain.Intensity : 0f);
                yield return null;
            }
            Time.timeScale = 1f;
            var costs = recorder.ToArray().Where(sample => sample.Count > 0).Select(sample => sample.Value / 1000.0).ToArray();
            recorder.Dispose();
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/" + name + ".csv", director.Log.ToCsv());
            var phases = Enumerable.Range(0, director.Log.Count).Select(i => director.Log[i].Phase).Distinct();
            string text = $"{name}: {Time.time - start:F0}s, spawned {director.SpawnCount}, peak intensity {peak:F2}, "
                + $"final target {director.Brain.Target:F2}, loot {director.Observation.LootFraction:P0}, "
                + $"phases {string.Join("/", phases)}, health {health.CurrentHealth:F0}, "
                + (costs.Length > 0 ? $"Director.Decision {costs.Length} frames mean {costs.Average():F1} us max {costs.Max():F1} us" : "no marker samples")
                + "\n" + spawns;
            File.AppendAllText(Folder + "/DirectorScenarios.txt", text);
            Debug.Log(text);
            Assert.That(director.Log.Count, Is.GreaterThan(0));
        }

        // The inventory only accepts items through interaction, so the report fills it directly.
        private static void GiveLoot()
        {
            var inventory = MuseumFixture.Player.GetComponent<LootInventory>();
            var add = typeof(LootInventory).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var item in Object.FindObjectsByType<LootItem>())
                add.Invoke(inventory, new object[] { item.ItemName, item.Value, item.transform });
        }
    }
}
#endif
