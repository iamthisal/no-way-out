#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using Lockdown.Combat;
using Lockdown.Player;
using Lockdown.Weapons;
using Lockdown.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Lockdown.Tests
{
    public sealed class HammerTests
    {
        private GameObject fixture;
        private FirstPersonController player;
        private PlayerEquipment equipment;
        private PlayerInteraction interaction;
        private FirstPersonHammer hammer;
        private Keyboard keyboard;
        private Mouse mouse;
        private bool previousBackground;
        private InputSettings.BackgroundBehavior previousInputBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            previousBackground = Application.runInBackground;
            previousInputBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("HammerTestKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("HammerTestMouse");
            fixture = new GameObject("Hammer test room");
            fixture.transform.position = new Vector3(200f, 0f, 200f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(fixture.transform, false);
            floor.transform.localPosition = Vector3.down * 0.5f;
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/FirstPersonPlayer.prefab");
            player = Object.Instantiate(prefab, fixture.transform).GetComponent<FirstPersonController>();
            player.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            player.transform.localRotation = Quaternion.identity;
            equipment = player.GetComponent<PlayerEquipment>();
            interaction = player.GetComponent<PlayerInteraction>();
            hammer = player.GetComponentInChildren<FirstPersonHammer>(true);
            player.CaptureCursor();
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PickupRequiresSightReachAndActivePlayerAndIsNotValuableLoot()
        {
            var pickup = MakePickup();
            pickup.transform.localPosition = new Vector3(0f, 1.2f, 6f);
            Aim(pickup.transform.position);
            Physics.SyncTransforms();
            Assert.That(pickup.TryInteract(player.gameObject), Is.False);
            pickup.transform.localPosition = new Vector3(0f, 1.2f, 1.6f);
            Aim(pickup.transform.position);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(fixture.transform, false);
            wall.transform.localPosition = new Vector3(0f, 1.4f, 0.8f);
            wall.transform.localScale = new Vector3(2f, 3f, 0.1f);
            Physics.SyncTransforms();
            Assert.That(pickup.TryInteract(player.gameObject), Is.False);
            Object.DestroyImmediate(wall);
            player.ReleaseCursor();
            Assert.That(pickup.TryInteract(player.gameObject), Is.False);
            player.CaptureCursor();
            yield return null;
            Physics.SyncTransforms();
            Assert.That(interaction.CurrentTarget, Is.SameAs(pickup));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            yield return null;
            Assert.That(pickup.IsCollected, Is.True);
            Assert.That(equipment.HasHammer, Is.True);
            Assert.That(equipment.IsHammerEquipped, Is.True);
            Assert.That(pickup.TryInteract(player.gameObject), Is.False);
            Assert.That(equipment.TryCollectHammer(), Is.False);
            Assert.That(player.GetComponent<LootInventory>().ItemCount, Is.Zero);
            Assert.That(hammer.GetComponentsInChildren<Collider>().Length, Is.Zero);
        }

        [UnityTest]
        public IEnumerator EquipmentKeysRequireOwnershipAndPreservePistolAmmo()
        {
            Assert.That(equipment.TryEquipHammer(), Is.False);
            Assert.That(hammer.TrySwing(), Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit3));
            yield return null;
            Assert.That(equipment.IsHammerEquipped, Is.False);
            yield return Collect();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
            yield return null;
            yield return null;
            Assert.That(equipment.EmptyHands.activeInHierarchy, Is.True);
            Assert.That(hammer.TrySwing(), Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2));
            yield return null;
            yield return null;
            var pistol = player.GetComponentInChildren<FirstPersonWeapon>(true);
            Assert.That(equipment.IsPistolEquipped, Is.True);
            Assert.That(equipment.IsHammerEquipped, Is.False);
            Assert.That(pistol.TryFire(), Is.True);
            int ammo = pistol.CurrentAmmo;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit3));
            yield return null;
            yield return null;
            Assert.That(equipment.IsHammerEquipped, Is.True);
            Assert.That(equipment.EmptyHands.activeInHierarchy, Is.False);
            Assert.That(pistol.TryFire(), Is.False);
            Assert.That(equipment.TryEquipPistol(), Is.True);
            Assert.That(pistol.CurrentAmmo, Is.EqualTo(ammo));
        }

        [UnityTest]
        public IEnumerator ClickSwingsOncePerPressAndDoesNotSwingOnEquipWhileHeld()
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return null;
            yield return Collect();
            yield return new WaitForSeconds(0.2f);
            Assert.That(hammer.SwingsPerformed, Is.Zero);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return null;
            yield return null;
            Assert.That(hammer.SwingsPerformed, Is.EqualTo(1));
            Assert.That(hammer.TrySwing(), Is.False, "Cooldown must block repeated calls.");
            yield return new WaitForSeconds(0.8f);
            Assert.That(hammer.SwingsPerformed, Is.EqualTo(1), "Holding Attack must not repeat swings.");
        }

        [UnityTest]
        public IEnumerator StrikeRespectsReachWallsAndDamagesOnlyFirstSurface()
        {
            yield return Collect();
            var glass = MakeGlass(new Vector3(0f, 1.4f, 4f));
            Aim(glass.transform.position);
            Assert.That(hammer.TrySwing(), Is.True);
            yield return new WaitForSeconds(0.7f);
            Assert.That(glass.IsBroken, Is.False, "Distant glass is out of melee reach.");
            glass.transform.localPosition = new Vector3(0f, 1.4f, 1.5f);
            Aim(glass.transform.position);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(fixture.transform, false);
            wall.transform.localPosition = new Vector3(0f, 1.4f, 0.9f);
            wall.transform.localScale = new Vector3(2f, 2f, 0.1f);
            Physics.SyncTransforms();
            Assert.That(hammer.TrySwing(), Is.True);
            yield return new WaitForSeconds(0.7f);
            Assert.That(glass.IsBroken, Is.False, "A solid wall must block a strike.");
            Object.DestroyImmediate(wall);
            var behind = MakeGlass(new Vector3(0f, 1.4f, 1.8f));
            Physics.SyncTransforms();
            Assert.That(hammer.TrySwing(), Is.True);
            yield return new WaitForSeconds(0.3f);
            Assert.That(glass.IsBroken, Is.True);
            Assert.That(behind.IsBroken, Is.False, "One impact must not hit a second surface after the first breaks.");
        }

        [UnityTest]
        public IEnumerator HolsteringCursorReleaseAndDeathCancelPendingImpacts()
        {
            yield return Collect();
            var glass = MakeGlass(new Vector3(0f, 1.4f, 1.5f));
            Aim(glass.transform.position);
            Physics.SyncTransforms();
            Assert.That(hammer.TrySwing(), Is.True);
            Assert.That(equipment.TryEquipHands(), Is.True);
            Assert.That(equipment.TryEquipHammer(), Is.True);
            Assert.That(hammer.TrySwing(), Is.False, "Holstering must not reset cooldown.");
            yield return new WaitForSeconds(0.7f);
            Assert.That(glass.IsBroken, Is.False);
            Assert.That(hammer.TrySwing(), Is.True);
            player.ReleaseCursor();
            yield return new WaitForSeconds(0.7f);
            Assert.That(glass.IsBroken, Is.False);
            player.CaptureCursor();
            yield return null;
            Assert.That(hammer.TrySwing(), Is.True);
            player.GetComponent<Health>().TakeDamage(100f);
            yield return new WaitForSeconds(0.3f);
            Assert.That(glass.IsBroken, Is.False);
            Assert.That(hammer.TrySwing(), Is.False);
            Assert.That(equipment.TryEquipHammer(), Is.False);
        }

        [UnityTest]
        public IEnumerator GlassBreaksOnceAndPreservesLootVisibility()
        {
            var glass = MakeGlass(new Vector3(0f, 1.4f, 1.5f));
            var loot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            loot.transform.SetParent(glass.transform, false);
            loot.transform.localScale = Vector3.one * 0.2f;
            int breaks = 0;
            glass.OnBroken.AddListener(() => breaks++);
            glass.GetComponent<Health>().TakeDamage(100f);
            glass.GetComponent<Health>().TakeDamage(100f);
            yield return null;
            Assert.That(glass.IsBroken, Is.True);
            Assert.That(breaks, Is.EqualTo(1));
            Assert.That(glass.GetComponent<Collider>().enabled, Is.False);
            Assert.That(glass.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(loot.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(loot.activeInHierarchy, Is.True);
        }

        private IEnumerator Collect()
        {
            var pickup = MakePickup();
            Aim(pickup.transform.position);
            Physics.SyncTransforms();
            Assert.That(interaction.TryInteract(), Is.True);
            yield return null;
            Assert.That(equipment.IsHammerEquipped, Is.True);
        }

        private HammerPickup MakePickup()
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.SetParent(fixture.transform, false);
            model.transform.localPosition = new Vector3(0f, 1.2f, 1.6f);
            model.transform.localScale = Vector3.one * 0.2f;
            return model.AddComponent<HammerPickup>();
        }

        private BreakableGlass MakeGlass(Vector3 position)
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.SetParent(fixture.transform, false);
            model.transform.localPosition = position;
            model.transform.localScale = new Vector3(0.8f, 1.6f, 0.06f);
            model.AddComponent<Health>();
            return model.AddComponent<BreakableGlass>();
        }

        private void Aim(Vector3 point)
        {
            var direction = point - player.PlayerCamera.transform.position;
            player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 0f);
            float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            typeof(FirstPersonController).GetField("pitch", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, pitch);
            player.PlayerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        [TearDown]
        public void Cleanup()
        {
            if (fixture != null) Object.DestroyImmediate(fixture);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            Application.runInBackground = previousBackground;
            InputSystem.settings.backgroundBehavior = previousInputBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
#endif
