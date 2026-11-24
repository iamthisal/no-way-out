#if UNITY_EDITOR
using System.Collections;
using Lockdown.AI.Agents;
using Lockdown.AI.Core.Hound;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lockdown.Tests.Hound
{
    public sealed class HoundMuseumTests
    {
        private Scene museum;

        [UnityTest]
        public IEnumerator MainMuseumBreakSpawnsExistingModelsWithAStarBrains()
        {
            museum = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            GlassHoundResponse response = null;
            BreakableGlass nearest = null;
            float nearestDistance = float.PositiveInfinity;
            int emitters = 0;
            int players = 0;
            foreach (var root in museum.GetRootGameObjects())
            {
                players += root.GetComponentsInChildren<HoundPlayerNoise>().Length;
                var responses = root.GetComponentsInChildren<GlassHoundResponse>();
                if (responses.Length > 0) response = responses[0];
                foreach (var glass in root.GetComponentsInChildren<BreakableGlass>())
                {
                    Assert.That(glass.GetComponent<HoundNoiseEmitter>(), Is.Not.Null);
                    emitters++;
                    float distance = Vector3.Distance(glass.transform.position, new Vector3(0, 0, 7));
                    if (distance < nearestDistance) { nearest = glass; nearestDistance = distance; }
                }
            }
            Assert.That(players, Is.EqualTo(1));
            Assert.That(emitters, Is.EqualTo(7));
            Assert.That(response, Is.Not.Null);
            Assert.That(nearest, Is.Not.Null);
            nearest.GetComponent<Health>().TakeDamage(10000);
            bool trackedBreak = false;
            bool selectedRoute = false;
            for (float elapsed = 0; elapsed < 2.5f; elapsed += Time.deltaTime)
            {
                yield return null;
                foreach (var legacy in response.Pack)
                {
                    var dog = legacy.GetComponent<HoundAgent>();
                    Assert.That(dog, Is.Not.Null);
                    Assert.That(dog.GetComponent<HoundCombat>(), Is.Not.Null);
                    Assert.That(dog.Navigation.IsReady, Is.True);
                    if (dog.State == HoundState.Track) trackedBreak = true;
                    if (dog.Navigation.Route.Count >= 2) selectedRoute = true;
                }
            }
            Assert.That(response.Pack.Count, Is.EqualTo(1));
            Assert.That(trackedBreak, Is.True, "At least the nearby hound must investigate the actual break.");
            Assert.That(selectedRoute, Is.True);
            foreach (var legacy in response.Pack)
            {
                var dog = legacy.GetComponent<HoundAgent>();
                TestContext.WriteLine(legacy.name + " position=" + legacy.transform.position + " state=" + dog.State
                    + " route=" + dog.Navigation.Route.Count + " reachable=" + dog.Navigation.CanReach(nearest.transform.position));
            }

            // Probe a real open room connection without moving or saving any scene object.
            var first = response.Pack[0].GetComponent<HoundAgent>();
            bool roomConnection = first.Navigation.CanReach(new Vector3(2.5f, 0.1f, 2));
            Assert.That(roomConnection, Is.True, "Exhibition to Central Hall must be connected.");
            var original = response.Pack[0];
            foreach (var root in museum.GetRootGameObjects())
                foreach (var glass in root.GetComponentsInChildren<BreakableGlass>())
                    if (!glass.IsBroken) glass.GetComponent<Health>().TakeDamage(10000);
            yield return new WaitForSeconds(1f);
            Assert.That(response.Pack.Count, Is.EqualTo(1), "Later breaks must not spawn extra dogs.");
            Assert.That(response.Pack[0], Is.SameAs(original));
            TestContext.WriteLine("Main scene: 7 break emitters, 1 player adapter, 1 hound; A* exhibition-to-central connection passed.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (museum.IsValid() && museum.isLoaded) yield return SceneManager.UnloadSceneAsync(museum);
        }
    }
}
#endif
