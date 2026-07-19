using System.Collections.Generic;
using MikeNspired.XRIStarterKit;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Pose = UnityEngine.Pose;

namespace TeamSentinels.CognitiveTracking
{
    /// <summary>
    /// Modern-soldier cognitive HUD, fixed to the player's view:
    ///   • Bottom-left  — live green mannequin mirroring the player's body
    ///     (head, hands, crouch, lean, turn) plus a vertical health bar.
    ///   • Bottom-centre — compass tape driven by head yaw, with a pitch marker.
    ///   • Bottom-right — held-weapon readout (silhouette, name, ammo), shown
    ///     only while a gun is actually grabbed; the mannequin also carries a
    ///     mini rifle in the matching hand.
    ///
    /// Everything is built at runtime on translucent panels. The XR rig is
    /// discovered read-only — no XRI kit asset is ever modified.
    /// </summary>
    [DefaultExecutionOrder(30000)] // run after XRI has updated tracked poses
    public class CognitiveMirrorHud : MonoBehaviour
    {
        [Header("Tracked sources (leave empty to auto-detect from the XR rig)")]
        [SerializeField] Transform headSource;
        [SerializeField] Transform leftHandSource;
        [SerializeField] Transform rightHandSource;

        [Header("Layout (metres, relative to the view centre)")]
        [SerializeField] float hudDistance = 0.55f;
        [SerializeField] Vector2 playerWidgetPos = new Vector2(-0.320f, -0.205f);
        [SerializeField] Vector2 compassPos = new Vector2(0f, -0.270f);
        [SerializeField] Vector2 weaponWidgetPos = new Vector2(0.320f, -0.205f);
        [Range(0.03f, 0.20f)]
        [SerializeField] float dummyScale = 0.062f;

        [Header("Appearance")]
        [SerializeField] Color bodyColor = new Color(0.15f, 0.85f, 0.30f);
        [SerializeField] Color panelColor = new Color(0.02f, 0.05f, 0.03f, 0.55f);
        [SerializeField] Color accentColor = new Color(0.40f, 0.95f, 0.50f, 0.85f);
        [SerializeField] Color textColor = new Color(0.72f, 1f, 0.80f, 1f);
        [Tooltip("Draw the HUD over world geometry so it is always visible.")]
        [SerializeField] bool renderOnTop = true;
        [SerializeField] bool showBaseDisc = true;

        [Header("Motion mapping")]
        [Tooltip("Max sideways lean/step shown before the dummy recentres (metres).")]
        [SerializeField] float maxLeanOffset = 0.35f;

        public static CognitiveMirrorHud Instance { get; private set; }

        // ── Read-only state for other systems (e.g. CognitiveMovementRecorder) ──
        public Transform HeadTransform => headSource;
        public Transform LeftHandTransform => leftHandSource;
        public Transform RightHandTransform => rightHandSource;
        public bool LeftWeaponHeld => _leftWeapon != null;
        public bool RightWeaponHeld => _rightWeapon != null;
        public float Health01 => _playerHealth != null ? _playerHealth.HealthFraction : 1f;
        public float RigFloorY => _origin != null ? _origin.transform.position.y : 0f;

        // Rig / gameplay bindings
        XROrigin _origin;
        PlayerHealth _playerHealth;
        XRBaseInteractor[] _leftInteractors, _rightInteractors;
        ProjectileWeapon _leftWeapon, _rightWeapon;

        // Visuals
        Transform _anchor;
        MirrorDummyAvatar _avatar;
        CompassWidget _compass;
        WeaponWidget _weaponWidget;
        HealthBarWidget _healthBar;
        float _compassW, _compassH;

        // Body-frame state
        Vector3 _framePos;
        Vector3 _framePosVel;
        float _frameYaw;
        float _frameYawVel;
        bool _frameInit;
        float _nextSearchTime;
        float _nextWeaponScan;

        bool HeadBound => headSource != null;
        bool FullyBound => headSource != null && leftHandSource != null && rightHandSource != null
                           && _playerHealth != null;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            TryBindRig();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Discovery (read-only, no XRI assets touched) ─────────────────────

        void TryBindRig()
        {
            if (_origin == null)
                _origin = FindFirstObjectByType<XROrigin>();

            if (headSource == null)
            {
                if (_origin != null && _origin.Camera != null) headSource = _origin.Camera.transform;
                else if (Camera.main != null) headSource = Camera.main.transform;
            }

            Transform searchRoot = _origin != null ? _origin.transform
                                 : headSource != null ? headSource.root : null;
            if (searchRoot != null)
            {
                if (leftHandSource == null) leftHandSource = FindHandTransform(searchRoot, true);
                if (rightHandSource == null) rightHandSource = FindHandTransform(searchRoot, false);
            }

            if (leftHandSource != null && _leftInteractors == null)
                _leftInteractors = leftHandSource.GetComponentsInChildren<XRBaseInteractor>(true);
            if (rightHandSource != null && _rightInteractors == null)
                _rightInteractors = rightHandSource.GetComponentsInChildren<XRBaseInteractor>(true);

            if (_playerHealth == null)
                _playerHealth = FindFirstObjectByType<PlayerHealth>();

            if (headSource != null && _avatar == null)
                CreateHudVisuals();
        }

