#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lockdown.AI.Agents;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Interfaces;
using Lockdown.Player;
using Lockdown.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Lockdown.Tests
{
    public sealed class MummyCurseTests
    {
        private const string MummyPath = "Assets/_Project/Prefabs/Characters/Mummy.prefab";
        private static readonly string[] Gods = { "Imsety", "Hapi", "Duamutef", "Qebehsenuef" };
        private static readonly Vector3 Arena = new Vector3(-200f, 0f, 200f);
        private readonly List<Object> created = new List<Object>();
        private bool previousBackground;

        [SetUp]
        public void Setup()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
            Application.runInBackground = previousBackground;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static string PropPath(string kind, string god) => "Assets/_Project/Prefabs/Props/" + kind + god + ".prefab";

        private void Floor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = Arena + Vector3.down * 0.1f;
            floor.transform.localScale = new Vector3(30f, 0.2f, 30f);
            created.Add(floor);
            Physics.SyncTransforms();
        }

        private CanopicJar SpawnJar(string god, Vector3 offset)
        {
            var jar = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PropPath("CanopicJar_", god)), Arena + offset, Quaternion.identity);
            created.Add(jar);
            return jar.GetComponentInChildren<CanopicJar>();
        }

        // Standing mummy with no NavMesh agent, configured before Awake runs.
        private MummyBody SpawnMummy(Vector3 offset, float riseDelay, string amuletGod = null)
        {
            var holder = new GameObject("Holder");
            holder.SetActive(false);
            var mummy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MummyPath), Arena + offset, Quaternion.identity, holder.transform);
            mummy.GetComponent<NavMeshAgent>().enabled = false;
            mummy.GetComponent<MummyAgent>().enabled = false;
            var body = mummy.GetComponent<MummyBody>();
            Field("startEntombed").SetValue(body, false);
            Field("riseDelay").SetValue(body, riseDelay);
            if (amuletGod != null) Field("amuletDrop").SetValue(body, AssetDatabase.LoadAssetAtPath<GameObject>(PropPath("Amulet_", amuletGod)));
            mummy.transform.SetParent(null, true);
            Object.DestroyImmediate(holder);
            created.Add(mummy);
            return body;
        }

        private static FieldInfo Field(string name) => typeof(MummyBody).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

        private static Transform Part(Component root, string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        [Test]
        public void PrefabsAreOriginalModelsWithSarcophagiSealedJarsAndAmulets()
        {
            var mummy = AssetDatabase.LoadAssetAtPath<GameObject>(MummyPath);
            Assert.That(mummy, Is.Not.Null, "Run Tools > Lockdown > Build Mummies and Canopic Jars.");
            var pieces = mummy.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Piece_")).ToArray();
            Assert.That(pieces.Length, Is.EqualTo(21));
            Assert.That(pieces.SelectMany(r => r.sharedMaterials).All(m => m.shader.name == "Universal Render Pipeline/Lit"), Is.True);
            var animator = mummy.GetComponentInChildren<Animator>();
            Assert.That(animator.transform.parent.name, Is.EqualTo("Facing"), "The clips animate the model root, so facing lives on a parent.");
            var clips = animator.runtimeAnimatorController.animationClips.Distinct().ToDictionary(c => c.name);
            Assert.That(clips.Keys, Is.EquivalentTo(new[] { "Idle", "Shamble", "Chase", "Bite", "Hit", "Fall", "Down", "Rise", "Entombed", "WalkOut" }));
            foreach (var loop in new[] { "Idle", "Shamble", "Chase", "Down", "Entombed" }) Assert.That(clips[loop].isLooping, Is.True, loop);
            Assert.That(clips["WalkOut"].length, Is.EqualTo(1.8f).Within(0.02f));
            Assert.That(clips["Rise"].length, Is.EqualTo(1.6f).Within(0.02f));
            foreach (var god in Gods)
            {
                var jarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PropPath("CanopicJar_", god));
                var jar = jarPrefab.GetComponentInChildren<CanopicJar>();
                Assert.That(jar.GodName, Is.EqualTo(god));
                Assert.That(jar.GetComponentsInChildren<Rigidbody>().Length, Is.EqualTo(14), god + ": base, twelve shards and the lid.");
                Assert.That(jarPrefab.transform.Find("Plinth").GetComponentInParent<IDamageable>(), Is.Null, "Hitting the stand must not break the jar.");
                Assert.That(jar.transform.position.y, Is.InRange(0.85f, 0.95f));

                var sarcophagus = AssetDatabase.LoadAssetAtPath<GameObject>(PropPath("Sarcophagus_", god)).GetComponent<Sarcophagus>();
                Assert.That(sarcophagus.GodName, Is.EqualTo(god));
                var lid = sarcophagus.transform.Find("Lid");
                var shell = sarcophagus.transform.Find("Case");
                Assert.That(lid.GetComponent<SarcophagusLid>(), Is.Not.Null);
                Assert.That(shell.GetComponent<MeshCollider>(), Is.Not.Null);
                Assert.That(shell.GetComponent<NavMeshObstacle>().carving, Is.True);
                Assert.That(lid.GetComponent<Renderer>().bounds.center.z, Is.GreaterThan(shell.GetComponent<Renderer>().bounds.center.z), "The lid faces +Z.");

                var amulet = AssetDatabase.LoadAssetAtPath<GameObject>(PropPath("Amulet_", god));
                Assert.That(amulet.GetComponent<LootItem>().ItemName, Is.EqualTo("Amulet of " + god));
                Assert.That(amulet.GetComponent<LootItem>().Value, Is.EqualTo(750));
                Assert.That(amulet.GetComponent<MummyAmulet>().GodName, Is.EqualTo(god));
                Assert.That(amulet.GetComponent<Rigidbody>(), Is.Not.Null);
            }
        }

        [UnityTest]
        public IEnumerator SealedJarShrugsOffHitsUntilItsMummyIsFreed()
        {
            Floor();
            var jar = SpawnJar("Hapi", Vector3.zero);
            var hammered = SpawnJar("Duamutef", new Vector3(2f, 0f, 0f));
            int rejected = 0;
            jar.Rejected += () => rejected++;
            yield return null;
            Assert.That(jar.IsSealed, Is.True);
            jar.TakeDamage(100f);
            Assert.That(jar.IsBroken, Is.False, "The curse protects a jar whose mummy is still entombed.");
            Assert.That(rejected, Is.EqualTo(1));
            jar.Unseal();
            jar.TakeDamage(25f);
            Assert.That(jar.IsBroken, Is.False);
            jar.TakeDamage(25f);
            Assert.That(jar.IsBroken, Is.True, "Two pistol shots break an unsealed jar.");
            hammered.Unseal();
            hammered.TakeDamage(100f);
            Assert.That(hammered.IsBroken, Is.True, "One hammer blow breaks an unsealed jar.");
            var shards = jar.GetComponentsInChildren<Rigidbody>();
            Assert.That(shards.All(s => !s.isKinematic), Is.True);
            yield return new WaitForSeconds(1.5f);
            Assert.That(shards.Count(s => s.position.y < 0.5f), Is.GreaterThan(10), "The pieces fall to the floor.");
        }

        [UnityTest]
        public IEnumerator MummyRisesAgainWhileItsJarIsIntact()
        {
            Floor();
            var jar = SpawnJar("Imsety", new Vector3(4f, 0f, 0f));
            var mummy = SpawnMummy(Vector3.zero, 1f);
            mummy.BindJar(jar);
            yield return null;
            Assert.That(mummy.Life, Is.EqualTo(MummyLife.Up));
            mummy.TakeDamage(60f);
            Assert.That(mummy.Life, Is.EqualTo(MummyLife.Down));
            Assert.That(mummy.GetComponent<CapsuleCollider>().enabled, Is.False, "The player can step over a fallen mummy.");
            mummy.TakeDamage(500f);
            Assert.That(mummy.Life, Is.EqualTo(MummyLife.Down), "A fallen mummy cannot be finished off.");
            yield return new WaitForSeconds(0.8f + 1f + 1.8f);
            Assert.That(mummy.Life, Is.EqualTo(MummyLife.Up));
            Assert.That(mummy.Health, Is.EqualTo(mummy.MaxHealth));
            Assert.That(mummy.TimesRisen, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DestroyedMummyCrumblesAndLeavesItsAmuletAsLoot()
        {
            Floor();
            var jar = SpawnJar("Qebehsenuef", new Vector3(4f, 0f, 0f));
            var mummy = SpawnMummy(Vector3.zero, 1f, "Qebehsenuef");
            mummy.BindJar(jar);
            yield return null;
            jar.Shatter();
            Assert.That(mummy.IsMortal, Is.True);
            mummy.TakeDamage(60f);
            Assert.That(mummy.Life, Is.EqualTo(MummyLife.Crumbling));
            yield return new WaitForSeconds(0.8f + 1.4f + 0.6f);
            Assert.That(mummy.Life, Is.EqualTo(MummyLife.Destroyed));
            Assert.That(mummy.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("Piece_")).All(r => !r.enabled), Is.True);
            var amulet = mummy.DroppedAmulet;
            Assert.That(amulet, Is.Not.Null);
            created.Add(amulet);
            Assert.That(amulet.GetComponent<LootItem>().ItemName, Is.EqualTo("Amulet of Qebehsenuef"));
            Assert.That(amulet.GetComponent<MummyAmulet>().GodName, Is.EqualTo("Qebehsenuef"));
            Assert.That(amulet.transform.position.y, Is.LessThan(0.3f), "The amulet lies on the floor.");
            yield return new WaitForSeconds(2f);
            Assert.That(mummy.Life, Is.EqualTo(MummyLife.Destroyed), "It never rises again.");
        }

        [UnityTest]
        public IEnumerator BreakingTheJarOfAFallenMummyTurnsItToDust()
        {
            Floor();
            var jar = SpawnJar("Duamutef", new Vector3(4f, 0f, 0f));
            var mummy = SpawnMummy(Vector3.zero, 30f);
            mummy.BindJar(jar);
            yield return null;
            mummy.TakeDamage(60f);
            yield return new WaitForSeconds(1f);
            Assert.That(mummy.Life, Is.EqualTo(MummyLife.Down));
            jar.Shatter();
            Assert.That(mummy.Life, Is.EqualTo(MummyLife.Crumbling));
            yield return new WaitForSeconds(1.7f);
            Assert.That(mummy.Life, Is.EqualTo(MummyLife.Destroyed));
        }

        [UnityTest]
        public IEnumerator OpeningASarcophagusReleasesItsMummyWhichWalksForwardAndBites()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            foreach (var shooter in Object.FindObjectsByType<EnemyShooting>()) shooter.enabled = false;
            var curse = Object.FindAnyObjectByType<MummyCurse>();
            Assert.That(curse, Is.Not.Null, "Run Tools > Lockdown > Place Mummies and Canopic Jars.");
            var tombs = Object.FindObjectsByType<Sarcophagus>();
            var mummies = Object.FindObjectsByType<MummyBody>();
            Assert.That(tombs.Length, Is.EqualTo(4));
            Assert.That(mummies.Length, Is.EqualTo(4));
            Assert.That(mummies.All(m => m.Life == MummyLife.Entombed), Is.True);
            Assert.That(mummies.All(m => m.BoundJar.IsSealed), Is.True, "Every jar starts sealed.");
            Assert.That(tombs.Select(t => t.Mummy).Distinct().Count(), Is.EqualTo(4), "One mummy per sarcophagus.");
            var jars = mummies.Select(m => m.BoundJar.transform.position).ToArray();
            for (int i = 0; i < jars.Length; i++)
                for (int k = i + 1; k < jars.Length; k++)
                    Assert.That(Vector3.Distance(jars[i], jars[k]), Is.GreaterThan(6f), "The jars are spread across different galleries.");

            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var health = player.GetComponent<Health>();
            var tomb = tombs.First(t => t.GodName == "Hapi");
            var mummy = tomb.Mummy;
            Vector3 standAt = mummy.transform.position + mummy.transform.forward * 3.5f;
            NavMesh.SamplePosition(standAt, out var hit, 2f, NavMesh.AllAreas);
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = hit.position + Vector3.up * 0.05f;
            controller.enabled = true;
            Physics.SyncTransforms();
            mummy.BoundJar.TakeDamage(100f);
            Assert.That(mummy.BoundJar.IsBroken, Is.False, "Hapi's jar cannot be broken before Hapi is released.");

            Assert.That(tomb.GetComponentInChildren<SarcophagusLid>().TryInteract(player.gameObject), Is.True);
            yield return new WaitForSeconds(0.6f);
            Assert.That(tomb.LidAngle, Is.GreaterThan(30f), "The lid swings open.");
            Assert.That(mummy.Life, Is.EqualTo(MummyLife.Emerging), "The mummy walks out.");
            Assert.That(mummy.BoundJar.IsSealed, Is.False, "Releasing Hapi unseals Hapi's jar.");
            Assert.That(mummies.Where(m => m != mummy).All(m => m.Life == MummyLife.Entombed && m.BoundJar.IsSealed), Is.True);
            Assert.That(tomb.GetComponentInChildren<SarcophagusLid>(), Is.Null, "No prompt remains on an open sarcophagus.");
            yield return new WaitForSeconds(2f);
            Assert.That(mummy.IsUp, Is.True);
            Assert.That(curse.Active, Is.True);

            // While it walks: the toes lead the way and the legs swing.
            var agent = mummy.GetComponent<NavMeshAgent>();
            var thigh = Part(mummy, "Thigh_L");
            var poses = new List<Quaternion>();
            float before = health.CurrentHealth, start = Time.time;
            bool checkedFacing = false;
            while (Time.time - start < 8f && health.CurrentHealth >= before)
            {
                if (!checkedFacing && agent.velocity.magnitude > 0.8f)
                {
                    checkedFacing = true;
                    var toes = Part(mummy, "Piece_Foot_L").GetComponent<Renderer>().bounds.center - Part(mummy, "Foot_L").position;
                    Assert.That(Vector3.Dot(toes, agent.velocity.normalized), Is.GreaterThan(0f), "The mummy walks forwards, not backwards.");
                }
                if (agent.velocity.magnitude > 0.5f) poses.Add(thigh.localRotation);
                yield return null;
            }
            Assert.That(checkedFacing, Is.True, "The mummy moved towards the player.");
            Assert.That(poses.Count, Is.GreaterThan(10));
            Assert.That(poses.Max(q => Quaternion.Angle(q, poses[0])), Is.GreaterThan(10f), "The legs swing while walking.");
            Assert.That(health.CurrentHealth, Is.LessThan(before), "The mummy reaches the player and bites.");
            Assert.That((before - health.CurrentHealth) % 15f, Is.EqualTo(0f).Within(0.01f), "Each bite takes 15 health.");
        }
    }
}
#endif
