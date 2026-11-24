#if UNITY_EDITOR
using System.Collections;
using Lockdown.Combat;
using Lockdown.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Lockdown.Tests.Director
{
    public sealed class GlassAlarmTests
    {
        private GameObject root, first, second;
        private GlassAlarm alarm;

        [SetUp]
        public void SetUp()
        {
            first = Glass("First", new Vector3(3f, 1f, 0f));
            second = Glass("Second", new Vector3(-4f, 1f, 2f));
            root = new GameObject("Alarm");
            root.SetActive(false);
            root.AddComponent<AudioSource>();
            alarm = root.AddComponent<GlassAlarm>();
            var data = new SerializedObject(alarm);
            var sources = data.FindProperty("glassSources");
            sources.arraySize = 2;
            sources.GetArrayElementAtIndex(0).objectReferenceValue = first.GetComponent<BreakableGlass>();
            sources.GetArrayElementAtIndex(1).objectReferenceValue = second.GetComponent<BreakableGlass>();
            data.FindProperty("durationSeconds").floatValue = 0.5f;
            data.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        private static GameObject Glass(string name, Vector3 position)
        {
            var glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glass.name = name;
            glass.transform.position = position;
            glass.AddComponent<Health>();
            glass.AddComponent<BreakableGlass>();
            return glass;
        }

        [UnityTest]
        public IEnumerator BreakingGlassRingsTheAlarmWithThePositionOfTheBreak()
        {
            int raised = 0, cleared = 0;
            Vector3 reported = Vector3.zero;
            alarm.AlarmRaised += position => { raised++; reported = position; };
            alarm.AlarmCleared += () => cleared++;
            yield return null;
            Assert.That(alarm.IsRinging, Is.False);
            first.GetComponent<Health>().TakeDamage(1000f);
            Assert.That(alarm.IsRinging, Is.True);
            Assert.That(raised, Is.EqualTo(1));
            Assert.That(reported, Is.EqualTo(first.transform.position));
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.That(alarm.IsRinging, Is.False);
            Assert.That(cleared, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ASecondBreakRestartsTheTimerAndMovesTheReport()
        {
            yield return null;
            first.GetComponent<Health>().TakeDamage(1000f);
            yield return new WaitForSecondsRealtime(0.35f);
            second.GetComponent<Health>().TakeDamage(1000f);
            Assert.That(alarm.LatestPosition, Is.EqualTo(second.transform.position));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(alarm.IsRinging, Is.True, "The second break should restart the 0.5 s timer.");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(alarm.IsRinging, Is.False);
        }
    }
}
#endif
