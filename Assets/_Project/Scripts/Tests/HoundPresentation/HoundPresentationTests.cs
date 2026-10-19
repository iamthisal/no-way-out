#if UNITY_EDITOR
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Lockdown.Tests
{
    public sealed class HoundPresentationTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/MuseumHound.prefab";
        private GameObject dog;

        [UnityTest]
        public IEnumerator PrefabHasACompleteDogRigAndCorrectMaterialsAtMetreScale()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            dog = Object.Instantiate(prefab, new Vector3(200f, 0f, 200f), Quaternion.identity);
            yield return null;
            var skins = dog.GetComponentsInChildren<SkinnedMeshRenderer>();
            Assert.That(skins.Length, Is.EqualTo(1));
            Assert.That(skins[0].bones.Length, Is.InRange(20, 32));
            Assert.That(skins[0].sharedMesh.vertexCount, Is.LessThan(15000));
            uint indices = 0;
            for (int i = 0; i < skins[0].sharedMesh.subMeshCount; i++) indices += skins[0].sharedMesh.GetIndexCount(i);
            Assert.That(indices / 3, Is.InRange(2000, 8000));
            Assert.That(skins[0].bounds.size.y, Is.InRange(0.8f, 1.4f));
            Assert.That(skins[0].bounds.min.y - dog.transform.position.y, Is.InRange(-0.025f, 0.025f));
            Assert.That(skins[0].sharedMaterials.Length, Is.EqualTo(8));
            foreach (var material in skins[0].sharedMaterials)
            {
                Assert.That(material, Is.Not.Null);
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            }
            Assert.That(FindBone("Head"), Is.Not.Null);
            Assert.That(FindBone("TailBase"), Is.Not.Null);
            Assert.That(FindBone("Head").position.z, Is.GreaterThan(FindBone("TailBase").position.z), "The model should face Unity +Z.");
            foreach (string side in new[] { "L", "R" })
            {
                Assert.That(FindBone("FrontPaw_" + side), Is.Not.Null);
                Assert.That(FindBone("HindPaw_" + side), Is.Not.Null);
            }
            Assert.That(dog.GetComponent<CapsuleCollider>(), Is.Not.Null);
            Assert.That(dog.GetComponentsInChildren<MonoBehaviour>(true).Length, Is.Zero, "This milestone is the dog presentation only.");
        }

        [UnityTest]
        public IEnumerator IdleAnimatesTheTailAndLoopsWithoutMovingTheDogRoot()
        {
            dog = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), new Vector3(200f, 0f, 200f), Quaternion.identity);
            yield return null;
            var animator = dog.GetComponentInChildren<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            var clips = animator.runtimeAnimatorController.animationClips;
            Assert.That(clips.Length, Is.EqualTo(1));
            Assert.That(clips[0].isLooping, Is.True);
            Assert.That(clips[0].length, Is.InRange(2.3f, 2.5f));
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Play("HoundIdle", 0, 0f);
            animator.Update(0f);
            var start = FindBone("TailBase").localRotation;
            var rootPosition = dog.transform.position;
            animator.Play("HoundIdle", 0, 0.25f);
            animator.Update(0f);
            Assert.That(Quaternion.Angle(start, FindBone("TailBase").localRotation), Is.GreaterThan(1f));
            animator.Update(clips[0].length * 1.5f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("HoundIdle"), Is.True);
            Assert.That(Vector3.Distance(dog.transform.position, rootPosition), Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator MuseumContainsOneHoundOnTheCentralHallFloor()
        {
            if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("3c09e9fafae22354683897089c02f979")))
            {
                LogAssert.Expect(LogType.Error, new Regex("^Problem detected while opening the Scene file: 'Assets/_Project/Scenes/Museum/Museum_Main.unity'"));
                LogAssert.Expect(LogType.Error, "Prefab instance problem. Missing Prefab Asset: 'Exit_MuseumDoubleDoor (Missing Prefab with guid: 3c09e9fafae22354683897089c02f979)'");
            }
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            int count = 0;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "MuseumHound")
                {
                    count++;
                    Assert.That(root.transform.position.y, Is.InRange(-0.05f, 0.15f));
                    Assert.That(root.transform.position.x, Is.InRange(-4f, 4f));
                    Assert.That(root.transform.position.z, Is.InRange(-1f, 4f));
                    Assert.That(root.GetComponentInChildren<Animator>().runtimeAnimatorController, Is.Not.Null);
                    Assert.That(root.GetComponent<CapsuleCollider>().enabled, Is.True);
                }
            Assert.That(count, Is.EqualTo(1));
        }

        private Transform FindBone(string name)
        {
            foreach (var transform in dog.GetComponentsInChildren<Transform>()) if (transform.name == name) return transform;
            return null;
        }

        [TearDown]
        public void Cleanup() { if (dog != null) Object.DestroyImmediate(dog); }
    }
}
#endif
