using System.Collections;
using System.Collections.Generic;
using Lockdown.AI.Agents;
using Lockdown.AI.Core.Hound;
using Lockdown.Combat;
using Lockdown.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using Lockdown.Player;
using Lockdown.Weapons;
#endif

namespace Lockdown.Tests.Hound
{
    // No graphical hound prefab or museum scene is required by these tests.
    public sealed class HoundAStarRuntimeTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly Vector3 origin = new Vector3(1000, 0, 1000);
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private HoundNavigation navigation;
        private GameObject actor;
        private float previousTimeScale;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1;
            Box("Floor", origin + Vector3.down * 0.25f, new Vector3(16, 0.5f, 16));
            var sources = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(16, 0.5f, 16),
                    transform = Matrix4x4.TRS(origin + Vector3.down * 0.25f, Quaternion.identity, Vector3.one) }
            };
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(origin, new Vector3(20, 8, 20)), Vector3.zero, Quaternion.identity);
            instance = NavMesh.AddNavMeshData(data);
            actor = new GameObject("HoundTest");
            objects.Add(actor);
            actor.transform.position = origin + Vector3.left * 4;
            var agent = actor.AddComponent<NavMeshAgent>();
            agent.radius = 0.25f;
            agent.height = 1.2f;
            agent.stoppingDistance = 0.15f;
            navigation = actor.AddComponent<HoundNavigation>();
            yield return null;
            Assert.That(navigation.BindToNavMesh(), Is.True);
        }

        private GameObject Box(string name, Vector3 position, Vector3 size)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.position = position;
            item.transform.localScale = size;
            objects.Add(item);
            return item;
        }

        [UnityTest]
        public IEnumerator DirectRouteHasExpectedCostAndMoves()
        {
            Assert.That(navigation.MoveTo(origin + Vector3.right * 4, 3), Is.True);
            Assert.That(navigation.RouteCost, Is.EqualTo(8).Within(0.1));
            Assert.That(navigation.Route.Count, Is.EqualTo(2));
            for (float elapsed = 0; elapsed < 1f; elapsed += Time.deltaTime) { navigation.Status(); yield return null; }
            Assert.That(actor.transform.position.x, Is.GreaterThan(origin.x - 3.9f), "Route status: " + navigation.Status());
        }

        [UnityTest]
        public IEnumerator SolidHealthObjectRequiresDetour()
        {
            var wall = Box("UnbrokenDisplay", origin + Vector3.up, new Vector3(0.4f, 2, 4));
            wall.AddComponent<Health>();
            Physics.SyncTransforms();
            Assert.That(navigation.MoveTo(origin + Vector3.right * 4, 3), Is.True);
            Assert.That(navigation.RouteCost, Is.GreaterThan(8));
            bool aroundEnd = false;
            foreach (var point in navigation.Route) if (Mathf.Abs(point.z - origin.z) > 2.2f) aroundEnd = true;
            Assert.That(aroundEnd, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FollowsDetourToArrivalWithoutCrossingWall()
        {
            Box("Wall", origin + Vector3.up, new Vector3(0.4f, 2, 4));
            Physics.SyncTransforms();
            Assert.That(navigation.MoveTo(origin + Vector3.right * 4, 4.8f), Is.True);
            bool arrived = false;
            for (float elapsed = 0; elapsed < 12f; elapsed += Time.deltaTime)
            {
                var status = navigation.Status();
                Assert.That(status, Is.Not.EqualTo(HoundRouteStatus.Blocked));
                Vector3 p = actor.transform.position - origin;
                Assert.That(Mathf.Abs(p.x) < 0.19f && Mathf.Abs(p.z) < 1.99f, Is.False);
                if (status == HoundRouteStatus.Arrived) { arrived = true; break; }
                yield return null;
            }
            Assert.That(arrived, Is.True);
            Assert.That(Vector3.Distance(actor.transform.position, origin + Vector3.right * 4), Is.LessThan(0.4f));
        }

        [UnityTest]
        public IEnumerator SoundAbovePedestalUsesAccessibleApproach()
        {
            Box("Pedestal", origin + Vector3.up * 0.5f, new Vector3(2, 1, 2));
            Physics.SyncTransforms();
            Assert.That(navigation.MoveTo(origin + Vector3.up * 1.2f, 3), Is.True);
            Assert.That(Vector3.Distance(navigation.Destination, origin), Is.GreaterThan(1.2f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClosingDoorReplansAroundIt()
        {
            Assert.That(navigation.MoveTo(origin + Vector3.right * 4, 3), Is.True);
            Box("Door", origin + Vector3.up, new Vector3(0.4f, 2, 4));
            Physics.SyncTransforms();
            bool detoured = false;
            for (float elapsed = 0; elapsed < 3f; elapsed += Time.deltaTime)
            {
                navigation.Status();
                foreach (var point in navigation.Route)
                    if (Mathf.Abs(point.z - origin.z) > 2.2f) detoured = true;
                if (detoured) break;
                yield return null;
            }
            Assert.That(detoured, Is.True);
            Assert.That(navigation.IsTravelling, Is.True);
        }

        [UnityTest]
        public IEnumerator SealedDoorwayAndOffMeshGoalFailSafely()
        {
            Box("SealedDoorway", origin + Vector3.up, new Vector3(0.4f, 2, 18));
            Physics.SyncTransforms();
            Assert.That(navigation.MoveTo(origin + Vector3.right * 4, 3), Is.False);
            Assert.That(navigation.Status(), Is.EqualTo(HoundRouteStatus.Blocked));
            Assert.That(navigation.MoveTo(origin + Vector3.right * 50, 3), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DistractionThenGunshotChangesAStarDestination()
        {
            var dog = actor.AddComponent<HoundAgent>();
            yield return null;
            HoundNoiseBus.Emit(origin + Vector3.forward * 3 + Vector3.up * 0.3f, HoundSoundKind.Distraction);
            yield return new WaitForSeconds(0.6f);
            Assert.That(dog.State, Is.EqualTo(HoundState.Track));
            Assert.That(navigation.Route.Count, Is.GreaterThanOrEqualTo(2));
            Vector3 next = origin + Vector3.back * 3;
            HoundNoiseBus.Emit(next + Vector3.up * 0.3f, HoundSoundKind.Gunshot);
            yield return new WaitForSeconds(0.6f);
            Assert.That(dog.CurrentClue.Sound.Kind, Is.EqualTo(HoundSoundKind.Gunshot));
            Assert.That(Vector3.Distance(navigation.Destination, next), Is.LessThan(0.2f));
        }

        [UnityTest]
        public IEnumerator RealSmashEmitsOnceAndStartsInvestigation()
        {
            var dog = actor.AddComponent<HoundAgent>();
            GameObject vase = Box("Vase", origin + Vector3.up * 0.9f, new Vector3(0.4f, 0.8f, 0.4f));
            Health health = vase.AddComponent<Health>();
            vase.AddComponent<BreakableGlass>();
            vase.AddComponent<HoundNoiseEmitter>();
            yield return null;
            health.TakeDamage(100);
            Assert.That(dog.GetComponent<HoundHearing>().Memory.Count, Is.EqualTo(1));
            Assert.That(dog.GetComponent<HoundHearing>().Memory.Latest.Sound.Kind, Is.EqualTo(HoundSoundKind.Distraction));
            yield return new WaitForSeconds(0.6f);
            Assert.That(dog.State, Is.EqualTo(HoundState.Track));
            Assert.That(navigation.Route.Count, Is.GreaterThanOrEqualTo(2));
        }

        [UnityTest]
        public IEnumerator DeathStopsAStarMovement()
        {
            var dog = actor.AddComponent<HoundAgent>();
            yield return null;
            HoundNoiseBus.Emit(origin + Vector3.up * 0.3f, HoundSoundKind.Gunshot);
            yield return new WaitForSeconds(0.6f);
            dog.GetComponent<Health>().TakeDamage(100);
            Vector3 stopped = actor.transform.position;
            yield return new WaitForSeconds(0.2f);
            Assert.That(dog.State, Is.EqualTo(HoundState.Dead));
            Assert.That(navigation.IsTravelling, Is.False);
            Assert.That(actor.transform.position, Is.EqualTo(stopped));
        }

#if UNITY_EDITOR
        private FirstPersonController SpawnPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/FirstPersonPlayer.prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject player = Object.Instantiate(prefab, origin + Vector3.right * 4, Quaternion.identity);
            objects.Add(player);
            player.AddComponent<HoundPlayerNoise>();
            return player.GetComponent<FirstPersonController>();
        }

        [UnityTest]
        public IEnumerator ActualWeaponHitsReduceHealthAndKillHound()
        {
            var dog = actor.AddComponent<HoundAgent>();
            var body = actor.AddComponent<CapsuleCollider>();
            body.center = Vector3.up * 0.6f;
            body.height = 1.2f;
            body.radius = 0.25f;
            var player = SpawnPlayer();
            yield return null;
            player.CaptureCursor();
            yield return null;
            Assert.That(player.GetComponent<PlayerEquipment>().TryEquipPistol(), Is.True);
            var weapon = player.GetComponentInChildren<FirstPersonWeapon>();
            Health health = dog.GetComponent<Health>();
            for (int shot = 1; shot <= 4; shot++)
            {
                player.PlayerCamera.transform.LookAt(actor.transform.position + Vector3.up * 0.6f);
                Physics.SyncTransforms();
                Assert.That(weapon.TryFire(), Is.True);
                Assert.That(health.CurrentHealth, Is.EqualTo(100 - shot * 25));
                yield return new WaitForSeconds(0.3f);
            }
            Assert.That(dog.State, Is.EqualTo(HoundState.Dead));
            Assert.That(navigation.IsTravelling, Is.False);
            Assert.That(body.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator GroundedRunningMovementEmitsSprintClue()
        {
            actor.AddComponent<HoundAgent>();
            var player = SpawnPlayer();
            yield return new WaitForSeconds(0.25f);
            player.CaptureCursor();
            var controller = player.GetComponent<CharacterController>();
            var hearing = actor.GetComponent<HoundHearing>();
            bool heardSprint = false;
            for (float elapsed = 0; elapsed < 1f; elapsed += Time.deltaTime)
            {
                controller.Move(Vector3.forward * (6f * Mathf.Max(0.02f, Time.deltaTime)) + Vector3.down * 0.02f);
                yield return null;
                if (hearing.Memory.Latest != null && hearing.Memory.Latest.Sound.Kind == HoundSoundKind.Sprint)
                { heardSprint = true; break; }
            }
            Assert.That(heardSprint, Is.True);
        }
#endif

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.Destroy(objects[i]);
            objects.Clear();
            yield return null;
            if (instance.valid) instance.Remove();
            if (data != null) Object.Destroy(data);
            Time.timeScale = previousTimeScale;
        }
    }
}
