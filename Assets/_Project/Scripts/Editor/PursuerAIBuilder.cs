using System;
using System.IO;
using System.Linq;
using Lockdown.AI.Agents;
using Lockdown.Player;
using Lockdown.Combat;
using Lockdown.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lockdown.Editor
{
    public static class PursuerAIBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Characters/Pursuer.prefab";
        private const string Folder = "Assets/_Project/Animations/Pursuer";

        [MenuItem("Tools/Lockdown/Connect Pursuer AI in Museum")]
        public static void Connect()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/Museum/Museum_Main.unity")
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            // Preserve unsaved work before saving the requested character connection.
            if (scene.isDirty)
            {
                Directory.CreateDirectory("Assets/_Recovery");
                EditorSceneManager.SaveScene(scene, "Assets/_Recovery/BeforePursuerAI_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity", true);
            }
            var player = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FirstPersonController>(true)).Single();
            var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            var oldVisual = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Pursuer_VisualOnly" || root.name == "Pursuer");
            var position = oldVisual != null ? oldVisual.transform.position : new Vector3(-1.3f, 0f, -7.4f);
            if (!NavMesh.SamplePosition(position, out var spawn, 1.5f, filter))
                throw new InvalidOperationException("No baked NavMesh near the pursuer spawn.");
            BuildPrefab();
            var route = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "PursuerPatrolRoute");
            if (route == null) { route = new GameObject("PursuerPatrolRoute"); SceneManager.MoveGameObjectToScene(route, scene); Undo.RegisterCreatedObjectUndo(route, "Create pursuer route"); }
            foreach (Transform child in route.transform.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
            var candidates = new[] { spawn.position, new Vector3(0f, 0f, -3f), new Vector3(3f, 0f, 0f), new Vector3(-3f, 0f, 0f) };
            foreach (var candidate in candidates)
            {
                if (!NavMesh.SamplePosition(candidate, out var point, 1.5f, filter)) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(spawn.position, point.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                var waypoint = new GameObject("Patrol_" + route.transform.childCount).transform;
                waypoint.SetParent(route.transform, false);
                waypoint.position = point.position;
            }
            if (route.transform.childCount < 2) throw new InvalidOperationException("Need at least two reachable patrol points.");
            var rotation = oldVisual != null ? oldVisual.transform.rotation : Quaternion.Euler(0f, 160f, 0f);
            if (oldVisual != null) Undo.DestroyObjectImmediate(oldVisual);
            var pursuer = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            pursuer.name = "Pursuer";
            pursuer.transform.SetPositionAndRotation(spawn.position, rotation);
            Undo.RegisterCreatedObjectUndo(pursuer, "Connect pursuer AI");
            var settings = new SerializedObject(pursuer.GetComponent<PursuerAgent>());
            settings.FindProperty("playerTarget").objectReferenceValue = player.transform;
            var points = settings.FindProperty("patrolPoints");
            points.arraySize = route.transform.childCount;
            for (int i = 0; i < points.arraySize; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = route.transform.GetChild(i);
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(pursuer.GetComponent<PursuerAgent>());
            var relay = scene.GetRootGameObjects().FirstOrDefault(root => root.GetComponent<PursuerStimulusRelay>() != null);
            if (relay == null)
            {
                relay = new GameObject("PursuerGameplaySignals");
                SceneManager.MoveGameObjectToScene(relay, scene);
                relay.AddComponent<PursuerStimulusRelay>();
                Undo.RegisterCreatedObjectUndo(relay, "Connect pursuer gameplay signals");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = pursuer;
            Debug.Log("Pursuer connected: full patrol/investigate/chase/attack/search/return/dead FSM, weapon, health and gameplay signals.");
        }

        private static void BuildPrefab()
        {
            var sourceClips = AssetDatabase.LoadAllAssetsAtPath(PursuerVisualBuilder.SourcePath).OfType<AnimationClip>().ToArray();
            var idle = Clip(sourceClips, "Idle");
            var walk = Clip(sourceClips, "Walk");
            var run = Clip(sourceClips, "Run_Gun");
            string controllerPath = Folder + "/PursuerLocomotion.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            foreach (var parameter in controller.parameters) controller.RemoveParameter(parameter);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            foreach (var old in machine.states) machine.RemoveState(old.state);
            foreach (var blend in AssetDatabase.LoadAllAssetsAtPath(controllerPath).OfType<BlendTree>()) Object.DestroyImmediate(blend, true);
            var state = machine.AddState("Locomotion");
            var tree = new BlendTree { name = "Idle Walk Run", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(idle, 0f); tree.AddChild(walk, 2f); tree.AddChild(run, 4f);
            state.motion = tree;
            machine.defaultState = state;
            AddReaction(controller, machine, "Shot", Clip(sourceClips, "Idle_Shoot", false), state, false);
            AddReaction(controller, machine, "Hit", Clip(sourceClips, "HitReact", false), state, false);
            AddReaction(controller, machine, "Death", Clip(sourceClips, "Death", false), state, true);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PursuerVisualBuilder.PrefabPath), preview);
                root.name = "Pursuer";
                var agent = root.AddComponent<NavMeshAgent>();
                agent.height = 1.9f; agent.radius = 0.3f; agent.speed = 2f;
                agent.acceleration = 8f; agent.angularSpeed = 360f; agent.stoppingDistance = 1.2f;
                root.AddComponent<PursuerAgent>();
                root.AddComponent<PursuerPerception>();
                root.AddComponent<PursuerFormation>();
                var animator = root.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                var locomotion = root.AddComponent<PursuerLocomotion>();
                var serialized = new SerializedObject(locomotion);
                serialized.FindProperty("animator").objectReferenceValue = animator;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var presentation = root.AddComponent<PursuerPresentation>();
                var presentationData = new SerializedObject(presentation);
                presentationData.FindProperty("animator").objectReferenceValue = animator;
                presentationData.ApplyModifiedPropertiesWithoutUndo();
                AddWeapon(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            AssetDatabase.SaveAssets();
        }

        private static void AddReaction(AnimatorController controller, AnimatorStateMachine machine, string trigger, AnimationClip clip, AnimatorState locomotion, bool terminal)
        {
            controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            var reaction = machine.AddState(trigger);
            reaction.motion = clip;
            var enter = machine.AddAnyStateTransition(reaction);
            enter.hasExitTime = false; enter.duration = 0.06f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            if (!terminal)
            {
                var exit = reaction.AddTransition(locomotion);
                exit.hasExitTime = true; exit.exitTime = 0.9f; exit.duration = 0.1f;
            }
        }

        private static void AddWeapon(GameObject root)
        {
            var muzzle = new GameObject("PursuerMuzzle").transform;
            muzzle.SetParent(root.transform, false); muzzle.localPosition = new Vector3(0f, 1.2f, 0.65f);
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "MuzzleFlash"; Object.DestroyImmediate(flash.GetComponent<Collider>());
            flash.transform.SetParent(muzzle, false); flash.transform.localScale = Vector3.one * 0.08f;
            const string materialPath = "Assets/_Project/Materials/Enemies/Pursuer/Pursuer_Muzzle.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor", new Color(1f, 0.65f, 0.12f));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            flash.GetComponent<Renderer>().sharedMaterial = material; flash.SetActive(false);
            var tracer = new GameObject("ShotTracer").AddComponent<LineRenderer>();
            tracer.transform.SetParent(root.transform, false); tracer.sharedMaterial = material;
            tracer.useWorldSpace = true; tracer.positionCount = 2; tracer.startWidth = 0.018f; tracer.endWidth = 0.008f; tracer.enabled = false;
            var audio = root.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 1f; audio.maxDistance = 20f;
            var shooting = root.AddComponent<EnemyShooting>();
            var data = new SerializedObject(shooting);
            data.FindProperty("muzzle").objectReferenceValue = muzzle;
            data.FindProperty("muzzleFlash").objectReferenceValue = flash;
            data.FindProperty("tracer").objectReferenceValue = tracer;
            data.FindProperty("shotAudio").objectReferenceValue = audio;
            data.FindProperty("controlledByAI").boolValue = true;
            data.FindProperty("shootingRange").floatValue = 8f;
            data.FindProperty("shotInterval").floatValue = 0.16f;
            data.FindProperty("firstShotDelay").floatValue = 0f;
            data.ApplyModifiedPropertiesWithoutUndo();
            audio.clip = Sound("PursuerShot", false);
            var presentation = new SerializedObject(root.GetComponent<PursuerPresentation>());
            presentation.FindProperty("warningSound").objectReferenceValue = Sound("PursuerWarning", true);
            presentation.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AudioClip Sound(string name, bool warning)
        {
            const string folder = "Assets/_Project/Audio/Pursuer";
            Directory.CreateDirectory(folder);
            string path = folder + "/" + name + ".wav";
            if (!File.Exists(path))
            {
                const int rate = 22050;
                int count = (int)(rate * (warning ? 0.2f : 0.12f));
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                    writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
                    writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                    var random = new System.Random(41);
                    for (int i = 0; i < count; i++)
                    {
                        float time = (float)i / rate;
                        float wave = warning ? Mathf.Sin(time * 880f * Mathf.PI * 2f) : (float)(random.NextDouble() * 2f - 1f);
                        writer.Write((short)(wave * Mathf.Exp(-time * (warning ? 12f : 45f)) * 12000f));
                    }
                }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static AnimationClip Clip(AnimationClip[] source, string name, bool loop = true)
        {
            var original = source.Single(clip => clip.name == name);
            string path = Folder + "/PursuerAI_" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = Object.Instantiate(original); AssetDatabase.CreateAsset(clip, path); }
            else EditorUtility.CopySerialized(original, clip);
            clip.name = "PursuerAI_" + name;
            clip.legacy = false;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }
    }
}