        static readonly string[] ExcludedNameParts =
        {
            "teleport", "stabilized", "attach", "interactor", "ray", "poke",
            "direct", "ui", "model", "visual", "smoothing", "offset", "anchor"
        };

        /// <summary>Breadth-first search for the shallowest transform that looks like a hand/controller root.</summary>
        static Transform FindHandTransform(Transform root, bool left)
        {
            string side = left ? "left" : "right";
            Transform best = null;
            int bestDepth = int.MaxValue;

            var queue = new Queue<(Transform t, int depth)>();
            queue.Enqueue((root, 0));
            while (queue.Count > 0)
            {
                var (t, depth) = queue.Dequeue();
                if (depth > 6) continue; // controllers sit near the rig root

                string n = t.name.ToLowerInvariant();
                if (n.Contains(side) && (n.Contains("controller") || n.Contains("hand")))
                {
                    bool excluded = false;
                    for (int i = 0; i < ExcludedNameParts.Length; i++)
                    {
                        if (n.Contains(ExcludedNameParts[i])) { excluded = true; break; }
                    }
                    if (!excluded && depth < bestDepth)
                    {
                        best = t;
                        bestDepth = depth;
                    }
                }

                for (int i = 0; i < t.childCount; i++)
                    queue.Enqueue((t.GetChild(i), depth + 1));
            }
            return best;
        }

        // ── HUD construction ─────────────────────────────────────────────────

        void CreateHudVisuals()
        {
            _anchor = new GameObject("SoldierHudAnchor").transform;
            _anchor.SetParent(transform, false);

            var panelMat = HudBuilder.PanelMat(panelColor, 3990);
            var borderMat = HudBuilder.PanelMat(accentColor, 3992);
            var tickMat = HudBuilder.PanelMat(accentColor, 3993);

            // Bottom-left: mannequin + health bar
            var playerSlot = MakeSlot("PlayerWidget", playerWidgetPos);
            const float panelW = 0.105f, panelH = 0.150f;
            var panel = HudBuilder.Panel(playerSlot, "PlayerPanel", panelW, panelH, panelMat, borderMat);
            panel.localPosition = new Vector3(0f, 0f, 0.012f);

            _avatar = MirrorDummyAvatar.Create(playerSlot, bodyColor, renderOnTop, showBaseDisc);
            _avatar.transform.localScale = Vector3.one * dummyScale;
            _avatar.transform.localPosition = new Vector3(-0.010f, -0.9f * dummyScale, 0f);

            _healthBar = new HealthBarWidget(playerSlot, new Vector3(panelW * 0.5f - 0.012f, 0.004f, 0f),
                panelH - 0.034f, borderMat, textColor);

            // Bottom-centre: compass
            var compassSlot = MakeSlot("CompassWidget", compassPos);
            _compassW = 0.20f;
            _compassH = 0.042f;
            _compass = new CompassWidget(compassSlot, _compassW, _compassH, panelMat, borderMat, tickMat, textColor);

            // Bottom-right: held weapon
            var weaponSlot = MakeSlot("WeaponWidget", weaponWidgetPos);
            var gunMat = HudBuilder.SolidMat(new Color(0.55f, 0.75f, 0.58f), 4005);
            if (gunMat.HasProperty("_ZTest") && renderOnTop)
                gunMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            _weaponWidget = new WeaponWidget(weaponSlot, 0.115f, 0.062f, panelMat, borderMat, gunMat, textColor);
        }

