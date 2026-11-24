#if UNITY_EDITOR
using System.Collections;
using Lockdown.AI.Agents;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Lockdown.Tests.Director
{
    public sealed class AlarmResponseTests
    {
        [UnityTest]
        public IEnumerator AGuardWhoCanWalkThereAnswersTheAlarm()
        {
            yield return MuseumFixture.Load();
            MuseumFixture.Director.enabled = false;
            // Out of everyone's sight, so only the alarm can move the guard.
            MuseumFixture.MovePlayer(new Vector3(-14.6f, 0.05f, 3.6f), Quaternion.Euler(0f, 180f, 0f));
            // The placed guard is shut in the Lobby, and its anti-tunnelling check undoes teleports.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/Pursuer.prefab");
            var guard = Object.Instantiate(prefab, new Vector3(0f, 0.03f, 24f), Quaternion.identity).GetComponent<PursuerAgent>();
            guard.Assign(MuseumFixture.Player.transform, null);
            yield return null;
            var glass = MuseumFixture.CaseNear(new Vector3(6.3f, 0f, 29.3f));
            MuseumFixture.Break(glass);
            yield return null;
            yield return null;
            Assert.That(MuseumFixture.Relay.Responder, Is.EqualTo(guard));
            Assert.That(MuseumFixture.Relay.IsUnanswered, Is.False);
            Assert.That(guard.IsAnsweringAlarm, Is.True, guard.DecisionReason);
            float deadline = Time.time + 20f;
            while (Time.time < deadline && Flat(guard.transform.position, glass.transform.position) > 2.2f) yield return null;
            Assert.That(Flat(guard.transform.position, glass.transform.position), Is.LessThan(2.2f), "The guard must arrive beside the broken case.");
        }

        [UnityTest]
        public IEnumerator AGuardBehindClosedDoorsLeavesTheAlarmUnanswered()
        {
            yield return MuseumFixture.Load();
            MuseumFixture.Director.enabled = false;
            yield return new WaitForSeconds(0.5f);
            MuseumFixture.Break(MuseumFixture.CaseNear(new Vector3(6.3f, 0f, 29.3f)));
            yield return null;
            Assert.That(MuseumFixture.Alarm.IsRinging, Is.True);
            Assert.That(MuseumFixture.Relay.IsUnanswered, Is.True);
            Assert.That(MuseumFixture.Guard.IsAnsweringAlarm, Is.False);
        }

        [UnityTest]
        public IEnumerator TheReportIsASnapshotOfTheGlassAndDoesNotFollowThePlayer()
        {
            yield return MuseumFixture.Load();
            MuseumFixture.Director.enabled = false;
            var glass = MuseumFixture.CaseNear(new Vector3(6.3f, 0f, 29.3f));
            MuseumFixture.Break(glass);
            MuseumFixture.MovePlayer(new Vector3(-14.6f, 0.05f, 3.6f), Quaternion.identity);
            yield return new WaitForSeconds(0.5f);
            Assert.That(MuseumFixture.Relay.LatestPosition, Is.EqualTo(glass.transform.position));
        }

        private static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }
    }
}
#endif
