using System;
using System.Linq;
using Lockdown.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Lockdown.Editor
{
    /// <summary>Connects the hound's death clip and adds corpse removal to the museum enemies. Safe to run again.</summary>
    public static class EnemyDeathBuilder
    {
        public const string DeathTrigger = "Death";
        private const string HoundModelPath = "Assets/_Project/Models/Hound/MuseumHound.fbx";
        private const string HoundControllerPath = "Assets/_Project/Animations/Hound/HoundIdle.controller";
        private const string DeathTake = "Hound_Rig|Hound_Death";
        private const string DeathClip = "Hound_Death";
        private const string Characters = "Assets/_Project/Prefabs/Characters/";

        [MenuItem("Tools/Lockdown/Import Hound Death Clip")]
        public static void ImportHoundDeathClip()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(HoundModelPath);
            var take = importer.defaultClipAnimations.FirstOrDefault(clip => clip.takeName == DeathTake);
            if (take == null) throw new InvalidOperationException(DeathTake + " is not in the FBX. Export the model with All Actions on.");
            // The explicit clip list keeps idle, walk and run exactly as they are.
            var clips = importer.clipAnimations.Where(clip => clip.name != DeathClip).ToList();
            take.name = DeathClip;
            take.loopTime = false;
            take.loopPose = false;
            clips.Add(take);
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }

        [MenuItem("Tools/Lockdown/Connect Hound Death Animation")]
        public static void ConnectHoundDeath()
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(HoundModelPath).OfType<AnimationClip>().FirstOrDefault(asset => asset.name == DeathClip);
            if (clip == null) throw new InvalidOperationException("Import the hound death clip first.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(HoundControllerPath);
            var machine = controller.layers[0].stateMachine;
            if (controller.parameters.All(parameter => parameter.name != DeathTrigger))
                controller.AddParameter(DeathTrigger, AnimatorControllerParameterType.Trigger);
            var state = machine.states.Select(child => child.state).FirstOrDefault(child => child.name == DeathTrigger);
            if (state == null) state = machine.AddState(DeathTrigger, new Vector3(304f, 260f));
            state.motion = clip;
            state.speed = 1f;
            state.speedParameterActive = false;
            foreach (var old in machine.anyStateTransitions.Where(transition => transition.destinationState == state).ToArray())
                machine.RemoveAnyStateTransition(old);
            var enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.08f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, DeathTrigger);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Lockdown/Add Corpse Removal to Enemies")]
        public static void AddCorpseRemoval()
        {
            AddTo(Characters + "InvestigatingHound.prefab");
            AddTo(Characters + "Pursuer.prefab");
            AddTo(Characters + "SkeletonArcher.prefab");
            AssetDatabase.SaveAssets();
        }

        private static void AddTo(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!AddRemoval(root)) return;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>For the character builders, so a rebuilt prefab keeps corpse removal. Returns true when it was added.</summary>
        public static bool AddRemoval(GameObject root)
        {
            if (root.GetComponent<CorpseRemoval>() != null) return false;
            root.AddComponent<CorpseRemoval>();
            return true;
        }
    }
}