        Transform MakeSlot(string name, Vector2 pos)
        {
            var slot = new GameObject(name).transform;
            slot.SetParent(_anchor, false);
            slot.localPosition = new Vector3(pos.x, pos.y, hudDistance);
            return slot;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Manual override for the health display (0–1). Normally driven by PlayerHealth automatically.</summary>
        public void SetHealth01(float health)
        {
            if (_avatar != null) _avatar.SetHealth01(health);
            _healthBar?.Update(health);
        }

        public void SetVisible(bool visible)
        {
            if (_anchor != null) _anchor.gameObject.SetActive(visible);
        }

        // ── Per-frame update ─────────────────────────────────────────────────

        void LateUpdate()
        {
            if (!FullyBound && Time.unscaledTime >= _nextSearchTime)
            {
                _nextSearchTime = Time.unscaledTime + 1f;
                TryBindRig();
            }
            if (!HeadBound || _avatar == null)
                return;

            // Fixed HUD: rigidly locked to the view, like a helmet display.
            _anchor.SetPositionAndRotation(headSource.position, headSource.rotation);

            UpdateAvatar();
            UpdateCompass();
            UpdateHealth();
            UpdateWeapons();
        }

        void UpdateCompass()
        {
            Vector3 f = headSource.forward;
            float heading = YawOf(f, _frameYaw);
            float pitch = Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
            _compass.Update(heading, pitch, _compassW, _compassH);
        }

        void UpdateHealth()
        {
            if (_playerHealth == null) return;
            float frac = _playerHealth.HealthFraction;
            _avatar.SetHealth01(frac);
            _healthBar.Update(frac);
        }

        void UpdateWeapons()
        {
            if (Time.unscaledTime >= _nextWeaponScan)
            {
                _nextWeaponScan = Time.unscaledTime + 0.25f;
                _leftWeapon = FindHeldWeapon(_leftInteractors);
                _rightWeapon = FindHeldWeapon(_rightInteractors);
            }

            _avatar.LeftWeaponVisible = _leftWeapon != null;
            _avatar.RightWeaponVisible = _rightWeapon != null;

            var weapon = _rightWeapon != null ? _rightWeapon : _leftWeapon;
            if (weapon == null)
            {
                _weaponWidget.Update(false, null, 0, 0, false);
                return;
            }

            var magazine = weapon.magazineAttach != null ? weapon.magazineAttach.Magazine : null;
            int current = magazine != null ? magazine.CurrentAmmo : -1;
            int max = magazine != null ? magazine.MaxAmmo : 0;
            _weaponWidget.Update(true, CleanName(weapon.name), current, max, weapon.infiniteAmmo);
        }

        static ProjectileWeapon FindHeldWeapon(XRBaseInteractor[] interactors)
        {
            if (interactors == null) return null;
            foreach (var interactor in interactors)
            {
                if (interactor == null || !interactor.hasSelection) continue;
                foreach (var selected in interactor.interactablesSelected)
                {
                    if (selected == null || selected.transform == null) continue;
                    var weapon = selected.transform.GetComponentInParent<ProjectileWeapon>();
                    if (weapon != null) return weapon;
                }
            }
            return null;
        }

        static string CleanName(string raw)
        {
            string s = raw.Replace("(Clone)", "").Replace("_", " ");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s*\(\d+\)\s*$", "");
            return s.Trim().ToUpperInvariant();
        }

        void UpdateAvatar()
        {
            float floorY = _origin != null ? _origin.transform.position.y : 0f;

            // Body reference frame: smoothed position on the floor under the head,
            // plus smoothed yaw. Quick motions (lean, crouch, turn, steps) show on
            // the dummy; sustained locomotion recentres so it always stays readable.
            Vector3 headWorld = headSource.position;
            Vector3 floorUnderHead = new Vector3(headWorld.x, floorY, headWorld.z);
            float targetYaw = YawOf(headSource.forward, _frameYaw);

            if (!_frameInit)
            {
                _framePos = floorUnderHead;
                _frameYaw = targetYaw;
                _frameInit = true;
            }
            _framePos = Vector3.SmoothDamp(_framePos, floorUnderHead, ref _framePosVel, 0.35f);
            _frameYaw = Mathf.SmoothDampAngle(_frameYaw, targetYaw, ref _frameYawVel, 0.45f);

            Quaternion invFrame = Quaternion.Inverse(Quaternion.Euler(0f, _frameYaw, 0f));

            Pose headLocal = ToFrame(invFrame, headSource);
            bool leftValid = leftHandSource != null;
            bool rightValid = rightHandSource != null;
            Pose leftLocal = leftValid ? ToFrame(invFrame, leftHandSource) : default;
            Pose rightLocal = rightValid ? ToFrame(invFrame, rightHandSource) : default;

            // Before HMD tracking kicks in (or in desktop testing) the camera sits
            // at the rig floor; show a neutral standing mannequin instead of a
            // fully crouched one. Real crouch/prone poses stay above this height.
            if (headLocal.position.y < 0.15f)
            {
                headLocal = new Pose(new Vector3(0f, 1.55f, 0f), headLocal.rotation);
                leftValid = false;
                rightValid = false;
            }

            // Clamp how far the whole body drifts from the HUD centre.
            Vector3 drift = new Vector3(headLocal.position.x, 0f, headLocal.position.z);
            float driftMag = drift.magnitude;
            if (driftMag > maxLeanOffset)
            {
                Vector3 shift = drift * (1f - maxLeanOffset / driftMag);
                headLocal.position -= shift;
                if (leftValid) leftLocal.position -= shift;
                if (rightValid) rightLocal.position -= shift;
            }

            _avatar.UpdatePose(headLocal, leftLocal, leftValid, rightLocal, rightValid);
        }

        Pose ToFrame(Quaternion invFrameRot, Transform t)
        {
            return new Pose(invFrameRot * (t.position - _framePos), invFrameRot * t.rotation);
        }

        static float YawOf(Vector3 forward, float fallbackDeg)
        {
            forward.y = 0f;
            return forward.sqrMagnitude < 1e-4f
                ? fallbackDeg
                : Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }
    }
}
