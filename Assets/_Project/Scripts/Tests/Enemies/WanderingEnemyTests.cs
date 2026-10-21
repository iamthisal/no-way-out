#if UNITY_EDITOR
using System.Collections;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Player;
using Lockdown.Weapons;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lockdown.Tests
{
    public sealed class WanderingEnemyTests
    {
        private bool previousBackground;
        private Random.State previousRandom;

        [SetUp]
        public void Setup()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            previousRandom = Random.state;
            Random.InitState(24);
        }

        [Test]
        public void HealthClampsDamageAndAnnouncesDeathOnce()
        {
            var target = new GameObject("HealthTest");
            try
            {
                var health = target.AddComponent<Health>();
                int deaths = 0;
                health.OnDeath.AddListener(() => deaths++);
                health.TakeDamage(-10f);
                health.TakeDamage(float.NaN);
                health.TakeDamage(float.PositiveInfinity);
                Assert.That(health.CurrentHealth, Is.EqualTo(100f));
                health.TakeDamage(25f);
                Assert.That(health.CurrentHealth, Is.EqualTo(75f));
                health.TakeDamage(200f);
                health.TakeDamage(25f);
                Assert.That(health.CurrentHealth, Is.Zero);
                Assert.That(health.IsDead, Is.True);
                Assert.That(deaths, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(target); }
        }

        [UnityTest]
        public IEnumerator MuseumEnemyWandersAndDiesFromFourPistolHits()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var enemies = Object.FindObjectsByType<WanderingEnemy>(FindObjectsSortMode.None);
            Assert.That(enemies.Length, Is.EqualTo(1), "The museum should contain exactly one wandering enemy.");
            var enemy = enemies[0];
            var agent = enemy.GetComponent<NavMeshAgent>();
            var health = enemy.GetComponent<Health>();
            Assert.That(agent.isOnNavMesh, Is.True);
            Vector3 start = enemy.transform.position;
            float maximumTravel = 0f;
            for (float elapsed = 0f; elapsed < 6f; elapsed += Time.deltaTime)
            {
                yield return null;
                maximumTravel = Mathf.Max(maximumTravel, Vector3.Distance(start, enemy.transform.position));
                Assert.That(agent.isOnNavMesh, Is.True);
                if (agent.hasPath && !agent.pathPending)
                    Assert.That(agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete));
            }
            Assert.That(maximumTravel, Is.GreaterThan(0.5f), "The enemy should actually move, not just pick a destination.");
            if (enemy.IsPatrolling)
            {
                // As the mission guard he walks his patrol route instead of wandering round his post.
                bool headingForStop = false;
                for (int i = 0; i < enemy.StopCount; i++) headingForStop |= Vector3.Distance(agent.destination, enemy.Stop(i)) < 0.6f;
                Assert.That(headingForStop, Is.True, "The guard walks between his patrol stops.");
            }
            else Assert.That(Vector3.Distance(agent.destination, enemy.WanderOrigin), Is.LessThanOrEqualTo(enemy.WanderRadius + 0.05f));

            var player = Object.FindFirstObjectByType<FirstPersonController>();
            var weapon = player.GetComponentInChildren<FirstPersonWeapon>(true);
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = enemy.transform.position + Vector3.back * 1.6f + Vector3.up * 0.05f;
            body.enabled = true;
            player.CaptureCursor();
            yield return null;
            Assert.That(player.GetComponent<PlayerEquipment>().TryEquipPistol(), Is.True);
            int deaths = 0;
            health.OnDeath.AddListener(() => deaths++);
            for (int i = 0; i < 4; i++)
            {
                player.PlayerCamera.transform.LookAt(enemy.transform.position + Vector3.up * 1.3f);
                Physics.SyncTransforms();
                Assert.That(weapon.TryFire(), Is.True);
                Assert.That(health.CurrentHealth, Is.EqualTo(100f - (i + 1) * 25f), "The equipped pistol must damage the actual enemy.");
                yield return new WaitForSeconds(0.3f);
            }
            Assert.That(health.IsDead, Is.True);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(agent.enabled, Is.False);
            Assert.That(enemy.GetComponent<Collider>().enabled, Is.False);
            Vector3 deathPosition = enemy.transform.position;
            yield return new WaitForSeconds(0.5f);
            Assert.That(Vector3.Distance(deathPosition, enemy.transform.position), Is.LessThan(0.01f));
            Assert.That(enemy.transform.Find("Model").localEulerAngles.x, Is.EqualTo(90f).Within(0.1f));
        }

        [TearDown]
        public void Cleanup()
        {
            Application.runInBackground = previousBackground;
            Random.state = previousRandom;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
#endif
