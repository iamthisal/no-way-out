#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Lockdown.Tests
{
    public sealed class HoundResponseTests
    {
        private IEnumerator LoadMuseum()
        {
            if (string.IsNullOrEmpty(UnityEditor.AssetDatabase.GUIDToAssetPath("3c09e9fafae22354683897089c02f979")))
            {
                LogAssert.Expect(LogType.Error, new Regex("^Problem detected while opening the Scene file: 'Assets/_Project/Scenes/Museum/Museum_Main.unity'"));
                LogAssert.Expect(LogType.Error, "Prefab instance problem. Missing Prefab Asset: 'Exit_MuseumDoubleDoor (Missing Prefab with guid: 3c09e9fafae22354683897089c02f979)'");
            }
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            foreach (var enemy in Object.FindObjectsByType<WanderingEnemy>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
        }

        [UnityTest]
        public IEnumerator BreakingGlassCallsOneDogAndItApproachesTheCase()
        {
            yield return LoadMuseum();
            var response = Object.FindAnyObjectByType<GlassHoundResponse>();
            Assert.That(response, Is.Not.Null);
            yield return new WaitForSeconds(0.2f);
            Assert.That(response.Pack.Count, Is.Zero);
            Assert.That(response.IsAlerted, Is.False);
            Capture(new Vector3(0f, 1.65f, 3f), new Vector3(0f, 0.6f, 9f), "before-glass");
            var glass = Object.FindObjectsByType<BreakableGlass>(FindObjectsSortMode.None).OrderBy(g => g.transform.position.z).First();
            glass.GetComponent<Health>().TakeDamage(1000f);
            Assert.That(response.IsAlerted, Is.True);
            yield return new WaitForSeconds(1.8f);
            Assert.That(response.Pack.Count, Is.EqualTo(1));
            Assert.That(response.Pack.Distinct().Count(), Is.EqualTo(1));
            var positions = response.Pack.Select(d => d.transform.position).ToArray();
            foreach (var dog in response.Pack)
            {
                Assert.That(dog.HasClue, Is.True);
                Assert.That(dog.LastNoisePosition, Is.EqualTo(glass.transform.position));
                Assert.That(dog.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True);
            }
            for (int i = 0; i < positions.Length; i++)
                for (int j = i + 1; j < positions.Length; j++) Assert.That(Vector3.Distance(positions[i], positions[j]), Is.GreaterThan(2f));
            yield return new WaitForSeconds(2f);
            File.WriteAllLines("Temp/HoundResponseVerification/movement.txt", response.Pack.Select((d, i) =>
            {
                var a = d.GetComponent<NavMeshAgent>();
                return i + " start=" + positions[i] + " current=" + d.transform.position + " arrived=" + d.HasArrived + " path=" + a.hasPath + " pending=" + a.pathPending + " status=" + a.pathStatus + " remaining=" + a.remainingDistance + " velocity=" + a.velocity;
            }));
            Capture(new Vector3(-1f, 1.65f, 5.5f), glass.transform.position, "dogs-responding");
            for (int i = 0; i < positions.Length; i++)
                Assert.That(response.Pack[i].HasArrived || Vector3.Distance(response.Pack[i].transform.position, positions[i]) > 0.4f,
                    Is.True, "Dog " + i + " should move or already have reached the nearby case.");
            float deadline = Time.time + 25f;
            while (Time.time < deadline && response.Pack.Any(d => Vector3.Distance(d.transform.position, glass.transform.position) > 3.5f)) yield return null;
            foreach (var dog in response.Pack)
                Assert.That(Vector3.Distance(dog.transform.position, glass.transform.position), Is.LessThan(3.5f), "Dogs must reach the glass, not stay at their spawn.");
            Capture(glass.transform.position + new Vector3(-3.8f, 0.85f, -3.8f), glass.transform.position, "pack-at-glass");
        }

        [UnityTest]
        public IEnumerator EveryCaseRedirectsTheSamePackWithoutSpawningExtraDogs()
        {
            yield return LoadMuseum();
            var response = Object.FindAnyObjectByType<GlassHoundResponse>();
            var glasses = Object.FindObjectsByType<BreakableGlass>(FindObjectsSortMode.None).OrderBy(g => g.transform.position.z).ToArray();
            Assert.That(glasses.Length, Is.EqualTo(7));
            glasses[0].GetComponent<Health>().TakeDamage(1000f);
            yield return new WaitForSeconds(1.8f);
            var pack = response.Pack.ToArray();
            Assert.That(pack.Length, Is.EqualTo(1));
            foreach (var glass in glasses.Skip(1))
            {
                glass.GetComponent<Health>().TakeDamage(1000f);
                yield return null;
                Assert.That(response.LatestNoisePosition, Is.EqualTo(glass.transform.position));
                foreach (var dog in pack)
                {
                    Assert.That(dog.LastNoisePosition, Is.EqualTo(glass.transform.position));
                    var agent = dog.GetComponent<NavMeshAgent>();
                    Assert.That(HoundInvestigator.TryFindApproach(dog.transform.position, glass.transform.position, agent.agentTypeID, agent.areaMask, out _), Is.True, "Every case needs a reachable approach.");
                }
            }
            yield return new WaitForSeconds(1.5f);
            CollectionAssert.AreEquivalent(pack, response.Pack);
            Assert.That(Object.FindObjectsByType<HoundInvestigator>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            yield return LoadMuseum();
            Assert.That(Object.FindAnyObjectByType<GlassHoundResponse>().IsAlerted, Is.False);
            Assert.That(Object.FindObjectsByType<HoundInvestigator>(FindObjectsSortMode.None), Is.Empty, "Restart must remove the pack.");
        }

        [UnityTest]
        public IEnumerator DisabledDirectorDoesNotRespondAndReenableDoesNotDuplicateListeners()
        {
            yield return LoadMuseum();
            var response = Object.FindAnyObjectByType<GlassHoundResponse>();
            var glasses = Object.FindObjectsByType<BreakableGlass>(FindObjectsSortMode.None);
            response.enabled = false;
            glasses[0].GetComponent<Health>().TakeDamage(1000f);
            yield return null;
            Assert.That(response.Pack, Is.Empty);
            for (int i = 0; i < 3; i++) { response.enabled = true; response.enabled = false; }
            response.enabled = true;
            glasses[1].GetComponent<Health>().TakeDamage(1000f);
            yield return new WaitForSeconds(1.8f);
            Assert.That(response.Pack.Count, Is.EqualTo(1));
        }

        private static void Capture(Vector3 position, Vector3 target, string name)
        {
            Directory.CreateDirectory("Temp/HoundResponseVerification");
            var cameraObject = new GameObject("HoundTestCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = position;
            camera.transform.LookAt(target);
            camera.fieldOfView = 70f;
            var render = new RenderTexture(1280, 720, 24);
            var previous = RenderTexture.active;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes("Temp/HoundResponseVerification/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(render);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
#endif
