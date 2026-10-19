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

        private sealed class LootSpec
        {
            public readonly string Path;
            public readonly string Name;
            public readonly int Value;
            public LootSpec(string path, string name, int value) { Path = path; Name = name; Value = value; }
        }

        private sealed class DisplaySpec
        {
            public readonly string Root;
            public readonly LootSpec[] Items;
            public DisplaySpec(string root, params LootSpec[] items) { Root = root; Items = items; }
        }

        private static readonly DisplaySpec[] Displays =
        {
            new DisplaySpec("P_Exhibition_CaseEast", new LootSpec("Homem De Ferro", "Armored Helmet", 500)),
            new DisplaySpec("P_Exhibition_CaseWest", new LootSpec("Marble_Bust", "Marble Bust", 400)),
            new DisplaySpec("P_StoneAge_WallCase", new LootSpec("Scull", "Ancient Skull", 350)),
            new DisplaySpec("P_Egypt_WallCase", new LootSpec("LP_Egypt_WallCase/Terracotta", "Egyptian Vessel", 450)),
            new DisplaySpec("P_GreeceRome_WallCase",
                new LootSpec("LP_GreeceRome_WallCase/Terracotta", "Classical Clay Vessel", 300),
                new LootSpec("LP_GreeceRome_WallCase/M_Museum_DeepBlue", "Classical Blue Vase", 350)),
            new DisplaySpec("P_AsianHeritage_WallCase",
                new LootSpec("LP_AsianHeritage_WallCase/Terracotta", "Heritage Clay Vessel", 300),
                new LootSpec("LP_AsianHeritage_WallCase/M_Museum_DeepBlue", "Heritage Blue Vase", 350)),
            new DisplaySpec("P_NaturalHistory_WallCase",
                new LootSpec("LP_NaturalHistory_WallCase/Terracotta", "Specimen Vessel", 300),
                new LootSpec("LP_NaturalHistory_WallCase/M_Museum_DeepBlue", "Blue Specimen Vessel", 350))
        };

        [MenuItem("Tools/Lockdown/Configure First Breakable Display")]
        public static void Build() { ConfigureDisplays(false); }

        [MenuItem("Tools/Lockdown/Configure All Breakable Exhibits")]
        public static void BuildAll() { ConfigureDisplays(true); }

        private static void ConfigureDisplays(bool all)
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                scene.path != "Assets/_Project/Scenes/Museum/Museum_Main.unity")
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            int count = all ? Displays.Length : 1;
            var roots = new Transform[count];
            var panes = new Renderer[count];
            // Resolve every model before making changes to the shared scene.
            for (int i = 0; i < count; i++)
            {
                var spec = Displays[i];
                roots[i] = HammerBuilder.Find(scene, spec.Root);
                if (roots[i] == null) throw new InvalidOperationException("Missing display: " + spec.Root);
                foreach (var renderer in roots[i].GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.name == "M_Museum_DisplayGlass") { panes[i] = renderer; break; }
                if (panes[i] == null) throw new InvalidOperationException("Missing glass: " + spec.Root);
                foreach (var item in spec.Items)
                {
                    var model = roots[i].Find(item.Path);
                    if (model == null || model.GetComponentsInChildren<Renderer>(true).Length == 0)
                        throw new InvalidOperationException("Missing exhibit model: " + spec.Root + "/" + item.Path);
                }
            }
            CreateBreakSound();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure breakable museum exhibits");
            int items = 0;
            for (int i = 0; i < count; i++)
            {
                ConfigureDisplay(roots[i], panes[i], Displays[i]);
                items += Displays[i].Items.Length;
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Configured " + count + " glass cases and " + items + " protected loot items. Save the scene.");
        }

        private static void ConfigureDisplay(Transform display, Renderer renderer, DisplaySpec spec)
        {
            var placeholder = display.GetComponent<Collider>();
            if (placeholder != null) { Undo.RecordObject(placeholder, "Disable solid placeholder"); placeholder.enabled = false; }
            var placeholderRenderer = display.GetComponent<Renderer>();
            if (placeholderRenderer != null) { Undo.RecordObject(placeholderRenderer, "Hide placeholder"); placeholderRenderer.enabled = false; }
            var glass = renderer.transform;
            var collider = glass.GetComponent<BoxCollider>();
            if (collider == null) collider = Undo.AddComponent<BoxCollider>(glass.gameObject);
            Undo.RecordObject(collider, "Fit glass blocker");
            collider.center = renderer.localBounds.center;
            collider.size = renderer.localBounds.size;
            if (glass.GetComponent<Health>() == null)
            {
                var health = Undo.AddComponent<Health>(glass.gameObject);
                var serializedHealth = new SerializedObject(health);
                serializedHealth.FindProperty("maxHealth").floatValue = 50f;
                serializedHealth.ApplyModifiedProperties();
            }
            var breakable = glass.GetComponent<BreakableGlass>();
            if (breakable == null) breakable = Undo.AddComponent<BreakableGlass>(glass.gameObject);
            var effect = display.Find("GlassShatter");
            var particles = effect != null ? effect.GetComponent<ParticleSystem>() : CreateEffect(display, renderer);
            var serialized = new SerializedObject(breakable);
            serialized.FindProperty("intactRenderers").arraySize = 1;
            serialized.FindProperty("intactRenderers").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("blockingColliders").arraySize = 1;
            serialized.FindProperty("blockingColliders").GetArrayElementAtIndex(0).objectReferenceValue = collider;
            serialized.FindProperty("breakEffect").objectReferenceValue = particles;
            serialized.FindProperty("breakSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(BreakSoundPath);
            serialized.ApplyModifiedProperties();

            // Mark artifact meshes before adding frame collision, so pottery stays loot.
            foreach (var item in spec.Items)
            {
                var model = display.Find(item.Path);
                var lootCollider = model.GetComponent<BoxCollider>();
                if (lootCollider == null) lootCollider = Undo.AddComponent<BoxCollider>(model.gameObject);
                Undo.RecordObject(lootCollider, "Fit exhibit collider");
                var bounds = HammerBuilder.BoundsIn(model, model.GetComponentsInChildren<Renderer>(true));
                lootCollider.center = bounds.center;
                lootCollider.size = bounds.size;
                var loot = model.GetComponent<LootItem>();
                if (loot == null) loot = Undo.AddComponent<LootItem>(model.gameObject);
                serialized = new SerializedObject(loot);
                serialized.FindProperty("itemName").stringValue = item.Name;
                serialized.FindProperty("value").intValue = item.Value;
                serialized.FindProperty("displayGlass").objectReferenceValue = breakable;
                serialized.ApplyModifiedProperties();
            }
            foreach (var surface in glass.parent.GetComponentsInChildren<MeshFilter>(true))
            {
                if (surface.transform == glass || surface.GetComponentInParent<LootItem>() != null ||
                    surface.GetComponent<Collider>() != null) continue;
                var frameCollider = Undo.AddComponent<MeshCollider>(surface.gameObject);
                frameCollider.sharedMesh = surface.sharedMesh;
            }
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
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24, 1, 0.01f) });
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
