#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Lockdown.AI.Agents;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Intro;
using Lockdown.Player;
using Lockdown.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lockdown.Tests
{
    public sealed class IntroCutsceneTests
    {
        // Behind the flower pot in the courtyard.
        private static readonly Vector3 Garden = new Vector3(1.6f, 0f, 18.75f);
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
            IntroCutscene.Requested = false;
            Time.timeScale = 1f;
            Application.runInBackground = previousBackground;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static IEnumerator LoadMuseum(bool withIntro)
        {
            IntroCutscene.Requested = withIntro;
            EditorSceneManager.LoadSceneInPlayMode(SceneFlow.MuseumScene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
        }

        private static IntroCutscene Cutscene() => Object.FindAnyObjectByType<IntroCutscene>();

        private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        private static IEnumerator SkipAndWait()
        {
            var cutscene = Cutscene();
            cutscene.Skip();
            for (float waited = 0f; cutscene.IsPlaying && waited < 3f; waited += Time.unscaledDeltaTime) yield return null;
            Assert.That(cutscene.IsPlaying, Is.False, "Skipping must end the intro.");
            yield return null;
        }

        private static Transform Bone(string actor, string name) => GameObject.Find(actor).GetComponentsInChildren<Transform>().First(bone => bone.name == name);

        private static IEnumerator Seek(PlayableDirector director, float time)
        {
            director.time = time;
            director.Evaluate();
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartGamePlaysTheIntroBeforeAnythingElseStarts()
        {
            EditorSceneManager.LoadSceneInPlayMode(SceneFlow.MainMenuScene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Object.FindAnyObjectByType<MainMenu>().StartGame();
            yield return null;
            yield return null;
            var cutscene = Cutscene();
            Assert.That(cutscene, Is.Not.Null);
            Assert.That(cutscene.IsPlaying, Is.True);
            Assert.That(IntroCutscene.Requested, Is.False, "The request is used up.");
            Assert.That(Object.FindAnyObjectByType<FirstPersonController>(), Is.Null);
            Assert.That(Object.FindAnyObjectByType<PursuerAgent>(), Is.Null);
            Assert.That(Object.FindAnyObjectByType<SkeletonArcherAgent>(FindObjectsInactive.Include).gameObject.activeInHierarchy, Is.False);
            Assert.That(Object.FindAnyObjectByType<SpawnDirectorAgent>(), Is.Null);
            Assert.That(Object.FindAnyObjectByType<PlayerMinimap>(), Is.Null);
            Assert.That(Object.FindObjectsByType<AudioListener>().Count(listener => listener.isActiveAndEnabled), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SkippingStartsTheGameBehindTheFlowerPot()
        {
            yield return LoadMuseum(true);
            Assert.That(Cutscene().IsPlaying, Is.True);
            yield return SkipAndWait();
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            Assert.That(player, Is.Not.Null);
            Assert.That(Flat(player.transform.position, Garden), Is.LessThan(0.3f));
            Assert.That(player.IsCursorCaptured, Is.True);
            Assert.That(player.GetComponent<PauseMenu>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<PursuerAgent>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<SpawnDirectorAgent>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<PlayerMinimap>(), Is.Not.Null);
            Assert.That(GameObject.Find("MuseumThief"), Is.Null, "The film actors must be gone.");
            Assert.That(Object.FindObjectsByType<AudioListener>().Count(listener => listener.isActiveAndEnabled), Is.EqualTo(1));
            // Everything started fresh: the guards run and nothing has hurt the player.
            yield return new WaitForSeconds(0.5f);
            Assert.That(player.GetComponent<Health>().CurrentHealth, Is.EqualTo(player.GetComponent<Health>().MaxHealth));
        }

        [UnityTest]
        public IEnumerator TheIntroEndsByItself()
        {
            yield return LoadMuseum(true);
            var cutscene = Cutscene();
            Assert.That(cutscene.Duration, Is.InRange(20f, 35f), "It should stay a short clip.");
            Time.timeScale = 10f;
            for (float waited = 0f; cutscene.IsPlaying && waited < 10f; waited += Time.unscaledDeltaTime) yield return null;
            Time.timeScale = 1f;
            Assert.That(cutscene.IsPlaying, Is.False);
            Assert.That(Object.FindAnyObjectByType<FirstPersonController>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator LoadingTheMuseumDirectlyOrRestartingPlaysNoIntro()
        {
            yield return LoadMuseum(false);
            Assert.That(Cutscene().IsPlaying, Is.False);
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            Assert.That(Flat(player.transform.position, Garden), Is.LessThan(0.3f), "Every run starts in the garden.");
            player.GetComponent<PauseMenu>().Restart();
            yield return null;
            yield return null;
            Assert.That(Cutscene().IsPlaying, Is.False);
            Assert.That(Object.FindAnyObjectByType<FirstPersonController>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ThePlayerCanLeaveTheGardenOnceTheGlassDoorBreaks()
        {
            yield return LoadMuseum(false);
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            foreach (var guard in Object.FindObjectsByType<PursuerAgent>()) guard.enabled = false;
            yield return new WaitForSeconds(0.5f);
            Assert.That(player.IsGrounded, Is.True);
            var path = new NavMeshPath();
            Vector3 exhibition = new Vector3(0f, 0.05f, 11.5f);
            Assert.That(NavMesh.SamplePosition(player.transform.position, out var here, 0.6f, NavMesh.AllAreas), Is.True, "The start must be on the walkable floor.");
            NavMesh.CalculatePath(here.position, exhibition, NavMesh.AllAreas, path);
            Assert.That(path.status, Is.Not.EqualTo(NavMeshPathStatus.PathComplete), "The glass door starts closed.");
            GameObject.Find("Courtyard_LockedGlassDoor").GetComponent<Health>().TakeDamage(1000f);
            yield return new WaitForSeconds(0.5f);
            Assert.That(NavMesh.CalculatePath(here.position, exhibition, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
        }

        [UnityTest]
        public IEnumerator TheThiefTouchesTheLadderTheRoofAndTheGarden()
        {
            yield return LoadMuseum(true);
            var director = Object.FindAnyObjectByType<PlayableDirector>();
            yield return Seek(director, 2f);
            Assert.That(GameObject.Find("MuseumThief"), Is.Null, "Hidden inside the van while it drives.");
            Assert.That(GameObject.Find("HeistLadder"), Is.Null);
            Assert.That(GameObject.Find("Van_RackLadder"), Is.Not.Null);

            yield return Seek(director, 14.05f);
            Assert.That(GameObject.Find("Van_RackLadder"), Is.Null);
            Transform ladder = Bone("HeistLadder", "Ladder");
            foreach (string side in new[] { "L", "R" })
            {
                Vector3 hand = Bone("MuseumThief", "Hand_" + side).position, foot = Bone("MuseumThief", "Foot_" + side).position;
                Assert.That(DistanceToLadder(ladder, hand), Is.LessThan(0.25f), side + " hand");
                Assert.That(DistanceToLadder(ladder, foot), Is.LessThan(0.30f), side + " foot");
            }
            yield return Seek(director, 18.2f);
            Assert.That(LowestFoot(), Is.EqualTo(3.81f + 0.09f).Within(0.05f), "Standing on the roof.");
            yield return Seek(director, 22.8f);
            Assert.That(LowestFoot(), Is.EqualTo(3.81f + 0.09f).Within(0.06f));
            Assert.That(Bone("MuseumThief", "Hips").position.z, Is.InRange(21.5f, 22.6f), "At the courtyard edge.");
            yield return Seek(director, 25.1f);
            Assert.That(LowestFoot(), Is.EqualTo(0.09f).Within(0.05f), "Landed in the garden.");
            yield return Seek(director, 28.3f);
            Assert.That(Flat(Bone("MuseumThief", "Hips").position, Garden), Is.LessThan(0.35f), "Hiding where the player starts.");
            var film = Object.FindAnyObjectByType<Camera>();
            Assert.That(Vector3.Distance(film.transform.position, Garden + Vector3.up * 1.65f), Is.LessThan(0.25f), "The last shot ends at the player's eyes.");
        }

        private static float LowestFoot() => Mathf.Min(Bone("MuseumThief", "Foot_L").position.y, Bone("MuseumThief", "Foot_R").position.y);

        private static float DistanceToLadder(Transform ladder, Vector3 point)
        {
            // The rails run from the foot of the ladder to the roof edge and beyond.
            Vector3 foot = ladder.position, along = (new Vector3(0f, 3.83f, 30.365f) - foot).normalized;
            Vector3 offset = point - foot;
            offset -= along * Vector3.Dot(offset, along);
            offset.x = Mathf.Max(0f, Mathf.Abs(offset.x) - 0.215f);
            return offset.magnitude;
        }

        /// <summary>Saves frames of the real playback for a visual check.</summary>
        [UnityTest, Explicit]
        public IEnumerator CaptureFrames()
        {
            yield return LoadMuseum(true);
            var director = Object.FindAnyObjectByType<PlayableDirector>();
            var film = Object.FindAnyObjectByType<Camera>();
            Directory.CreateDirectory("Temp/Intro");
            var texture = new RenderTexture(960, 540, 24);
            float[] times = { 0.8f, 2f, 2.7f, 3.5f, 4.4f, 5.1f, 5.7f, 6.1f, 6.5f, 6.9f, 7.4f, 8.2f, 8.8f, 9.2f, 10f, 10.7f, 11.5f, 12.3f, 13f, 14f, 15f, 16f, 16.8f, 17.2f, 17.6f, 18.2f,
                18.7f, 20f, 21.6f, 22.3f, 23.3f, 23.8f, 24.1f, 24.4f, 24.65f, 24.9f, 25.4f, 25.8f, 26.5f, 27f, 27.6f, 28.2f };
            foreach (float time in times)
            {
                // Resume from just before, so skinning and shadows are those of a normal frame.
                yield return Seek(director, time - 0.02f);
                film.targetTexture = texture;
                yield return new WaitForEndOfFrame();
                RenderTexture.active = texture;
                var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                image.Apply();
                File.WriteAllBytes("Temp/Intro/f_" + Mathf.RoundToInt(time * 100f).ToString("0000") + ".png", image.EncodeToPNG());
                RenderTexture.active = null;
                film.targetTexture = null;
                Object.Destroy(image);
            }
            Object.Destroy(texture);
        }
    }
}
#endif
