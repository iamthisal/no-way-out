#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lockdown.AI.Agents;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Player;
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
    public sealed class SkeletonArcherTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/SkeletonArcher.prefab";
        private const string ArrowPath = "Assets/_Project/Prefabs/Characters/SkeletonArrow.prefab";
        private static readonly Vector3 Arena = new Vector3(200f, 0f, 200f);
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
            foreach (var arrow in Object.FindObjectsByType<ArrowProjectile>()) Object.DestroyImmediate(arrow.gameObject);
            Application.runInBackground = previousBackground;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private GameObject Floor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestFloor";
            floor.transform.position = Arena + Vector3.down * 0.1f;
            floor.transform.localScale = new Vector3(40f, 0.2f, 40f);
            created.Add(floor);
            Physics.SyncTransforms();
            return floor;
        }

        // Instantiates under an inactive parent so the archer can be configured before Awake.
        private GameObject Spawn(bool dormant, bool agent = false, Vector3? at = null)
        {
            var holder = new GameObject("Holder");
            holder.SetActive(false);
            var archer = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), at ?? Arena, Quaternion.identity, holder.transform);
            archer.GetComponent<NavMeshAgent>().enabled = false;
            archer.GetComponent<SkeletonDormancy>().enabled = dormant;
            archer.GetComponent<SkeletonArcherAgent>().enabled = agent;
            archer.transform.SetParent(null, true);
            Object.DestroyImmediate(holder);
            archer.SetActive(true);
            created.Add(archer);
            return archer;
        }

        private static SkeletonHitbox Piece(GameObject archer, string name) =>
            archer.GetComponentsInChildren<SkeletonHitbox>(true).First(piece => piece.name == name);

        private static Transform Bone(GameObject archer, string name) =>
            archer.GetComponentsInChildren<Transform>(true).First(item => item.name == name);

        [Test]
        public void PrefabIsAnOriginalRiggedArcherBuiltFromRigidBonePieces()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, "Run Tools > Lockdown > Build Skeleton Archer.");
            var pieces = prefab.GetComponentsInChildren<SkeletonHitbox>(true);
            Assert.That(pieces.Length, Is.EqualTo(25));
            int triangles = 0;
            foreach (var piece in pieces)
            {
                Assert.That(piece.GetComponent<BoxCollider>(), Is.Not.Null, piece.name);
                Assert.That(piece.Body, Is.EqualTo(prefab.GetComponent<SkeletonBody>()), piece.name);
                var renderer = piece.GetComponent<MeshRenderer>();
                foreach (var material in renderer.sharedMaterials)
                    Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), piece.name);
                var mesh = piece.GetComponent<MeshFilter>().sharedMesh;
                for (int i = 0; i < mesh.subMeshCount; i++) triangles += (int)mesh.GetIndexCount(i) / 3;
            }
            Assert.That(triangles, Is.InRange(3000, 8000));
            Assert.That(pieces.Single(p => p.name == "Piece_Skull").Region, Is.EqualTo(SkeletonRegion.Head));
            Assert.That(pieces.Single(p => p.name == "Piece_Hand_L").Region, Is.EqualTo(SkeletonRegion.BowArm));
            Assert.That(pieces.Single(p => p.name == "Piece_Bow").Region, Is.EqualTo(SkeletonRegion.Gear));
            Assert.That(pieces.Single(p => p.name == "Piece_Ribcage").Region, Is.EqualTo(SkeletonRegion.Body));
            var bowString = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(bowString.name, Is.EqualTo("BowString"));
            // The FBX skin lists the whole rig; the string's weights use only these two bones.
            Assert.That(bowString.bones.Select(b => b.name), Is.SupersetOf(new[] { "Bow", "StringNock" }));
            Assert.That(prefab.GetComponent<Health>().MaxHealth, Is.EqualTo(70f));
            Assert.That(prefab.GetComponent<NavMeshAgent>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<SkeletonArcherAgent>(), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPath).GetComponent<ArrowProjectile>(), Is.Not.Null);
            Assert.That(Bone(prefab, "ArrowTip").parent.name, Is.EqualTo("Arrow"));
            var foot = pieces.Single(p => p.name == "Piece_Foot_L").GetComponent<Renderer>().bounds.center;
            Assert.That(foot.z, Is.GreaterThan(Bone(prefab, "Foot_L").position.z), "The archer should face Unity +Z.");
        }

        [Test]
        public void AnimatorHasEveryClipWithTheRightLoopingAndTiming()
        {
            var animator = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponentInChildren<Animator>();
            Assert.That(animator.applyRootMotion, Is.False);
            var clips = animator.runtimeAnimatorController.animationClips.Distinct().ToDictionary(c => c.name);
            Assert.That(clips.Keys, Is.EquivalentTo(new[] { "Idle", "Walk", "Run", "Crouch", "Draw", "AimHold", "Release", "Nock", "Hit" }));
            foreach (var looping in new[] { "Idle", "Walk", "Run", "Crouch", "AimHold" }) Assert.That(clips[looping].isLooping, Is.True, looping);
            foreach (var once in new[] { "Draw", "Release", "Nock", "Hit" }) Assert.That(clips[once].isLooping, Is.False, once);
            Assert.That(clips["Draw"].length, Is.EqualTo(0.7f).Within(0.02f), "SkeletonBow's draw time matches this clip.");
            Assert.That(clips["Release"].length, Is.EqualTo(0.3f).Within(0.02f));
            Assert.That(clips["Nock"].length, Is.EqualTo(0.6f).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator FullDrawAimsTheNockedArrowStraightAhead()
        {
            Floor();
            var archer = Spawn(false);
            var animator = archer.GetComponentInChildren<Animator>();
            yield return null;
            var bounds = archer.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.06f), "Feet rest on the floor.");
            Assert.That(bounds.size.y, Is.InRange(1.7f, 2.0f));
            animator.Play("AimHold", 0, 0.5f);
            yield return null;
            yield return null;
            Vector3 tip = archer.transform.InverseTransformPoint(Bone(archer, "ArrowTip").position);
            Vector3 nock = archer.transform.InverseTransformPoint(Bone(archer, "Arrow").position);
            Vector3 direction = (tip - nock).normalized;
            Assert.That(tip.z, Is.GreaterThan(0.5f), "At full draw the arrowhead is out in front of him.");
            Assert.That(tip.y, Is.InRange(1.3f, 1.7f));
            Assert.That(Vector3.Dot(direction, Vector3.forward), Is.GreaterThan(0.95f));
            Assert.That(Bone(archer, "Piece_NockedArrow").GetComponent<Renderer>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator BowFiresATelegraphedTwoShotBurstAlongTheAim()
        {
            Floor();
            var archer = Spawn(false);
            var bow = archer.GetComponent<SkeletonBow>();
            var voice = archer.GetComponent<AudioSource>();
            var target = Arena + new Vector3(0f, 1.2f, 10f);
            float start = Time.time, firstShot = -1f;
            while (Time.time - start < 3.5f)
            {
                bow.Tick(true, target, Vector3.zero, Time.deltaTime);
                if (Time.time - start < 0.2f && bow.CurrentPhase == SkeletonBow.Phase.Drawing) Assert.That(voice.isPlaying, Is.True, "The bow creaks as he draws.");
                if (firstShot < 0f && bow.ShotsFired == 1)
                {
                    firstShot = Time.time - start;
                    Assert.That(bow.ArrowInHand, Is.False);
                    Assert.That(Bone(archer, "Piece_NockedArrow").GetComponent<Renderer>().enabled, Is.False);
                    Vector3 toTarget = (target - bow.LastArrow.transform.position).normalized;
                    Assert.That(Vector3.Dot(bow.LastArrow.Velocity.normalized, toTarget), Is.GreaterThan(0.97f));
                }
                yield return null;
            }
            Assert.That(firstShot, Is.InRange(0.8f, 1.1f), "The draw is the warning before the first arrow.");
            Assert.That(bow.ShotsFired, Is.InRange(2, 3));
        }

        [UnityTest]
        public IEnumerator ArrowsHurtThePlayerAndStickInWalls()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            foreach (var shooter in Object.FindObjectsByType<EnemyShooting>()) shooter.enabled = false;
            Floor();
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var health = player.GetComponent<Health>();
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = Arena + Vector3.up * 0.05f;
            body.enabled = true;
            Physics.SyncTransforms();
            yield return null;
            float before = health.CurrentHealth;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPath).GetComponent<ArrowProjectile>();
            var origin = Arena + new Vector3(0f, 1.2f, 6f);
            var arrow = Object.Instantiate(prefab, origin, Quaternion.identity);
            arrow.Launch(origin, Vector3.back * 24f, 0f, 18f, null);
            yield return new WaitForSeconds(0.5f);
            Assert.That(arrow == null || arrow.HitPlayer, Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(before - 18f));

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(wall);
            wall.transform.position = Arena + new Vector3(8f, 1.5f, 0f);
            Physics.SyncTransforms();
            origin = Arena + new Vector3(8f, 1.5f, 6f);
            arrow = Object.Instantiate(prefab, origin, Quaternion.identity);
            arrow.Launch(origin, Vector3.back * 24f, 3f, 18f, null);
            yield return new WaitForSeconds(0.6f);
            Assert.That(arrow != null && arrow.IsStuck, Is.True, "Arrows stay in what they hit.");
            Assert.That(arrow.StuckIn, Is.EqualTo(wall.GetComponent<Collider>()));
            Assert.That(arrow.transform.position.z, Is.InRange(Arena.z + 0.3f, Arena.z + 0.5f), "The tip is buried just inside the face.");
            Assert.That(health.CurrentHealth, Is.EqualTo(before - 18f));
        }

        [UnityTest]
        public IEnumerator HeadshotsHitHarderGearAbsorbsAndBowArmHitsSpoilAim()
        {
            Floor();
            var archer = Spawn(false, true);
            var health = archer.GetComponent<Health>();
            yield return null;
            Piece(archer, "Piece_Skull").TakeDamage(10f);
            Assert.That(health.CurrentHealth, Is.EqualTo(50f));
            Piece(archer, "Piece_Bow").TakeDamage(10f);
            Assert.That(health.CurrentHealth, Is.EqualTo(46.5f).Within(0.01f));
            Assert.That(archer.GetComponent<SkeletonBow>().AimSpoiled, Is.False);
            Piece(archer, "Piece_Forearm_L").TakeDamage(10f);
            Assert.That(health.CurrentHealth, Is.EqualTo(36.5f).Within(0.01f));
            Assert.That(archer.GetComponent<SkeletonBow>().AimSpoiled, Is.True);
        }

        [UnityTest]
        public IEnumerator DefeatMakesEveryVisibleBoneFallApart()
        {
            Floor();
            var archer = Spawn(false);
            yield return null;
            Piece(archer, "Piece_Ribcage").TakeDamage(500f);
            var body = archer.GetComponent<SkeletonBody>();
            Assert.That(body.IsCollapsed, Is.True);
            Assert.That(archer.GetComponentInChildren<Animator>().enabled, Is.False);
            Assert.That(archer.GetComponentInChildren<SkinnedMeshRenderer>().enabled, Is.False, "The bow string snaps.");
            var pieces = archer.GetComponentsInChildren<SkeletonHitbox>();
            Assert.That(pieces.Length, Is.EqualTo(25));
            foreach (var piece in pieces)
            {
                Assert.That(piece.GetComponent<Rigidbody>(), Is.Not.Null, piece.name);
                Assert.That(piece.transform.parent, Is.EqualTo(archer.transform), piece.name);
            }
            yield return new WaitForSeconds(2.5f);
            foreach (var piece in pieces)
                Assert.That(piece.transform.position.y, Is.InRange(-0.05f, 0.9f), piece.name + " should have fallen to the floor.");
        }

        [UnityTest]
        public IEnumerator HeadshotKnocksTheSkullOffBeforeTheBodyCrumbles()
        {
            Floor();
            var archer = Spawn(false);
            yield return null;
            var skull = Piece(archer, "Piece_Skull");
            skull.TakeDamage(500f);
            Assert.That(skull.GetComponent<Rigidbody>().linearVelocity.magnitude, Is.GreaterThan(4f));
            Assert.That(Piece(archer, "Piece_Ribcage").GetComponent<Rigidbody>(), Is.Null, "The headless body stands for a moment.");
            yield return new WaitForSeconds(0.35f);
            Assert.That(Piece(archer, "Piece_Ribcage").GetComponent<Rigidbody>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator BonePileGathersIntoTheArcherWhenWoken()
        {
            Floor();
            // Well clear of the arena centre, where an earlier test may have left the player.
            var archer = Spawn(true, at: Arena + new Vector3(14f, 0f, 14f));
            var body = archer.GetComponent<SkeletonBody>();
            var dormancy = archer.GetComponent<SkeletonDormancy>();
            var agent = archer.GetComponent<SkeletonArcherAgent>();
            yield return null;
            Assert.That(body.IsDormant, Is.True);
            Assert.That(agent.enabled, Is.False);
            foreach (var piece in body.Pieces)
                Assert.That(piece.GetComponent<Renderer>().bounds.max.y, Is.LessThan(0.6f), piece.name + " should lie in the pile.");
            dormancy.Wake();
            Assert.That(body.IsAssembling, Is.True);
            yield return new WaitForSeconds(2.2f);
            Assert.That(body.IsDormant, Is.False);
            Assert.That(body.IsAssembling, Is.False);
            Assert.That(dormancy.HasWoken, Is.True);
            Assert.That(agent.enabled, Is.True);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            foreach (var piece in body.Pieces)
            {
                var rest = Piece(prefab, piece.name).transform;
                Assert.That(Vector3.Distance(piece.transform.localPosition, rest.localPosition), Is.LessThan(1e-3f), piece.name);
            }
            Assert.That(Piece(archer, "Piece_Skull").transform.position.y, Is.GreaterThan(1.2f));
        }

        [UnityTest]
        public IEnumerator BonesRattleWhileHeMovesAndFallSilentWhenHeStops()
        {
            Floor();
            var archer = Spawn(false);
            var rattle = archer.GetComponentInChildren<SkeletonRattle>();
            yield return null;
            float start = Time.time;
            while (Time.time - start < 1f)
            {
                archer.transform.position += Vector3.forward * (4f * Time.deltaTime);
                yield return null;
            }
            Assert.That(rattle.Volume, Is.GreaterThan(0.4f));
            yield return new WaitForSeconds(1f);
            Assert.That(rattle.Volume, Is.LessThan(0.05f));
        }
    }
}
#endif
