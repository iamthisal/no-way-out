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
        [SerializeField] private LineRenderer tracer;

        [Header("Pistol")]
        [SerializeField, Min(0.1f)] private float damage = 25f;
        [SerializeField, Min(1f)] private float range = 60f;
        [SerializeField, Min(0.05f)] private float shotInterval = 0.25f;
        [SerializeField, Min(1)] private int magazineSize = 12;
        [SerializeField, Min(0.1f)] private float reloadDuration = 1.2f;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;

        private FirstPersonController player;
        private InputActionAsset ownedActions;
        private InputAction attackAction;
        private InputAction reloadAction;
        private float nextShotTime;
        private float reloadEndsAt;
        private float flashEndsAt;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private float recoil;
        private GUIStyle ammoStyle;
        private bool waitForAttackRelease;

        public int CurrentAmmo { get; private set; }
        public int MagazineSize => magazineSize;
        public bool IsReloading { get; private set; }
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
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            CurrentAmmo = magazineSize;
        }

        private void OnEnable()
        {
            attackAction?.Enable();
            reloadAction?.Enable();
            waitForAttackRelease = true;
        }

        private void OnDisable()
        {
            attackAction?.Disable();
            reloadAction?.Disable();
            IsReloading = false;
            HideFlash();
        }

        private void OnDestroy()
        {
            reloadAction?.Dispose();
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
            transform.localPosition = restPosition + Vector3.back * (recoil * 0.035f);
            transform.localRotation = restRotation * Quaternion.Euler(-recoil * 5f, 0f, 0f);
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
            Vector3 end = ray.GetPoint(range);
            if (Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable is Health health) health.TakeDamage(damage, player.transform);
                else damageable?.TakeDamage(damage);
                if (impactPrefab != null)
                {
                    var impact = Instantiate(impactPrefab, hit.point + hit.normal * 0.015f, Quaternion.LookRotation(hit.normal));
                    Destroy(impact, 1.5f);
                }
                OnHit?.Invoke(hit);
            }
            if (tracer != null)
            {
                tracer.SetPosition(0, muzzle.position);
                tracer.SetPosition(1, end);
                tracer.enabled = true;
            }
            return true;
        }

        public bool TryReload()
        {
            if (!isActiveAndEnabled || player == null || !player.CanUseWeapon || IsReloading || CurrentAmmo == magazineSize)
                return false;
            IsReloading = true;
            reloadEndsAt = Time.time + reloadDuration;
            return true;
        }

        private void HideFlash()
        {
            if (muzzleFlash != null) muzzleFlash.SetActive(false);
            if (tracer != null) tracer.enabled = false;
        }

        private void OnGUI()
        {
            if (player == null || !player.IsCursorCaptured) return;
            float x = Screen.width * 0.5f;
            float y = Screen.height * 0.5f;
            Color previousColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x - 7f, y - 1f, 14f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 1f, y - 7f, 2f, 14f), Texture2D.whiteTexture);
            if (ammoStyle == null)
                ammoStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleRight, normal = { textColor = Color.white } };
            GUI.Label(new Rect(Screen.width - 190f, Screen.height - 64f, 160f, 34f), IsReloading ? "Reloading..." : CurrentAmmo + " / " + magazineSize, ammoStyle);
            GUI.color = previousColor;
        }
    }
}
