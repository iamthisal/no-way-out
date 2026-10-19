using System;
using System.IO;
using Lockdown.Combat;
using Lockdown.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lockdown.Editor
{
    public static class GlassDisplayBuilder
    {
        public const string BreakSoundPath = "Assets/_Project/Audio/Glass/DisplayGlassBreak.wav";

        [MenuItem("Tools/Lockdown/Configure First Breakable Display")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                scene.path != "Assets/_Project/Scenes/Museum/Museum_Main.unity")
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            var display = HammerBuilder.Find(scene, "P_Exhibition_CaseEast");
            var figure = display != null ? display.Find("Homem De Ferro") : null;
            var glass = display != null ? display.Find("LP_Exhibition_WallCase 1/M_Museum_DisplayGlass") : null;
            if (glass == null || figure == null) throw new InvalidOperationException("The east exhibition display or its figure is missing.");
            if (glass.GetComponent<BreakableGlass>() != null) return;
            var renderer = glass.GetComponent<Renderer>();
            if (renderer == null) throw new InvalidOperationException("The glass renderer is missing.");
            CreateBreakSound();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure breakable display");
            var placeholder = display.GetComponent<Collider>();
            if (placeholder != null) { Undo.RecordObject(placeholder, "Disable solid placeholder"); placeholder.enabled = false; }
            var placeholderRenderer = display.GetComponent<Renderer>();
            if (placeholderRenderer != null) { Undo.RecordObject(placeholderRenderer, "Hide placeholder"); placeholderRenderer.enabled = false; }

            var collider = Undo.AddComponent<BoxCollider>(glass.gameObject);
            collider.center = renderer.localBounds.center;
            collider.size = renderer.localBounds.size;
            var health = Undo.AddComponent<Health>(glass.gameObject);
            var serializedHealth = new SerializedObject(health);
            serializedHealth.FindProperty("maxHealth").floatValue = 50f;
            serializedHealth.ApplyModifiedProperties();
            var breakable = Undo.AddComponent<BreakableGlass>(glass.gameObject);
            var effect = CreateEffect(display, renderer);
            var serialized = new SerializedObject(breakable);
            serialized.FindProperty("intactRenderers").arraySize = 1;
            serialized.FindProperty("intactRenderers").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("blockingColliders").arraySize = 1;
            serialized.FindProperty("blockingColliders").GetArrayElementAtIndex(0).objectReferenceValue = collider;
            serialized.FindProperty("breakEffect").objectReferenceValue = effect;
            serialized.FindProperty("breakSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(BreakSoundPath);
            serialized.ApplyModifiedProperties();

            // Preserve the wooden base and thin frame after the glass is gone.
            foreach (var surface in display.Find("LP_Exhibition_WallCase 1").GetComponentsInChildren<MeshFilter>())
            {
                if (surface.transform == glass || surface.GetComponent<Collider>() != null) continue;
                var frameCollider = Undo.AddComponent<MeshCollider>(surface.gameObject);
                frameCollider.sharedMesh = surface.sharedMesh;
            }
            var lootCollider = Undo.AddComponent<BoxCollider>(figure.gameObject);
            var bounds = HammerBuilder.BoundsIn(figure, figure.GetComponentsInChildren<Renderer>());
            lootCollider.center = bounds.center;
            lootCollider.size = bounds.size;
            var loot = Undo.AddComponent<LootItem>(figure.gameObject);
            serialized = new SerializedObject(loot);
            serialized.FindProperty("itemName").stringValue = "Armored Helmet";
            serialized.FindProperty("value").intValue = 500;
            serialized.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("East exhibition glass now protects a $500 Armored Helmet. Save the scene.");
        }

        private static ParticleSystem CreateEffect(Transform display, Renderer glass)
        {
            var effectObject = new GameObject("GlassShatter");
            Undo.RegisterCreatedObjectUndo(effectObject, "Create glass effect");
            effectObject.transform.SetParent(display, false);
            effectObject.transform.position = glass.bounds.center;
            var particles = effectObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.05f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            main.gravityModifier = 1f;
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.7f, 0.5f, 0.7f);
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = glass.sharedMaterial;
            return particles;
        }

        private static void CreateBreakSound()
        {
            if (File.Exists(BreakSoundPath)) return;
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Audio/Glass"))
                AssetDatabase.CreateFolder("Assets/_Project/Audio", "Glass");
            // A small synthesized shatter: decaying noise and several ringing frequencies.
            const int rate = 22050;
            const int count = rate / 2;
            var random = new System.Random(71);
            using (var writer = new BinaryWriter(File.Create(BreakSoundPath)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / rate;
                    double tone = Math.Sin(t * 2 * Math.PI * 2700) + 0.4 * Math.Sin(t * 2 * Math.PI * 4300);
                    double value = ((random.NextDouble() * 2 - 1) * 0.65 + tone * 0.2) * Math.Exp(-t * 12);
                    writer.Write((short)(Math.Clamp(value, -1, 1) * short.MaxValue * 0.7));
                }
            }
            AssetDatabase.ImportAsset(BreakSoundPath);
        }
    }
}
