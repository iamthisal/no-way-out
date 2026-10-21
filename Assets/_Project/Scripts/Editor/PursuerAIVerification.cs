using System;
using System.Collections.Generic;
using System.IO;
using Lockdown.AI.Agents;
using Lockdown.Agents.Pursuer;
using Lockdown.AI.Core.Hound;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.Profiling;
using Object = UnityEngine.Object;

namespace Lockdown.Editor
{
    public static class PursuerAIVerification
    {
        private static PursuerAgent brain;
        private static NavMeshAgent nav;
        private static Transform originalTarget;
        private static GameObject probe, wall;
        private static Vector3 spawn, phaseStart, lastSeen;
        private static Quaternion rotation;
        private static double deadline;
        private static int phase, initialShots;
        private static float maximumTravel, maximumAnimation, playerHealth;
        private static bool previousBackground;
        private static ProfilerRecorder decisionRecorder;
        private static readonly List<string> checks = new List<string>();
        private static readonly string Report = Path.GetFullPath("Logs/PursuerAIVerification.txt");
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Museum/Museum_Main.unity");
            Start();
        }
        public static void Start()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.playModeStateChanged -= Entered;
                EditorApplication.playModeStateChanged += Entered;
                EditorApplication.isPlaying = true; return;
            }
            if (brain != null) return;
            checks.Clear();
            try
            {
                brain = Object.FindAnyObjectByType<PursuerAgent>();
                Require(brain != null, "Pursuer FSM exists in Museum_Main");
                Require(brain.GetComponent<PursuerController>() != null, "Separate runtime controller is connected to the pursuer brain");
                nav = brain.GetComponent<NavMeshAgent>();
                Require(nav.isOnNavMesh, "Pursuer is bound to the baked museum NavMesh");
                var data = new SerializedObject(brain);
                originalTarget = (Transform)data.FindProperty("playerTarget").objectReferenceValue;
                Require(originalTarget != null && originalTarget.GetComponent<FirstPersonController>() != null, "Actual player is assigned");
                Require(data.FindProperty("patrolPoints").arraySize == 4, "Four patrol points are assigned");
                Require(brain.GetComponent<EnemyShooting>() != null && brain.GetComponent<Health>() != null, "Weapon and damage systems are connected");
                Require(Object.FindAnyObjectByType<PursuerStimulusRelay>() != null, "Gameplay noise relay is present");
                spawn = brain.transform.position; rotation = brain.transform.rotation;
                previousBackground = Application.runInBackground; Application.runInBackground = true;
                brain.Settings.searchDuration = 1.5f;
                brain.Settings.inspectionDuration = 0.3f;
                probe = new GameObject("Pursuer_TestTarget"); probe.transform.position = spawn + Vector3.right * 1000f;
                Assign(probe.transform);
                brain.enabled = false; brain.enabled = true;
                phaseStart = spawn; maximumTravel = maximumAnimation = 0f;
                phase = 0; deadline = Time.timeAsDouble + 3;
                decisionRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Pursuer.Decision", 2048);
                File.WriteAllText(Report, "RUNNING\n");
                EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish(error); }
        }
        private static void Entered(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= Entered; EditorApplication.delayCall += Start;
        }
        private static void Assign(Transform target)
        {
            var data = new SerializedObject(brain);
            data.FindProperty("playerTarget").objectReferenceValue = target;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Warp()
        {
            var navigation = brain.GetComponent<PursuerNavigation>();
            navigation.enabled = false; nav.Warp(spawn); navigation.enabled = true; nav.ResetPath();
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying) { Finish(new InvalidOperationException("Preview stopped during verification")); return; }
            if (phase == 0)
            {
                maximumTravel = Mathf.Max(maximumTravel, Vector3.Distance(phaseStart, brain.transform.position));
                maximumAnimation = Mathf.Max(maximumAnimation, brain.GetComponentInChildren<Animator>().GetFloat("Speed"));
            }
            if (Time.timeAsDouble < deadline) return;
            try
            {
                switch (phase)
                {
                    case 0:
                        Require(brain.State == PursuerAgent.PursuerState.Patrol && !brain.IsPlayerVisible, "Out-of-range target does not interrupt patrol");
                        Require(maximumTravel > 0.3f && maximumAnimation > 0.1f, "Patrol moves and drives locomotion animation");
                        Warp(); probe.transform.position = VisiblePoint();
                        brain.transform.rotation = Quaternion.LookRotation(probe.transform.position - spawn);
                        phaseStart = spawn; Next(1, 0.65f); break;
                    case 1:
                        Require(brain.State == PursuerAgent.PursuerState.Chase && brain.IsPlayerVisible, "Clear sight acquires the target and starts Chase");
                        Require(nav.speed == 4f && Vector3.Distance(phaseStart, brain.transform.position) > 0.15f, "Chase moves at configured speed");
                        nav.updateRotation = false;
                        brain.transform.rotation = Quaternion.LookRotation(brain.transform.position - probe.transform.position);
                        Next(2, 0.15f); break;
                    case 2:
                        Require(brain.IsPlayerVisible && brain.State == PursuerAgent.PursuerState.Chase, "Navigation turns do not lose a confirmed target");
                        nav.updateRotation = true; nav.updatePosition = false; Warp(); brain.transform.position = spawn;
                        lastSeen = brain.LastKnownPlayerPosition;
                        Block(probe.transform.position); Next(3, 0.2f); break;
                    case 3:
                        Require(!brain.IsPlayerVisible && brain.State == PursuerAgent.PursuerState.Chase, "Wall blocks sight while short-term pursuit is retained");
                        probe.transform.position = spawn + Vector3.right * 1000f;
                        Next(4, brain.Settings.sightMemoryDuration + 0.25f); break;
                    case 4:
                        Require(brain.State == PursuerAgent.PursuerState.Search, "Loss of contact transitions to a timed Search");
                        Require(Vector3.Distance(brain.LastKnownPlayerPosition, lastSeen) < 0.01f, "Hidden target movements never update visual memory");
                        Object.DestroyImmediate(wall); wall = null; nav.updatePosition = true; Warp();
                        Next(5, 2f); break;
                    case 5:
                        Require(brain.State == PursuerAgent.PursuerState.Return || brain.State == PursuerAgent.PursuerState.Patrol, "Search expires and returns toward patrol");
                        brain.enabled = false; brain.enabled = true; Warp();
                        HoundNoiseBus.Emit(spawn, HoundSoundKind.Distraction);
                        Next(6, 0.2f); break;
                    case 6:
                        Require(brain.State == PursuerAgent.PursuerState.Investigate, "A distraction noise starts Investigation");
                        Warp(); Next(7, 0.6f); break;
                    case 7:
                        Require(brain.State == PursuerAgent.PursuerState.Search, "Inspecting the clue starts a local Search");
                        var controller = originalTarget.GetComponent<FirstPersonController>(); controller.enabled = false;
                        var body = originalTarget.GetComponent<CharacterController>(); body.enabled = false;
                        originalTarget.position = lastSeen; body.enabled = true;
                        controller.PlayerCamera.transform.LookAt(spawn + Vector3.up * 1.2f);
                        Assign(originalTarget); Warp();
                        brain.transform.rotation = Quaternion.LookRotation(originalTarget.position - spawn);
                        initialShots = brain.GetComponent<EnemyShooting>().ShotsFired;
                        playerHealth = originalTarget.GetComponent<Health>().CurrentHealth;
                        Next(8, 0.25f); break;
                    case 8:
                        Require(brain.State == PursuerAgent.PursuerState.Attack, "Visible healthy player in range selects Attack");
                        Require(brain.GetComponent<EnemyShooting>().ShotsFired == initialShots, "Aim warning precedes the first bullet");
                        Capture("PursuerAimWarning");
                        Next(9, 1.2f); break;
                    case 9:
                        Require(brain.GetComponent<EnemyShooting>().ShotsFired > initialShots, "Attack decision executes actual weapon fire");
                        Require(originalTarget.GetComponent<Health>().CurrentHealth < playerHealth, "Weapon fire damages actual player Health");
                        Capture("PursuerAttack");
                        Next(91, 0.2f); break;
                    case 91:
                        nav.updatePosition = false; Warp(); brain.transform.position = spawn;
                        Block(originalTarget.position);
                        initialShots = brain.GetComponent<EnemyShooting>().ShotsFired;
                        Next(10, 0.25f); break;
                    case 10:
                        Require(!brain.GetComponent<EnemyShooting>().TryFireControlled(originalTarget), "Solid blocker prevents actual controlled weapon damage");
                        Require(brain.GetComponent<EnemyShooting>().ShotsFired == initialShots, "Occlusion cancels remaining burst shots");
                        Object.DestroyImmediate(wall); wall = null;
                        brain.GetComponent<Health>().TakeDamage(1000f);
                        Require(brain.State == PursuerAgent.PursuerState.Dead && !nav.enabled, "Lethal damage enters Dead and disables navigation");
                        Require(!brain.GetComponent<EnemyShooting>().enabled, "Death disables the weapon");
                        Require(!brain.GetComponent<Collider>().enabled && !brain.GetComponent<PursuerFormation>().enabled, "Death removes collision and releases formation slot");
                        Next(11, 0.3f); break;
                    default:
                        if (phase == 11)
                        {
                            Require(brain.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Death"), "Death animation plays on imported character");
                            Capture("PursuerDeath");
                            Next(12, 0.2f);
                        }
                        else Finish(null);
                        break;
                }
            }
            catch (Exception error) { Finish(error); }
        }
        private static void Next(int value, float seconds) { phase = value; deadline = Time.timeAsDouble + seconds; }
        private static void Capture(string name)
        {
            // Camera rendering also works when no Game view is open in batch mode.
            var camera = originalTarget.GetComponent<FirstPersonController>().PlayerCamera;
            camera.transform.LookAt(brain.transform.position + Vector3.up * 1.1f);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(960, 640, 24);
            var picture = new Texture2D(960, 640, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                picture.ReadPixels(new Rect(0f, 0f, 960f, 640f), 0, 0); picture.Apply();
                File.WriteAllBytes(Path.GetFullPath("Logs/" + name + ".png"), picture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(picture);
            }
        }
        private static void Block(Vector3 target)
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Pursuer_TestWall";
            wall.transform.SetPositionAndRotation((spawn + target) * 0.5f + Vector3.up * 1.3f, Quaternion.LookRotation(target - spawn));
            wall.transform.localScale = new Vector3(4f, 3f, 0.15f);
            Physics.SyncTransforms();
        }
        private static Vector3 VisiblePoint()
        {
            foreach (var offset in new[] { Vector3.right, Vector3.back, Vector3.forward, Vector3.left })
            {
                if (!NavMesh.SamplePosition(spawn + offset * 3f, out var hit, 0.5f, nav.areaMask)) continue;
                if (!brain.GetComponent<PursuerNavigation>().CanReach(hit.position)) continue;
                Vector3 from = spawn + Vector3.up * 1.2f, direction = hit.position + Vector3.up * 1.2f - from;
                bool blocked = false;
                foreach (var obstacle in Physics.RaycastAll(from, direction.normalized, direction.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    if (!obstacle.transform.IsChildOf(brain.transform)) blocked = true;
                if (!blocked) return hit.position;
            }
            throw new InvalidOperationException("No reachable clear verification target");
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message + " [state=" + (brain != null ? brain.State.ToString() : "missing") + "]");
            checks.Add("PASS: " + message);
        }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            if (probe != null) Object.DestroyImmediate(probe);
            if (wall != null) Object.DestroyImmediate(wall);
            Application.runInBackground = previousBackground;
            if (decisionRecorder.Valid)
            {
                var samples = decisionRecorder.ToArray();
                double total = 0, maximum = 0;
                foreach (var sample in samples) { total += sample.Value; maximum = Math.Max(maximum, sample.Value); }
                checks.Add("PROFILE: one pursuer, mixed museum integration scenario, " + samples.Length + " recorded frames; average decision "
                    + (samples.Length > 0 ? total / samples.Length / 1000d : 0).ToString("F2") + " us; maximum " + (maximum / 1000d).ToString("F2") + " us. Rendering, animation and managed allocation costs are not included.");
                decisionRecorder.Dispose();
            }
            File.WriteAllText(Report, (error == null ? "PASSED" : "FAILED: " + error) + "\n" + string.Join("\n", checks));
            brain = null; EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
