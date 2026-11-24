using System.Collections.Generic;
using Lockdown.AI.Agents;
using NUnit.Framework;
using UnityEngine;

namespace Lockdown.Tests
{
    public sealed class PursuerDecisionTests
    {
        [Test]
        public void BurstWaitsForWarningThenFiresThreeShotsAndCoolsDown()
        {
            var cycle = new PursuerAttackCycle(new PursuerSettings()); int shots = 0;
            System.Func<bool> fire = () => { shots++; return true; };
            cycle.Tick(0f, true, fire); cycle.Tick(0.64f, true, fire);
            Assert.That(shots, Is.Zero); Assert.That(cycle.IsAiming, Is.True);
            cycle.Tick(0.66f, true, fire); cycle.Tick(0.85f, true, fire); cycle.Tick(1.04f, true, fire);
            Assert.That(shots, Is.EqualTo(3)); Assert.That(cycle.IsAiming, Is.False);
            cycle.Tick(2f, true, fire); Assert.That(shots, Is.EqualTo(3));
            cycle.Tick(2.6f, true, fire); Assert.That(cycle.IsAiming, Is.True);
            cycle.Tick(3.26f, true, fire); Assert.That(shots, Is.EqualTo(4));
        }
        [Test]
        public void OcclusionCancelsBurstAndReacquisitionNeedsAnotherWarning()
        {
            var cycle = new PursuerAttackCycle(new PursuerSettings()); int shots = 0;
            System.Func<bool> fire = () => { shots++; return true; };
            cycle.Tick(0f, true, fire); cycle.Tick(0.66f, true, fire);
            cycle.Tick(0.7f, false, fire); cycle.Tick(0.9f, true, fire);
            Assert.That(shots, Is.EqualTo(1)); Assert.That(cycle.IsAiming, Is.False);
            cycle.Tick(2.3f, true, fire); cycle.Tick(2.8f, true, fire);
            Assert.That(shots, Is.EqualTo(1));
        }
        [Test]
        public void BlockedWeaponAttemptCancelsAttackInsteadOfIgnoringFailure()
        {
            var cycle = new PursuerAttackCycle(new PursuerSettings()); int attempts = 0;
            System.Func<bool> blocked = () => { attempts++; return false; };
            cycle.Tick(0f, true, blocked); cycle.Tick(0.7f, true, blocked); cycle.Tick(0.8f, true, blocked);
            Assert.That(attempts, Is.EqualTo(1)); Assert.That(cycle.IsAiming, Is.False);
        }
        [Test]
        public void InvestigationInspectsAfterArrivalAndHasATravelTimeout()
        {
            var task = new PursuerInvestigation(); task.Begin(Vector3.one, 0f, 12f);
            Assert.That(task.Finished(false, 5f, 1.5f), Is.False);
            Assert.That(task.Finished(true, 5f, 1.5f), Is.False);
            Assert.That(task.Finished(true, 6.6f, 1.5f), Is.True);
            task.Begin(Vector3.zero, 10f, 2f);
            Assert.That(task.Finished(false, 12.1f, 1.5f), Is.True);
        }
        [Test]
        public void SearchSkipsUnreachablePositionsAndEndsAfterItsBudget()
        {
            var search = new PursuerSearch(); var center = new Vector3(2f, 0f, 3f);
            search.Begin(center, 1f, 14f, 3f);
            Assert.That(search.TryNext(point => point.x > center.x, out var result), Is.True);
            Assert.That(result.x, Is.GreaterThan(center.x));
            Assert.That(Vector3.Distance(center, result), Is.LessThanOrEqualTo(3.01f));
            Assert.That(search.Expired(14.9f), Is.False); Assert.That(search.Expired(15f), Is.True);
        }
        [Test]
        public void SearchCandidatesAreFiniteAndUnique()
        {
            var search = new PursuerSearch(); search.Begin(Vector3.zero, 0f, 14f, 3f);
            var points = new List<Vector3>();
            while (search.TryNext(_ => true, out var point))
            {
                foreach (var previous in points) Assert.That(Vector3.Distance(previous, point), Is.GreaterThan(0.1f));
                points.Add(point); Assert.That(points.Count, Is.LessThanOrEqualTo(17));
            }
            Assert.That(points.Count, Is.EqualTo(17));
        }
        [Test]
        public void EntirelyUnreachableSearchDoesNotLoopForever()
        {
            var search = new PursuerSearch(); search.Begin(Vector3.zero, 0f, 14f, 3f);
            int checks = 0;
            Assert.That(search.TryNext(_ => { checks++; return false; }, out _), Is.False);
            Assert.That(checks, Is.EqualTo(17));
        }
        [Test]
        public void PatrolUsesConeButTrackedPlayerSurvivesTurningAndWallsStillBlock()
        {
            var guard = new GameObject("Guard"); var player = new GameObject("Player"); GameObject wall = null;
            try
            {
                // Keep the unit scenario away from any currently open scene geometry.
                guard.transform.position = new Vector3(1000f, 100f, 1000f);
                var eyes = guard.AddComponent<PursuerPerception>(); eyes.Configure(new PursuerSettings());
                player.transform.position = guard.transform.position + Vector3.back * 3f;
                Assert.That(eyes.CanSee(player.transform, false), Is.False);
                Assert.That(eyes.CanSee(player.transform, true), Is.True);
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = guard.transform.position + Vector3.back * 1.5f + Vector3.up;
                wall.transform.localScale = new Vector3(4f, 3f, 0.2f); Physics.SyncTransforms();
                Assert.That(eyes.CanSee(player.transform, true), Is.False);
                wall.GetComponent<Collider>().isTrigger = true; Physics.SyncTransforms();
                Assert.That(eyes.CanSee(player.transform, true), Is.True);
                player.SetActive(false); Assert.That(eyes.CanSee(player.transform, true), Is.False);
            }
            finally { Object.DestroyImmediate(guard); Object.DestroyImmediate(player); if (wall != null) Object.DestroyImmediate(wall); }
        }
        [Test]
        public void FormationUsesDistinctSlotsAndFallsBackForUnreachableFlank()
        {
            var target = new GameObject("Target"); var a = new GameObject("Leader"); var b = new GameObject("Flanker"); var c = new GameObject("SecondFlanker");
            try
            {
                target.transform.position = new Vector3(1000f, 100f, 1000f);
                a.transform.position = b.transform.position = c.transform.position = target.transform.position;
                var first = a.AddComponent<PursuerFormation>(); var second = b.AddComponent<PursuerFormation>(); var third = c.AddComponent<PursuerFormation>();
                first.SetTarget(target.transform); second.SetTarget(target.transform); third.SetTarget(target.transform);
                Assert.That(first.TryFlank(target.transform.position, _ => true, out _), Is.False);
                Assert.That(second.TryFlank(target.transform.position, _ => true, out var left), Is.True);
                Assert.That(third.TryFlank(target.transform.position, _ => true, out var right), Is.True);
                Assert.That(Vector3.Distance(left, right), Is.GreaterThan(1f));
                Assert.That(second.TryFlank(target.transform.position, _ => false, out _), Is.False);
                first.enabled = false;
                Assert.That(second.TryFlank(target.transform.position, _ => true, out _), Is.False);
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(c); Object.DestroyImmediate(target); }
        }
    }
}
