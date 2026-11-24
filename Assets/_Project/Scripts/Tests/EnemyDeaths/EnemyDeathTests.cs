#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Lockdown.AI.Agents;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lockdown.Tests
{
    public sealed class EnemyDeathTests
    {
        private const string Characters = "Assets/_Project/Prefabs/Characters/";
        // Open floor in the Central Hall, away from the authored enemies.
        private static readonly Vector3 Spot = new Vector3(-2f, 0.03f, 2.2f);

        [TearDown]
        public void TearDown() { Time.timeScale = 1f; }

        private static IEnumerator LoadMuseum()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            // Only the enemy under test may act.
            foreach (var guard in Object.FindObjectsByType<PursuerAgent>()) guard.enabled = false;
            Object.FindAnyObjectByType<SpawnDirectorAgent>().enabled = false;
        }

        private static GameObject Spawn(string prefab) => Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(Characters + prefab + ".prefab"), Spot, Quaternion.identity);

        private static void Kill(GameObject enemy) { enemy.GetComponent<Health>().TakeDamage(10000f); }

        private static IEnumerator ExpectRemoval(GameObject enemy)
        {
            var removal = enemy.GetComponent<CorpseRemoval>();
            Assert.That(removal, Is.Not.Null);
            yield return new WaitForSeconds(0.5f);
            Assert.That(removal.IsRemoving, Is.False, "A living enemy must not be removed.");
            Kill(enemy);
            float died = Time.time;
            yield return new WaitForSeconds(3f);
            Assert.That(enemy != null, Is.True, "The body should stay for a few seconds.");
            while (enemy != null && Time.time - died < removal.TotalTime + 2f) yield return null;
            Assert.That(enemy == null, Is.True, "The body should be destroyed.");
            Assert.That(Time.time - died, Is.GreaterThan(5f));
        }

        [UnityTest]
        public IEnumerator TheHoundFallsOverAndStaysDown()
        {
            yield return LoadMuseum();
            var hound = Spawn("InvestigatingHound");
            var animator = hound.GetComponentInChildren<Animator>();
            yield return new WaitForSeconds(0.5f);
            Transform head = hound.GetComponentsInChildren<Transform>().First(bone => bone.name == "Head");
            float standing = head.position.y - hound.transform.position.y;
            Capture(hound, "hound-alive");
            Kill(hound);
            yield return new WaitForSeconds(0.45f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), Is.True);
            Capture(hound, "hound-falling");
            yield return new WaitForSeconds(1.5f);
            Capture(hound, "hound-dead");
            var state = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(state.IsName("Death"), Is.True, "Death must not return to locomotion.");
            Assert.That(state.loop, Is.False);
            Assert.That(head.position.y - hound.transform.position.y, Is.LessThan(standing * 0.4f), "The head must end up near the floor.");
        }

        [UnityTest]
        public IEnumerator TheHoundIsRemovedAfterDeath()
        {
            yield return LoadMuseum();
            yield return ExpectRemoval(Spawn("InvestigatingHound"));
        }

        [UnityTest]
        public IEnumerator TheGuardIsRemovedAfterDeath()
        {
            yield return LoadMuseum();
            yield return ExpectRemoval(Spawn("Pursuer"));
        }

        [UnityTest]
        public IEnumerator TheArcherAndItsBonesAreRemovedAfterDeath()
        {
            yield return LoadMuseum();
            var archer = Spawn("SkeletonArcher");
            yield return null;
            archer.GetComponent<SkeletonDormancy>().Wake();
            yield return new WaitForSeconds(3f);
            int before = Object.FindObjectsByType<SkeletonHitbox>().Length;
            int own = archer.GetComponentsInChildren<SkeletonHitbox>().Length;
            yield return ExpectRemoval(archer);
            Assert.That(Object.FindObjectsByType<SkeletonHitbox>().Length, Is.EqualTo(before - own), "No loose bones may remain.");
        }

        [UnityTest]
        public IEnumerator PlacedMuseumEnemiesHaveRemovalToo()
        {
            yield return LoadMuseum();
            Assert.That(Object.FindAnyObjectByType<PursuerAgent>().GetComponent<CorpseRemoval>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<SkeletonArcherAgent>().GetComponent<CorpseRemoval>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PausingStopsTheCountdown()
        {
            yield return LoadMuseum();
            var hound = Spawn("InvestigatingHound");
            hound.GetComponent<CorpseRemoval>().Configure(0.3f, 0.1f, 0.5f);
            yield return null;
            Kill(hound);
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(hound != null, Is.True);
            Time.timeScale = 1f;
            yield return new WaitForSeconds(0.8f);
            Assert.That(hound == null, Is.True);
        }

        [UnityTest]
        public IEnumerator BreakingGlassAfterTheDogIsGoneCausesNoErrors()
        {
            yield return LoadMuseum();
            var response = Object.FindAnyObjectByType<GlassHoundResponse>();
            var glass = Object.FindObjectsByType<BreakableGlass>().OrderBy(pane => pane.transform.position.z).ToArray();
            glass[0].GetComponent<Health>().TakeDamage(1000f);
            yield return new WaitForSeconds(1.5f);
            Assert.That(response.Pack.Count, Is.EqualTo(1));
            var dog = response.Pack[0].gameObject;
            dog.GetComponent<CorpseRemoval>().Configure(0.1f, 0.1f, 0.5f);
            Kill(dog);
            yield return new WaitForSeconds(0.6f);
            Assert.That(dog == null, Is.True);
            glass[1].GetComponent<Health>().TakeDamage(1000f);
            yield return new WaitForSeconds(0.5f);
            Assert.That(response.Pack.Count, Is.Zero, "The response must not keep a destroyed dog.");
        }

        private static void Capture(GameObject target, string name)
        {
            Directory.CreateDirectory("Temp/EnemyDeaths");
            var view = new GameObject("DeathCamera").AddComponent<Camera>();
            view.transform.position = target.transform.position + new Vector3(1.9f, 1.2f, 1.5f);
            view.transform.LookAt(target.transform.position + Vector3.up * 0.3f);
            var texture = new RenderTexture(800, 500, 24);
            view.targetTexture = texture;
            view.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(800, 500, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 800, 500), 0, 0);
            image.Apply();
            File.WriteAllBytes("Temp/EnemyDeaths/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = null;
            view.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(view.gameObject);
        }
    }
}
#endif
