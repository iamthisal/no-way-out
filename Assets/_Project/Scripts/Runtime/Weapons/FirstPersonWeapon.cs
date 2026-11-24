using System;
using Lockdown.Interfaces;
using Lockdown.Combat;
using Lockdown.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lockdown.Weapons
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonWeapon : MonoBehaviour
    {
        [Header("Assets")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Transform muzzle;
        [SerializeField] private GameObject muzzleFlash;
        [SerializeField] private GameObject impactPrefab;
        [SerializeField] private AudioSource shotAudio;

        [Header("Pistol")]
        [SerializeField, Min(0.1f)] private float damage = 25f;
        [SerializeField, Min(1f)] private float range = 60f;
        [SerializeField, Min(0.05f)] private float shotInterval = 0.25f;
        [SerializeField, Min(1)] private int magazineSize = 12;
        [SerializeField, Min(0.1f)] private float reloadDuration = 1.2f;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;

        [Header("Aiming")]
        [Tooltip("Front sight post; its top is placed on the crosshair while aiming.")]
        [SerializeField] private Transform frontSight;
        [Tooltip("Distance from the camera to the front sight while aiming, in metres.")]
        [SerializeField, Min(0.1f)] private float sightDistance = 0.5f;
        [Tooltip("Seconds to raise the sights to the eye or lower them again.")]
        [SerializeField, Min(0.01f)] private float aimTime = 0.12f;

        private FirstPersonController player;
        private InputActionAsset ownedActions;
        private InputAction attackAction;
        private InputAction reloadAction;
        private InputAction aimAction;
        private float nextShotTime;
        private float reloadEndsAt;
        private float flashEndsAt;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector3 aimPosition;
        private Quaternion aimRotation;
        private bool canAim;
        private float aimBlend;
        private float recoil;
        private bool waitForAttackRelease;

        public int CurrentAmmo { get; private set; }
        public int MagazineSize => magazineSize;
        public bool IsReloading { get; private set; }
        public bool IsAiming { get; private set; }
        /// <summary>0 at the hip, 1 with the sights fully on the crosshair.</summary>
        public float AimBlend => aimBlend;
        public Transform FrontSight => frontSight;
        public int ShotsFired { get; private set; }
        public event Action<RaycastHit> OnHit;

        private void Awake()
        {
            player = GetComponentInParent<FirstPersonController>();
            if (player == null || inputActions == null || muzzle == null)
            {
                Debug.LogError("FirstPersonWeapon requires a player, input actions, and muzzle.", this);
                enabled = false;
                return;
            }
            ownedActions = Instantiate(inputActions);
            attackAction = ownedActions.FindAction("Player/Attack", true);
            reloadAction = new InputAction("Reload", InputActionType.Button, "<Keyboard>/r");
            reloadAction.AddBinding("<Gamepad>/buttonNorth");
            aimAction = new InputAction("Aim", InputActionType.Button, "<Mouse>/rightButton");
            aimAction.AddBinding("<Gamepad>/leftTrigger");
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            if (frontSight == null) frontSight = transform.Find("FrontSight");
            canAim = ComputeAimPose();
            CurrentAmmo = magazineSize;
        }

        private void OnEnable()
        {
            attackAction?.Enable();
            reloadAction?.Enable();
            aimAction?.Enable();
            waitForAttackRelease = true;
        }

        private void OnDisable()
        {
            attackAction?.Disable();
            reloadAction?.Disable();
            aimAction?.Disable();
            IsReloading = false;
            IsAiming = false;
            aimBlend = 0f;
            recoil = 0f;
            if (player != null)
            {
                transform.localPosition = restPosition;
                transform.localRotation = restRotation;
            }
            HideFlash();
        }

        private void OnDestroy()
        {
            reloadAction?.Dispose();
            aimAction?.Dispose();
            if (ownedActions != null) Destroy(ownedActions);
        }

        private void Update()
        {
            if (player == null) return;
            if (IsReloading && Time.time >= reloadEndsAt)
            {
                CurrentAmmo = magazineSize;
                IsReloading = false;
            }
            if (Time.time >= flashEndsAt) HideFlash();
            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 8f);
            IsAiming = canAim && player.CanUseWeapon && !IsReloading && aimAction.IsPressed();
            aimBlend = Mathf.MoveTowards(aimBlend, IsAiming ? 1f : 0f, Time.deltaTime / aimTime);
            float aim = Mathf.SmoothStep(0f, 1f, aimBlend);
            Vector3 position = Vector3.Lerp(restPosition, aimPosition, aim);
            Quaternion rotation = Quaternion.Slerp(restRotation, aimRotation, aim);
            transform.localPosition = position + rotation * Vector3.back * (recoil * 0.035f);
            transform.localRotation = rotation * Quaternion.Euler(-recoil * 5f, 0f, 0f);
            if (!player.CanUseWeapon) return;
            if (!attackAction.IsPressed()) waitForAttackRelease = false;
            if (reloadAction.WasPressedThisFrame()) TryReload();
            if (!waitForAttackRelease && attackAction.WasPressedThisFrame()) TryFire();
        }

        public bool TryFire()
        {
            if (!isActiveAndEnabled || player == null || !player.CanUseWeapon || IsReloading || CurrentAmmo <= 0 || Time.time < nextShotTime)
                return false;
            CurrentAmmo--;
            ShotsFired++;
            nextShotTime = Time.time + shotInterval;
            recoil = 1f;
            flashEndsAt = Time.time + 0.06f;
            if (muzzleFlash != null) muzzleFlash.SetActive(true);
            if (shotAudio != null && shotAudio.clip != null) shotAudio.PlayOneShot(shotAudio.clip);

            var camera = player.PlayerCamera;
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            if (TryFindTarget(ray, out RaycastHit hit))
            {
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable is Health health) health.TakeDamage(damage, player.transform);
                else damageable?.TakeDamage(damage);
                if (impactPrefab != null)
                {
                    // Sits just off the surface to avoid z-fighting; the hole removes itself.
                    var impact = Instantiate(impactPrefab, hit.point + hit.normal * 0.002f, Quaternion.LookRotation(hit.normal));
                    var hole = impact.GetComponent<BulletHole>();
                    if (hole != null) hole.Attach(hit.collider.transform);
                    else Destroy(impact, 1.5f);
                }
                OnHit?.Invoke(hit);
            }
            return true;
        }

        // The first collider along the ray that is not part of the player. The player's own
        // capsule must be skipped: CharacterController.Move updates the transform at once, but
        // the physics scene keeps the capsule at its old position until the next sync, so after
        // stepping backwards the camera can sit behind that stale capsule and shoot into it.
        private bool TryFindTarget(Ray ray, out RaycastHit target)
        {
            var hits = Physics.RaycastAll(ray, range, hitMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(player.transform)) continue;
                target = hit;
                return true;
            }
            target = default;
            return false;
        }

        public bool TryReload()
        {
            if (!isActiveAndEnabled || player == null || !player.CanUseWeapon || IsReloading || CurrentAmmo == magazineSize)
                return false;
            IsReloading = true;
            reloadEndsAt = Time.time + reloadDuration;
            return true;
        }

        // The aim pose keeps the pistol parallel to the camera and puts the top of the front
        // sight on the camera's forward axis, which is where the crosshair is and shots go.
        private bool ComputeAimPose()
        {
            var parent = transform.parent;
            if (frontSight == null || parent == null || player.PlayerCamera == null) return false;
            var camera = player.PlayerCamera.transform;
            Vector3 sightTop = transform.InverseTransformPoint(frontSight.TransformPoint(0f, 0.5f, 0f));
            sightTop = Vector3.Scale(transform.localScale, sightTop);
            Vector3 weaponInCamera = new Vector3(0f, 0f, sightDistance) - sightTop;
            aimRotation = Quaternion.Inverse(parent.rotation) * camera.rotation;
            aimPosition = parent.InverseTransformPoint(camera.TransformPoint(weaponInCamera));
            return true;
        }

        private void HideFlash()
        {
            if (muzzleFlash != null) muzzleFlash.SetActive(false);
        }

        private void OnGUI()
        {
            if (player == null || !player.IsCursorCaptured) return;
            float x = Screen.width * 0.5f;
            float y = Screen.height * 0.5f;
            Color previousColor = GUI.color;
            // Fade the crosshair while aiming so the front sight stays visible beneath it.
            GUI.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.45f, aimBlend));
            GUI.DrawTexture(new Rect(x - 7f, y - 1f, 14f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 1f, y - 7f, 2f, 14f), Texture2D.whiteTexture);
            // Rounds are shown on the weapon plaque (Lockdown.Player.WeaponHud).
            GUI.color = previousColor;
        }
    }
}
