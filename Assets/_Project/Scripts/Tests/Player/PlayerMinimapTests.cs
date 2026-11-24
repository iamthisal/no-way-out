using Lockdown.Player;
using NUnit.Framework;
using UnityEngine;

namespace Lockdown.Tests
{
    public sealed class PlayerMinimapTests
    {
        private static readonly Vector2 Min = new Vector2(-10f, -20f);
        private static readonly Vector2 Max = new Vector2(10f, 20f);

        [Test]
        public void WorldToMapPutsCornersAndCentreInPlace()
        {
            AssertNear(new Vector2(0f, 0f), PlayerMinimap.WorldToMap(new Vector3(-10f, 0f, -20f), Min, Max));
            AssertNear(new Vector2(1f, 1f), PlayerMinimap.WorldToMap(new Vector3(10f, 5f, 20f), Min, Max));
            AssertNear(new Vector2(0.5f, 0.5f), PlayerMinimap.WorldToMap(Vector3.zero, Min, Max));
        }

        [Test]
        public void WorldToMapIgnoresHeightAndKeepsNorthUp()
        {
            Vector2 low = PlayerMinimap.WorldToMap(new Vector3(5f, -3f, 10f), Min, Max);
            Vector2 high = PlayerMinimap.WorldToMap(new Vector3(5f, 30f, 10f), Min, Max);
            AssertNear(low, high);
            Assert.That(low.x, Is.GreaterThan(0.5f), "East should be to the right.");
            Assert.That(low.y, Is.GreaterThan(0.5f), "North should be up.");
        }

        [Test]
        public void WorldToMapClampsOutsidePositionsToTheEdge()
        {
            AssertNear(new Vector2(0f, 1f), PlayerMinimap.WorldToMap(new Vector3(-50f, 0f, 99f), Min, Max));
        }

        [Test]
        public void MapRectSitsTopLeftWithThePlanAspect()
        {
            var go = new GameObject("Minimap");
            try
            {
                var minimap = go.AddComponent<PlayerMinimap>();
                minimap.Configure(null, null, Min, Max);
                Rect rect = minimap.MapRect(1000f);
                Assert.That(rect.x, Is.EqualTo(16f));
                Assert.That(rect.y, Is.EqualTo(16f));
                Assert.That(rect.width / rect.height, Is.EqualTo(0.5f).Within(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void AssertNear(Vector2 expected, Vector2 actual)
        {
            Assert.That(Vector2.Distance(expected, actual), Is.LessThan(1e-4f), "Expected " + expected + " but was " + actual);
        }
    }
}
