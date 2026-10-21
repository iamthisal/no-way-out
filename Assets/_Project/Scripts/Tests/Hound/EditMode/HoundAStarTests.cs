using System.Collections.Generic;
using Lockdown.AI.Core.Hound;
using NUnit.Framework;
using UnityEngine;

namespace Lockdown.Tests.Hound
{
    public sealed class HoundAStarTests
    {
        // Lower branch costs 4; upper branch costs 2 + sqrt(8) + 2 = 6.828427.
        private readonly List<Vector3> points = new List<Vector3>
        {
            new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(4, 0, 0),
            new Vector3(0, 0, 2), new Vector3(2, 0, 2)
        };
        private readonly List<List<int>> edges = new List<List<int>>
        {
            new List<int> { 1, 3 }, new List<int> { 0, 2 }, new List<int> { 1, 4 },
            new List<int> { 0, 4 }, new List<int> { 3, 2 }
        };
        private readonly List<int> route = new List<int>();

        [Test]
        public void ChoosesKnownCheaperRoute()
        {
            Assert.That(HoundAStar.FindPath(points, edges, 0, 2, null, route, out float cost), Is.True);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, route);
            Assert.That(cost, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void BlockedConnectionUsesKnownDetour()
        {
            Assert.That(HoundAStar.FindPath(points, edges, 0, 2,
                (a, b) => !(a == 1 && b == 2), route, out float cost), Is.True);
            CollectionAssert.AreEqual(new[] { 0, 3, 4, 2 }, route);
            Assert.That(cost, Is.EqualTo(4f + Mathf.Sqrt(8f)).Within(0.0001f));
        }

        [Test]
        public void NoConnectionReturnsNoRoute()
        {
            route.Add(99);
            Assert.That(HoundAStar.FindPath(points, edges, 0, 2, (a, b) => b != 2, route, out float cost), Is.False);
            Assert.That(route, Is.Empty);
            Assert.That(cost, Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void DirectEdgeAndIdenticalEndpoints()
        {
            Assert.That(HoundAStar.FindPath(points, edges, 0, 1, null, route, out float cost), Is.True);
            CollectionAssert.AreEqual(new[] { 0, 1 }, route);
            Assert.That(cost, Is.EqualTo(2f));
            Assert.That(HoundAStar.FindPath(points, edges, 3, 3, null, route, out cost), Is.True);
            CollectionAssert.AreEqual(new[] { 3 }, route);
            Assert.That(cost, Is.Zero);
        }

        [Test]
        public void ReopeningDoorRestoresShorterRoute()
        {
            HoundAStar.FindPath(points, edges, 0, 2, (a, b) => !(a == 1 && b == 2), route, out float closedCost);
            HoundAStar.FindPath(points, edges, 0, 2, null, route, out float openCost);
            Assert.That(openCost, Is.LessThan(closedCost));
            Assert.That(openCost, Is.EqualTo(4f));
        }
    }
}
