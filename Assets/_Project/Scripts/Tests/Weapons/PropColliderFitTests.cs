#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lockdown.Tests
{
    // Regression: greybox Box Colliders taller than the imported art made shots stop in mid-air
    // above props such as the Egypt pyramid and the courtyard vase.
    public sealed class PropColliderFitTests
    {
        private const float Tolerance = 0.1f;

        [UnityTest]
        public IEnumerator NoMuseumPropColliderRisesAboveItsArt()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var museum = GameObject.Find("Museum");
            Assert.That(museum, Is.Not.Null);
            var cover = museum.transform.Find("FixedCover");
            var tooTall = new List<string>();
            int checkedCount = 0;
            foreach (var collider in cover.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || !collider.enabled) continue;
                if (!TryArtBounds(collider, out Bounds art)) continue; // Invisible blockers have no art.
                checkedCount++;
                float above = collider.bounds.max.y - art.max.y;
                if (above > Tolerance) tooTall.Add(collider.name + " (+" + above.ToString("F2") + " m)");
            }
            Assert.That(checkedCount, Is.GreaterThan(50), "The museum's props should be checked.");
            Assert.That(tooTall, Is.Empty, "These colliders rise above their art: " + string.Join(", ", tooTall));

            var vase = GameObject.Find("maceta");
            Assert.That(vase.GetComponent<MeshCollider>(), Is.Not.Null, "The courtyard vase should have an exact collider.");
            Assert.That(GameObject.Find("Courtyard_Planter_Blocker"), Is.Null, "The tall capsule blocker should be gone.");

            AssertShotPassesAbove("P_Egypt_Pyramid");
            AssertShotPassesAbove("P_StoneAge_StandingStones");
        }

        // A shot 0.2 m above the art's highest point, aimed across its centre, must not hit it.
        private static void AssertShotPassesAbove(string propName)
        {
            var prop = GameObject.Find(propName);
            Assert.That(prop, Is.Not.Null, propName);
            Bounds art = default;
            bool any = false;
            foreach (var renderer in prop.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled || renderer.gameObject == prop) continue;
                if (!any) { art = renderer.bounds; any = true; }
                else art.Encapsulate(renderer.bounds);
            }
            Assert.That(any, Is.True, propName + " should have visible art.");
            Vector3 origin = new Vector3(art.center.x, art.max.y + 0.2f, art.min.z - 1f);
            bool hit = Physics.Raycast(origin, Vector3.forward, out RaycastHit result, art.size.z + 2f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Assert.That(hit && result.collider.transform.IsChildOf(prop.transform), Is.False,
                "A shot just above " + propName + " stopped in mid-air at " + (hit ? result.point.ToString("F2") : "-"));
        }

        private static bool TryArtBounds(Collider collider, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            var owner = collider.GetComponent<MeshRenderer>() != null || collider.transform.childCount > 0 ? collider.transform : null;
            if (owner == null) return false;
            foreach (var renderer in owner.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                if (!any) { bounds = renderer.bounds; any = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return any;
        }
    }
}
#endif
