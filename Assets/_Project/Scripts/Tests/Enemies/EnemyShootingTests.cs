#if UNITY_EDITOR
using System.Collections;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Player;
using Lockdown.Weapons;
using Lockdown.AI.Agents;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lockdown.Tests
{
    public sealed class EnemyShootingTests
    {
        private bool previousBackground;
        private GameObject wall;

        [SetUp]
        public void Setup()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
        }

        [UnityTest]
        public IEnumerator EnemyShootsNearbyPlayerButNotThroughWallsOrAfterDeath()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var shooter = PrepareWanderingShooter();
            Assert.That(shooter, Is.Not.Null);
            shooter.GetComponent<WanderingEnemy>().enabled = false;
            var player = Object.FindFirstObjectByType<FirstPersonController>();
            var health = player.GetComponent<Health>();
            Assert.That(health, Is.Not.Null, "The player must have the shared Health component.");
            var hitAudio = player.GetComponent<AudioSource>();
            Assert.That(hitAudio, Is.Not.Null, "The player must have a local hit audio source.");
            Assert.That(hitAudio.spatialBlend, Is.Zero);
            Assert.That(hitAudio.playOnAwake, Is.False);
            Assert.That(hitAudio.isPlaying, Is.False, "No hit sound should play on spawn.");
            SetPosition(player, shooter.transform.position + Vector3.back * 2f);
            player.CaptureCursor();
            yield return new WaitForSeconds(1f);
            Assert.That(shooter.ShotsFired, Is.EqualTo(1));
            Assert.That(health.CurrentHealth, Is.EqualTo(90f));
            Assert.That(hitAudio.isPlaying, Is.True, "An enemy hit should play the player's oof sound.");
            Assert.That(shooter.TryFire(), Is.False, "Fire interval should prevent immediate repeat shots.");

            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = shooter.transform.position + Vector3.back + Vector3.up * 1.2f;
            wall.transform.localScale = new Vector3(3f, 2.4f, 0.2f);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(1.5f);
            Assert.That(shooter.ShotsFired, Is.EqualTo(1), "A wall should block shooting and damage.");
            Assert.That(health.CurrentHealth, Is.EqualTo(90f));
            Assert.That(hitAudio.isPlaying, Is.False, "Blocked enemy shots should not play hit audio.");
            Object.DestroyImmediate(wall);
            yield return new WaitForSeconds(1.1f);
            Assert.That(shooter.ShotsFired, Is.EqualTo(2));

            SetPosition(player, new Vector3(0f, 0.05f, -9.5f));
            yield return new WaitForSeconds(1.5f);
            Assert.That(shooter.ShotsFired, Is.EqualTo(2), "The enemy must not shoot outside its short range.");
            shooter.GetComponent<Health>().TakeDamage(100f);
            SetPosition(player, shooter.transform.position + Vector3.back * 2f);
            yield return new WaitForSeconds(1.5f);
            Assert.That(shooter.ShotsFired, Is.EqualTo(2), "A dead enemy must stop shooting.");
        }

        [UnityTest]
        public IEnumerator EnemyDamageStopsPlayerAndRestartRestoresEncounter()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var shooter = PrepareWanderingShooter();
            shooter.GetComponent<WanderingEnemy>().enabled = false;
            var player = Object.FindFirstObjectByType<FirstPersonController>();
            var health = player.GetComponent<Health>();
            var weapon = player.GetComponentInChildren<FirstPersonWeapon>(true);
            var vitals = player.GetComponent<PlayerVitals>();
            SetPosition(player, shooter.transform.position + Vector3.back * 2f);
            player.CaptureCursor();
            yield return null;
            Assert.That(player.GetComponent<PlayerEquipment>().TryEquipPistol(), Is.True);
            health.TakeDamage(90f);
            yield return new WaitForSeconds(1.1f);
            Assert.That(health.IsDead, Is.True);
            Assert.That(player.enabled, Is.False);
            Assert.That(player.IsCursorCaptured, Is.False);
            Assert.That(weapon.enabled, Is.False);
            Assert.That(weapon.TryFire(), Is.False);
            int shots = shooter.ShotsFired;
            yield return new WaitForSeconds(1.5f);
            Assert.That(shooter.ShotsFired, Is.EqualTo(shots), "The enemy must not keep shooting a dead player.");
            vitals.RestartEncounter();
            yield return null;
            yield return null;
            var freshPlayer = Object.FindFirstObjectByType<FirstPersonController>();
            Assert.That(freshPlayer.enabled, Is.True);
            Assert.That(freshPlayer.GetComponent<Health>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(freshPlayer.GetComponentInChildren<FirstPersonWeapon>(true).enabled, Is.True);
            Assert.That(freshPlayer.GetComponent<PlayerEquipment>().IsPistolEquipped, Is.False, "Restart should begin with empty hands.");
            Assert.That(PrepareWanderingShooter().ShotsFired, Is.Zero);
        }

        private static EnemyShooting PrepareWanderingShooter()
        {
            // This suite validates the autonomous wanderer, independently of the pursuer FSM.
            foreach (var pursuer in Object.FindObjectsByType<PursuerAgent>(FindObjectsInactive.Exclude))
                pursuer.gameObject.SetActive(false);
            return Object.FindAnyObjectByType<WanderingEnemy>().GetComponent<EnemyShooting>();
        }

        private static void SetPosition(FirstPersonController player, Vector3 position)
        {
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = position + Vector3.up * 0.05f;
            body.enabled = true;
            Physics.SyncTransforms();
        }

        [TearDown]
        public void Cleanup()
        {
            if (wall != null) Object.DestroyImmediate(wall);
            Application.runInBackground = previousBackground;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
#endif
