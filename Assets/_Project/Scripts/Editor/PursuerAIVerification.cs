using System;
using System.Collections.Generic;
using System.IO;
using Lockdown.AI.Agents;
using Lockdown.Agents.Pursuer;
using Lockdown.AI.Core.Hound;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Player;
using Lockdown.Weapons;
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
        private static GameObject probe, wall, coverWall;
        private static Vector3 spawn, phaseStart, lastSeen;
        private static Vector3 interceptStart, interceptVelocity;
        private static Vector3 cornerLastSeen, cornerVelocity, confirmedBeforeHiding, cornerSearchGoal;
        private static double movingAt;
        private static bool sawIntercept;
        private static Quaternion rotation;
        private static double deadline;
        private static int phase, initialShots;
        private static float maximumTravel, maximumAnimation, playerHealth;
        private static bool previousBackground;
        private static int previousTargetFrameRate, previousVSync;
        private static float previousCaptureDelta;
        private static bool ownsFrameTiming;
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
                previousTargetFrameRate = Application.targetFrameRate; previousVSync = QualitySettings.vSyncCount;
                previousCaptureDelta = Time.captureDeltaTime; ownsFrameTiming = true;
                Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0; Time.captureDeltaTime = 0f;
                brain.Settings.searchDuration = 1.5f;
                brain.Settings.inspectionDuration = 0.3f;
                brain.Settings.lostTravelTimeout = 2.5f;
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
            EditorApplication.playModeStateChanged -= Entered;
            EditorApplication.update -= StartWhenReady;
            EditorApplication.update += StartWhenReady;
        }
        private static void StartWhenReady()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= StartWhenReady;
            Start();
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
            if (phase == 50)
            {
                probe.transform.position = interceptStart + interceptVelocity * (float)(Time.timeAsDouble - movingAt);
                sawIntercept |= brain.State == PursuerAgent.PursuerState.Intercept;
            }
            if (phase == 55)
                probe.transform.position = cornerLastSeen - cornerVelocity * 0.3f + cornerVelocity * (float)(Time.timeAsDouble - movingAt);
            if (phase == 57 && brain.State == PursuerAgent.PursuerState.Search && brain.HasReachedSearchArea)
            {
                try
                {
                    Require(Vector3.Distance(phaseStart, brain.transform.position) > 0.5f, "Guard actually travels around the museum corner to inspect its hypothesis");
                    Require(Vector3.Distance(brain.transform.position, cornerSearchGoal) < 0.8f, "Guard reaches the selected occluded corner rather than merely falling back to the old location");
                    Require(Vector3.Distance(brain.LastKnownPlayerPosition, confirmedBeforeHiding) < 0.01f, "Corner pursuit never reads the hidden target's new position");
                    Capture("PursuerCornerSearch"); Next(58, 0.6f);
                }
                catch (Exception error) { Finish(error); return; }
            }
            if (Time.timeAsDouble < deadline) return;
            try
            {
                switch (phase)
                {
                    case 0:
                        Require(brain.State == PursuerAgent.PursuerState.Patrol && !brain.IsPlayerVisible, "Out-of-range target does not interrupt patrol");
                        Require(maximumTravel > 0.3f && maximumAnimation > 0.1f, "Patrol moves and drives locomotion animation");
                        Warp(); FindInterceptionScenario();
                        probe.transform.position = interceptStart;
                        brain.transform.rotation = Quaternion.LookRotation(interceptStart - spawn);
                        nav.updatePosition = false; movingAt = Time.timeAsDouble; sawIntercept = false;
                        Next(50, 0.9f); break;
                    case 50:
                        Require(sawIntercept, "Observed fleeing movement selects Intercept using actual museum route lengths");
                        Require(brain.GetComponent<PursuerController>().TryRouteLength(spawn, brain.InterceptPoint, out float interceptionLength)
                            && interceptionLength > 0f, "Selected interception destination has a complete baked-NavMesh route");
                        Require(!brain.IsRetaliating && brain.GetComponent<EnemyShooting>().ShotsFired == 0, "Fleeing alone never authorizes shooting");
                        Capture("PursuerIntercept");
                        brain.enabled = false; brain.enabled = true; nav.updatePosition = true;
                        Warp(); probe.transform.position = VisiblePoint();
                        brain.transform.rotation = Quaternion.LookRotation(probe.transform.position - spawn);
                        phaseStart = spawn; Next(1, 0.65f); break;
                    case 1:
                        Require((brain.State == PursuerAgent.PursuerState.Approach || brain.State == PursuerAgent.PursuerState.Arrest) && brain.IsPlayerVisible, "First sight approaches for arrest instead of shooting");
                        Require(nav.speed == brain.Settings.approachSpeed && Vector3.Distance(phaseStart, brain.transform.position) > 0.15f,
                            "Approach moves at configured speed [speed=" + nav.speed + ", travelled=" + Vector3.Distance(phaseStart, brain.transform.position)
                            + ", route=" + brain.GetComponent<PursuerController>().RouteStatus() + ", position=" + brain.transform.position + "]");
                        Require(!brain.IsRetaliating && brain.GetComponent<EnemyShooting>().ShotsFired == 0, "Unarmed contact never authorizes weapon fire");
                        nav.updateRotation = false;
                        brain.transform.rotation = Quaternion.LookRotation(brain.transform.position - probe.transform.position);
                        Next(2, 0.15f); break;
                    case 2:
                        Require(brain.IsPlayerVisible, "Navigation turns do not lose a confirmed target");
                        nav.updateRotation = true; nav.updatePosition = false; Warp(); brain.transform.position = spawn;
                        lastSeen = brain.LastKnownPlayerPosition;
                        Block(probe.transform.position); Next(3, 0.2f); break;
                    case 3:
                        Require(!brain.IsPlayerVisible && brain.State == PursuerAgent.PursuerState.Chase, "Wall blocks sight while short-term pursuit is retained");
                        probe.transform.position = spawn + Vector3.right * 1000f;
                        Next(4, brain.Settings.sightMemoryDuration + 0.25f); break;
                    case 4:
                        Require(brain.State == PursuerAgent.PursuerState.Search, "Loss of contact transitions to a timed Search");
                        Require(!brain.HasReachedSearchArea, "Guard keeps travelling after sight memory expires; inspection budget has not started");
                        Require(Vector3.Distance(brain.LastKnownPlayerPosition, lastSeen) < 0.01f, "Hidden target movements never update visual memory");
                        Object.DestroyImmediate(wall); wall = null; nav.updatePosition = true; Warp();
                        Next(5, brain.Settings.lostTravelTimeout + brain.Settings.searchDuration + 0.5f); break;
                    case 5:
                        Require(brain.State == PursuerAgent.PursuerState.Return || brain.State == PursuerAgent.PursuerState.Patrol, "Search expires and returns toward patrol");
                        brain.enabled = false; brain.enabled = true; Warp(); FindCornerScenario();
                        brain.Settings.lostTravelTimeout = 6f;
                        nav.updatePosition = false;
                        probe.transform.position = cornerLastSeen - cornerVelocity * 0.3f;
                        brain.transform.rotation = Quaternion.LookRotation(probe.transform.position - spawn);
                        movingAt = Time.timeAsDouble; Next(55, 0.3f); break;
                    case 55:
                        Require(brain.IsPlayerVisible && brain.ObservedVelocity.magnitude > 1.2f, "Guard observes actual motion toward a museum corner before hiding");
                        confirmedBeforeHiding = brain.LastKnownPlayerPosition;
                        probe.transform.position = spawn + Vector3.right * 1000f;
                        Next(56, 0.2f); break;
                    case 56:
                        Require(!brain.IsPlayerVisible && brain.State == PursuerAgent.PursuerState.Intercept,
                            "Loss of sight preserves a forward/corner hypothesis instead of discarding it");
                        Require(brain.GetComponent<PursuerNavigation>().IsScreened(confirmedBeforeHiding, brain.SearchDestination),
                            "Predicted search area is behind actual corner geometry, not just the old visible point");
                        Require(brain.GetComponent<PursuerController>().TryRouteLength(spawn, brain.SearchDestination, out float cornerRoute),
                            "Guard chooses a complete navigation route to the hidden corner area");
                        cornerSearchGoal = brain.SearchDestination;
                        phaseStart = brain.transform.position; nav.updatePosition = true;
                        Next(57, brain.Settings.sightMemoryDuration + brain.Settings.lostTravelTimeout + 1f); break;
                    case 57:
                        throw new InvalidOperationException("Guard did not reach a predicted corner search area before its bounded travel timeout");
                    case 58:
                        Require(brain.State == PursuerAgent.PursuerState.Search, "Guard keeps inspecting after arrival instead of immediately returning to patrol");
                        Next(59, brain.Settings.searchDuration + 0.3f); break;
                    case 59:
                        Require(brain.State == PursuerAgent.PursuerState.Return || brain.State == PursuerAgent.PursuerState.Patrol,
                            "Corner search eventually gives up after a full inspection budget");
                        brain.enabled = false; brain.enabled = true; Warp();
                        HoundNoiseBus.Emit(spawn, HoundSoundKind.Distraction);
                        Next(6, 0.2f); break;
                    case 6:
                        Require(brain.State == PursuerAgent.PursuerState.Investigate, "A distraction noise starts Investigation");
                        Warp(); Next(7, 0.6f); break;
                    case 7:
                        Require(brain.State == PursuerAgent.PursuerState.Search, "Inspecting the clue starts a local Search");
                        var controller = originalTarget.GetComponent<FirstPersonController>(); controller.enabled = true; controller.CaptureCursor();
                        var body = originalTarget.GetComponent<CharacterController>(); body.enabled = false;
                        originalTarget.position = lastSeen; body.enabled = true;
                        controller.PlayerCamera.transform.LookAt(spawn + Vector3.up * 1.2f);
                        Assign(originalTarget); Warp();
                        brain.transform.rotation = Quaternion.LookRotation(originalTarget.position - spawn);
                        initialShots = brain.GetComponent<EnemyShooting>().ShotsFired;
                        playerHealth = originalTarget.GetComponent<Health>().CurrentHealth;
                        Next(8, 0.25f); break;
                    case 8:
                        Require(brain.State == PursuerAgent.PursuerState.Approach || brain.State == PursuerAgent.PursuerState.Arrest, "Actual player is approached for handcuffing on first sight");
                        Require(brain.GetComponent<EnemyShooting>().ShotsFired == initialShots, "Guard does not shoot an unprovoked player");
                        Next(80, 0.65f); break;
                    case 80:
                        Require(brain.State == PursuerAgent.PursuerState.Arrest && brain.ArrestProgress > 0f, "Staying within reach starts a visible arrest countdown");
                        Capture("PursuerArrest");
                        nav.updatePosition = false; nav.updateRotation = false; Warp(); brain.transform.position = spawn;
                        Next(81, 0.2f); break;
                    case 81:
                        Require(brain.ArrestProgress == 0f && !originalTarget.GetComponent<PlayerVitals>().IsCaptured, "Breaking reach cancels the handcuff attempt");
                        brain.enabled = false; brain.enabled = true; Warp();
                        brain.transform.rotation = Quaternion.LookRotation(spawn - originalTarget.position);
                        originalTarget.GetComponent<FirstPersonController>().CaptureCursor();
                        Next(82, 0.15f); break;
                    case 82:
                        Require(!brain.IsPlayerVisible && !brain.IsRetaliating, "Guard has not seen the player behind it before the hit");
                        Require(originalTarget.GetComponent<PlayerEquipment>().TryEquipPistol(), "Actual player equips the pistol for retaliation verification");
                        originalTarget.GetComponent<FirstPersonController>().PlayerCamera.transform.LookAt(spawn + Vector3.up * 1.2f);
                        AddForwardCover();
                        Physics.SyncTransforms();
                        float guardHealth = brain.GetComponent<Health>().CurrentHealth;
                        Require(originalTarget.GetComponentInChildren<FirstPersonWeapon>(true).TryFire(), "Actual player pistol fires from behind the guard");
                        Require(brain.GetComponent<Health>().CurrentHealth == guardHealth - 25f, "Actual bullet damages the guard");
                        Require(brain.IsRetaliating, "Attributed player hit authorizes retaliation even from behind");
                        nav.updateRotation = true; Next(83, 0.2f); break;
                    case 83:
                        Require(brain.State == PursuerAgent.PursuerState.AdvanceCover, "Attacked guard selects nearby forward cover against real solid geometry");
                        Require(brain.GetComponent<EnemyShooting>().ShotsFired == initialShots, "Aim warning precedes the first retaliatory bullet");
                        Capture("PursuerAimWarning"); Next(9, 1.7f); break;
                    case 9:
                        Require(brain.GetComponent<EnemyShooting>().ShotsFired > initialShots, "Attack decision executes actual weapon fire");
                        Require(originalTarget.GetComponent<Health>().CurrentHealth < playerHealth, "Weapon fire damages actual player Health");
                        Require(brain.State != PursuerAgent.PursuerState.AdvanceCover, "Brief cover expires and guard resumes armed pressure");
                        Object.DestroyImmediate(coverWall); coverWall = null;
                        Capture("PursuerAttack");
                        nav.updatePosition = true; phaseStart = brain.transform.position;
                        Next(90, 0.7f); break;
                    case 90:
                        Require(Vector3.Distance(brain.transform.position, originalTarget.position) <= brain.Settings.combatStopDistance + 0.3f
                            || Vector3.Distance(phaseStart, brain.transform.position) > 0.1f, "Armed guard closes distance instead of camping");
                        Next(91, 0.1f); break;
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
                    case 11:
                        Require(brain.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Death"), "Death animation plays on imported character");
                        Capture("PursuerDeath");
                        Object.DestroyImmediate(brain.gameObject);
                        var fresh = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PursuerAIBuilder.PrefabPath), spawn, rotation);
                        brain = fresh.GetComponent<PursuerAgent>(); nav = fresh.GetComponent<NavMeshAgent>(); Assign(originalTarget);
                        brain.Settings.arrestDuration = 0.8f;
                        nav.updatePosition = false;
                        var playerBody = originalTarget.GetComponent<CharacterController>(); playerBody.enabled = false;
                        originalTarget.position = spawn + (lastSeen - spawn).normalized * 1.1f;
                        playerBody.enabled = true;
                        brain.transform.rotation = Quaternion.LookRotation(originalTarget.position - spawn);
                        Physics.SyncTransforms(); playerHealth = originalTarget.GetComponent<Health>().CurrentHealth;
                        Next(12, 1.2f); break;
                    case 12:
                        Require(originalTarget.GetComponent<PlayerVitals>().IsCaptured && brain.State == PursuerAgent.PursuerState.Caught, "Remaining within handcuff reach ends the encounter as Caught");
                        Require(originalTarget.GetComponent<Health>().CurrentHealth == playerHealth, "Handcuffing captures without inflicting lethal damage");
                        Require(!originalTarget.GetComponent<FirstPersonController>().enabled
                            && !originalTarget.GetComponentInChildren<FirstPersonWeapon>(true).enabled, "Capture disables player movement and weapon controls");
                        Require(!originalTarget.GetComponentInChildren<FirstPersonWeapon>(true).TryFire(), "Captured player cannot fire the pistol");
                        originalTarget.GetComponent<PlayerVitals>().RestartEncounter();
                        Next(13, 0.6f); break;
                    case 13:
                        var restartedPlayer = Object.FindAnyObjectByType<FirstPersonController>();
                        brain = Object.FindAnyObjectByType<PursuerAgent>();
                        Require(restartedPlayer != null && restartedPlayer.enabled && !restartedPlayer.GetComponent<PlayerVitals>().IsCaptured
                            && restartedPlayer.GetComponent<Health>().CurrentHealth == 100f && !brain.IsRetaliating, "Restart clears arrest and retaliation and restores the player");
                        Finish(null);
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
        private static void AddForwardCover()
        {
            Vector3 direction = originalTarget.position - spawn; direction.y = 0f; direction.Normalize();
            Vector3 candidate = spawn + direction + Vector3.Cross(Vector3.up, direction) * 1.5f;
            coverWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            coverWall.name = "Pursuer_ForwardCoverVerification";
            coverWall.transform.SetPositionAndRotation((candidate + originalTarget.position) * 0.5f + Vector3.up * 1.3f,
                Quaternion.LookRotation(originalTarget.position - candidate));
            coverWall.transform.localScale = new Vector3(0.8f, 2.6f, 0.12f);
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
        private static void FindInterceptionScenario()
        {
            var actions = brain.GetComponent<PursuerController>();
            var navigation = brain.GetComponent<PursuerNavigation>();
            for (int ring = 3; ring <= 5; ring += 2)
            for (int direction = 0; direction < 8; direction++)
            for (int travel = 0; travel < 8; travel++)
            {
                float angle = direction * Mathf.PI / 4f, heading = travel * Mathf.PI / 4f;
                var start = spawn + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
                var velocity = new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading)) * 4f;
                if (Vector3.Dot((start - spawn).normalized, velocity.normalized) < -0.1f) continue;
                if (!navigation.HasClearLine(spawn + Vector3.up * 1.2f, start + Vector3.up * 1.2f)
                    || !navigation.HasClearLine(spawn + Vector3.up * 1.2f, start + velocity * 0.9f + Vector3.up * 1.2f)
                    || !actions.TryRouteLength(start, start + velocity * 0.9f, out _)) continue;
                var planner = new PursuerInterception();
                planner.Observe(start - velocity * 0.1f, 0f); planner.Observe(start, 0.1f);
                if (!planner.TryChoose(spawn, 0.1f, brain.Settings, actions.TryRouteLength, out _)) continue;
                interceptStart = start; interceptVelocity = velocity; return;
            }
            throw new InvalidOperationException("No visible reachable interception scenario near museum spawn");
        }
        private static void FindCornerScenario()
        {
            var actions = brain.GetComponent<PursuerController>();
            var navigation = brain.GetComponent<PursuerNavigation>();
            var settings = new PursuerSettings();
            for (int ring = 2; ring <= 8; ring += 2)
            for (int direction = 0; direction < 16; direction++)
            for (int travel = 0; travel < 8; travel++)
            {
                float angle = direction * Mathf.PI / 8f, heading = travel * Mathf.PI / 4f;
                Vector3 last = spawn + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
                Vector3 velocity = new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading)) * 4f;
                if (!navigation.HasClearLine(spawn + Vector3.up * 1.2f, last + Vector3.up * 1.2f)
                    || !navigation.HasClearLine(spawn + Vector3.up * 1.2f, last - velocity * 0.3f + Vector3.up * 1.2f)
                    || !actions.TryRouteLength(spawn, last, out _)) continue;
                var areas = PursuerCornerSearch.Build(spawn, last, velocity, settings, actions.TryRouteLength, actions.IsScreened);
                if (!actions.IsScreened(last, areas[0]) || !actions.IsScreened(spawn, areas[0])
                    || !actions.TryRouteLength(spawn, areas[0], out float length) || length > 12f) continue;
                cornerLastSeen = last; cornerVelocity = velocity; return;
            }
            throw new InvalidOperationException("No reachable occluded museum corner for lost-contact verification");
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message + " [state=" + (brain != null ? brain.State.ToString() + ", visible=" + brain.IsPlayerVisible + ", observed velocity=" + brain.ObservedVelocity + ", reason=" + brain.DecisionReason : "missing") + "]");
            checks.Add("PASS: " + message);
        }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            if (probe != null) Object.DestroyImmediate(probe);
            if (wall != null) Object.DestroyImmediate(wall);
            if (coverWall != null) Object.DestroyImmediate(coverWall);
            Application.runInBackground = previousBackground;
            if (ownsFrameTiming)
            {
                Application.targetFrameRate = previousTargetFrameRate; QualitySettings.vSyncCount = previousVSync;
                Time.captureDeltaTime = previousCaptureDelta; ownsFrameTiming = false;
            }
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
