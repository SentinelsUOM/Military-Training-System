// Module 4 Integration | Sentinels | University of Moratuwa | 2026

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

/// <summary>
/// Makes being shot READABLE, and makes dying feel like dying.
///
/// Before this, PlayerHealth simply subtracted a number and wrote to the console: no sound, no
/// flash, no haptics, no indication of where the fire was coming from. In a headset the trainee
/// had no way to tell they were under fire — they just abruptly died on the 10th round. A trainee
/// who cannot perceive that they are being hit cannot learn to break contact and take cover,
/// which is the entire point of the simulator.
///
/// Three signals, deliberately chosen so none of them obstructs aiming:
///   1. RED VIGNETTE  — peripheral only, transparent through the centre. Deepens as health falls,
///                      and pulses on each impact. Tells you "I am hurt / I am being hit NOW".
///   2. HAPTICS       — both controllers buzz on impact. The one channel that still reaches the
///                      trainee when they are looking the wrong way.
///   3. DIRECTIONAL   — a red arc at the edge of vision pointing at the shooter. Turns "I'm being
///      INDICATOR       shot" into "I'm being shot from THERE", which is the actionable version.
///
/// Death is a fade to black — the camera is never moved. Forcibly translating or rotating a
/// player's head in VR is a well-known cause of motion sickness, so there is no collapse
/// animation; the world simply goes dark, input is released, and the result panel appears.
///
/// Everything is built procedurally at runtime (same approach as MissionResultUI), so there is no
/// scene wiring to forget. World-space canvases throughout: a screen-space canvas does not render
/// inside an HMD.
///
/// Setup: add to the same GameObject as PlayerHealth (PlayerHitbox). No other wiring needed.
/// </summary>
public class PlayerDamageFeedback : MonoBehaviour
{
    [Header("References (auto-found if left empty)")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Damage vignette")]
    [Tooltip("Colour of the peripheral damage vignette.")]
    [SerializeField] private Color damageColor = new Color(0.78f, 0.06f, 0.06f);

    [Tooltip("Health fraction at which the sustained vignette begins to appear. You die in 10 " +
             "rounds, so 0.9 means it starts showing from the SECOND hit — early enough to be a " +
             "warning, not so early that a healthy trainee stares at red for the whole mission.")]
    [Range(0f, 1f)][SerializeField] private float vignetteOnsetHealth = 0.9f;

    [Tooltip("Vignette opacity at 0 health (just before death).")]
    [Range(0f, 1f)][SerializeField] private float maxSustainedAlpha = 0.8f;

    [Tooltip("Extra opacity spike on each individual hit — this is the 'I was just hit' pulse.")]
    [Range(0f, 1f)][SerializeField] private float hitFlashAlpha = 0.9f;

    [Tooltip("How fast the per-hit pulse decays (alpha per second). 1.2 gives roughly a " +
             "three-quarter-second flash. The first version decayed in 0.28s, which is faster " +
             "than a blink — trainees took five rounds and never saw a thing.")]
    [SerializeField] private float hitFlashDecay = 1.2f;

    [Header("Directional damage indicator")]
    [Tooltip("Seconds an arc stays on screen after the hit that spawned it.")]
    [SerializeField] private float indicatorLifetime = 1.8f;

    [Tooltip("Most arcs shown at once. Beyond this the oldest is recycled, so a sustained burst " +
             "never turns the whole view into a red ring.")]
    [SerializeField] private int maxIndicators = 4;

    [Header("Haptics")]
    [Range(0f, 1f)][SerializeField] private float hapticAmplitude = 0.6f;
    [SerializeField] private float hapticDuration = 0.12f;

    [Header("Death")]
    [Tooltip("Seconds to fade to black once the trainee is killed.")]
    [SerializeField] private float fadeToBlackSeconds = 1.0f;

    [Tooltip("Seconds to hold on black before the result panel appears — a beat to let the death " +
             "land, rather than snapping straight to a scoreboard.")]
    [SerializeField] private float holdBlackSeconds = 0.5f;

    // ── runtime ────────────────────────────────────────────────────────────────
    private Camera    _cam;
    private Transform _camT;

    private Canvas _hudCanvas;      // vignette + directional arcs (0.6 m)
    private Canvas _fadeCanvas;     // death fade (0.4 m — closer, so it covers the HUD)

    private Image _vignette;
    private Image _fade;

    private Sprite _vignetteSprite;
    private Sprite _arcSprite;

    private readonly List<HapticImpulsePlayer> _haptics = new List<HapticImpulsePlayer>();
    private readonly List<Indicator> _indicators = new List<Indicator>();

    private float _flash;           // current per-hit pulse alpha
    private bool  _dying;

    private class Indicator
    {
        public Image image;
        public float bornAt;
        public Vector3 source;      // world position of the shooter — re-aimed every frame
    }

    // ── lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();

        if (playerHealth == null)
        {
            Debug.LogError("[PlayerDamageFeedback] No PlayerHealth found — damage feedback disabled.");
            enabled = false;
            return;
        }

        _cam = Camera.main;
        if (_cam == null)
        {
            Debug.LogError("[PlayerDamageFeedback] No Camera.main — damage feedback disabled.");
            enabled = false;
            return;
        }
        _camT = _cam.transform;

        _vignetteSprite = BuildVignetteSprite();
        _arcSprite      = BuildArcSprite();

        BuildHud();
        BuildFade();

        // The rig's haptic players — same component the archery/climbing features drive.
        _haptics.AddRange(FindObjectsByType<HapticImpulsePlayer>(FindObjectsSortMode.None));
    }

    private void OnEnable()
    {
        if (playerHealth == null) return;
        playerHealth.OnDamaged   += HandleDamaged;
        playerHealth.OnPlayerDied += HandleDied;
    }

    private void OnDisable()
    {
        if (playerHealth == null) return;
        playerHealth.OnDamaged   -= HandleDamaged;
        playerHealth.OnPlayerDied -= HandleDied;
    }

    // ── damage ─────────────────────────────────────────────────────────────────

    private void HandleDamaged(Vector3? source)
    {
        _flash = hitFlashAlpha;

        foreach (var h in _haptics)
            if (h != null) h.SendHapticImpulse(hapticAmplitude, hapticDuration);

        if (source.HasValue) SpawnIndicator(source.Value);
    }

    private void SpawnIndicator(Vector3 source)
    {
        Indicator ind;

        if (_indicators.Count >= Mathf.Max(1, maxIndicators))
        {
            // Recycle the oldest rather than stacking arcs forever.
            ind = _indicators[0];
            _indicators.RemoveAt(0);
        }
        else
        {
            var go = new GameObject("DamageArc", typeof(RectTransform));
            go.transform.SetParent(_hudCanvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.sprite        = _arcSprite;
            img.raycastTarget = false;
            ind = new Indicator { image = img };
        }

        ind.bornAt = Time.time;
        ind.source = source;
        ind.image.gameObject.SetActive(true);
        _indicators.Add(ind);
    }

    private void Update()
    {
        // Keep both canvases locked to the real frustum. Cheap, and it survives XR swapping in the
        // HMD's true FOV after startup.
        FitCanvasToFov(_hudCanvas.transform,  HudDistance,  HudOverfill);
        FitCanvasToFov(_fadeCanvas.transform, FadeDistance, FadeOverfill);

        if (_flash > 0f) _flash = Mathf.Max(0f, _flash - hitFlashDecay * Time.deltaTime);

        // Vignette: sustained component from how hurt you are, plus the per-hit pulse.
        // They're combined with Max (not added) so a hit at full health still reads clearly,
        // and a hit at low health doesn't blow out to a solid red screen you can't see through.
        float hurt      = Mathf.InverseLerp(vignetteOnsetHealth, 0f, playerHealth.HealthFraction);
        float sustained = hurt * maxSustainedAlpha;
        float alpha     = _dying ? 0f : Mathf.Max(sustained, _flash);

        var c = damageColor; c.a = alpha;
        _vignette.color = c;

        UpdateIndicators();
    }

    private void UpdateIndicators()
    {
        for (int i = _indicators.Count - 1; i >= 0; i--)
        {
            var ind = _indicators[i];
            float age = Time.time - ind.bornAt;

            if (_dying || age >= indicatorLifetime)
            {
                ind.image.gameObject.SetActive(false);
                _indicators.RemoveAt(i);
                continue;
            }

            // Re-aim every frame: the arc must track the shooter as the trainee turns their head,
            // otherwise it points at where the threat *was* and walks you into the line of fire.
            Vector3 toSource = ind.source - _camT.position;
            Vector3 flatTo   = Vector3.ProjectOnPlane(toSource,      Vector3.up);
            Vector3 flatFwd  = Vector3.ProjectOnPlane(_camT.forward, Vector3.up);
            if (flatTo.sqrMagnitude < 1e-4f || flatFwd.sqrMagnitude < 1e-4f) continue;

            // Signed angle from where you're looking to where the shot came from.
            // The arc art points "up" (12 o'clock = dead ahead), so rotating by -angle
            // swings it round to the correct bearing.
            float angle = Vector3.SignedAngle(flatFwd, flatTo, Vector3.up);
            ind.image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);

            var col = damageColor;
            col.a = Mathf.Clamp01(1f - age / indicatorLifetime);
            ind.image.color = col;
        }
    }

    // ── death ──────────────────────────────────────────────────────────────────

    private void HandleDied()
    {
        if (_dying) return;
        _dying = true;
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        ReleasePlayerControl();

        // 1) Fade to black. The camera is NOT moved — see the class summary.
        float t = 0f;
        while (t < fadeToBlackSeconds)
        {
            t += Time.deltaTime;
            var c = Color.black;
            c.a = Mathf.Clamp01(t / Mathf.Max(0.01f, fadeToBlackSeconds));
            _fade.color = c;
            yield return null;
        }
        _fade.color = Color.black;

        yield return new WaitForSeconds(holdBlackSeconds);

        // 2) Now make the blackout permanent by rendering NOTHING but UI. This is what lets the
        //    result panel sit against pure black: a fade quad alone can't do it, because it has to
        //    live at some fixed distance from the eye, and any scenery (or the trainee's own
        //    rifle) nearer than that would still poke through in front of it.
        _hudCanvas.gameObject.SetActive(false);
        _fadeCanvas.gameObject.SetActive(false);

        _cam.clearFlags      = CameraClearFlags.SolidColor;
        _cam.backgroundColor = Color.black;
        _cam.cullingMask     = LayerMask.GetMask(UILayerName);

        // Keep the XR pointer ray visible so the trainee can actually aim at the Restart button —
        // the line renderers live on the rig's own layer, which we have just culled away.
        PromoteXRPointersToUI();

        // MissionResultUI is already listening for the session-complete event and will build the
        // panel on the UI layer, so it draws against the black.
    }

    /// <summary>Stop the corpse walking around. Locomotion providers are disabled rather than the
    /// whole rig, so head tracking keeps working — freezing someone's head in VR is disorienting
    /// and can be nauseating, even when they're dead.</summary>
    private void ReleasePlayerControl()
    {
        var rig = _camT.root;
        foreach (var mb in rig.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null || mb == this) continue;
            string n = mb.GetType().Name;
            if (n.EndsWith("MoveProvider")   ||
                n.EndsWith("TurnProvider")   ||
                n.EndsWith("ClimbProvider")  ||
                n == "TeleportationProvider" ||
                n == "LocomotionMediator")
            {
                mb.enabled = false;
            }
        }
    }

    private void PromoteXRPointersToUI()
    {
        int ui = LayerMask.NameToLayer(UILayerName);
        if (ui < 0) return;

        var rig = _camT.root;
        foreach (var lr in rig.GetComponentsInChildren<LineRenderer>(true))
            if (lr != null) lr.gameObject.layer = ui;
    }

    // ── procedural UI construction ─────────────────────────────────────────────

    private const string UILayerName = "UI";
    private const float  HudDistance  = 0.60f;   // metres in front of the eye
    private const float  FadeDistance = 0.40f;   // closer than the HUD, so it covers it

    // The HUD must match the camera's ACTUAL field of view. The first version simply scaled the
    // quad to 6x its distance, which made it roughly three times wider than the visible frustum —
    // so the vignette's red rim and the directional arcs, which by design live in the OUTER part
    // of the texture, were rendered completely outside the trainee's view. The effect was that
    // damage feedback appeared to be missing entirely when in fact it was drawing perfectly,
    // just off-screen. Hug the frustum instead.
    private const float  HudOverfill  = 1.02f;   // vignette: hug the frustum edge exactly
    private const float  FadeOverfill = 2.0f;    // fade: solid black, so spill past the corners

    /// <summary>Scale a camera-parented world-space canvas so it exactly spans the frustum at its
    /// distance. Recomputed every frame because XR only fills in the real HMD field of view AFTER
    /// the first frames — reading it once in Awake gives you the editor's placeholder value.</summary>
    private void FitCanvasToFov(Transform canvasT, float distance, float overfill)
    {
        if (canvasT == null || _cam == null) return;

        float vFov = Mathf.Max(1f, _cam.fieldOfView);
        float h    = 2f * distance * Mathf.Tan(vFov * 0.5f * Mathf.Deg2Rad);
        float w    = h * Mathf.Max(0.1f, _cam.aspect);
        float size = Mathf.Max(w, h) * overfill;

        canvasT.localScale = Vector3.one * (size / 1000f);   // sizeDelta is 1000x1000
    }

    private void BuildHud()
    {
        _hudCanvas = BuildCanvas("PlayerDamageHUD", HudDistance);

        var go = new GameObject("Vignette", typeof(RectTransform));
        go.transform.SetParent(_hudCanvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        _vignette = go.AddComponent<Image>();
        _vignette.sprite        = _vignetteSprite;
        _vignette.raycastTarget = false;
        _vignette.color         = new Color(damageColor.r, damageColor.g, damageColor.b, 0f);
    }

    private void BuildFade()
    {
        _fadeCanvas = BuildCanvas("PlayerDeathFade", FadeDistance);

        var go = new GameObject("FadeToBlack", typeof(RectTransform));
        go.transform.SetParent(_fadeCanvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        _fade = go.AddComponent<Image>();
        _fade.raycastTarget = false;
        _fade.color = new Color(0f, 0f, 0f, 0f);
    }

    private Canvas BuildCanvas(string name, float distance)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_camT, false);
        go.transform.localPosition = new Vector3(0f, 0f, distance);
        go.transform.localRotation = Quaternion.identity;

        int ui = LayerMask.NameToLayer(UILayerName);
        if (ui >= 0) go.layer = ui;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.WorldSpace;
        canvas.worldCamera = _cam;

        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1000f, 1000f);

        // Actual scale is set every frame by FitCanvasToFov — see the note there.
        return canvas;
    }

    // ── procedural sprites (no art assets required) ─────────────────────────────

    private static Sprite BuildVignetteSprite()
    {
        const int N = 256;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px  = new Color32[N * N];

        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            // 0 at centre, 1 at the edge midpoints.
            float dx = (x / (float)(N - 1) - 0.5f) * 2f;
            float dy = (y / (float)(N - 1) - 0.5f) * 2f;
            float d  = Mathf.Sqrt(dx * dx + dy * dy);

            // Clear through the middle so it never obscures the sights, but the red has to start
            // well before the very rim or it sits in the corners of the eye where nobody sees it.
            float a = Smooth(0.42f, 1.0f, d);
            px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
        }

        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
    }

    private static Sprite BuildArcSprite()
    {
        const int N = 256;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px  = new Color32[N * N];

        const float innerR = 0.52f, outerR = 0.82f;   // out toward the edge, but still ON screen
        const float halfArcDeg = 30f;                 // a wedge, not a full ring

        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float dx = (x / (float)(N - 1) - 0.5f) * 2f;
            float dy = (y / (float)(N - 1) - 0.5f) * 2f;
            float d  = Mathf.Sqrt(dx * dx + dy * dy);

            // Bearing measured from straight up (12 o'clock = the direction you're facing).
            float ang = Mathf.Abs(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg);

            float radial  = Smooth(innerR, innerR + 0.06f, d) * (1f - Smooth(outerR - 0.06f, outerR, d));
            float angular = 1f - Smooth(halfArcDeg * 0.55f, halfArcDeg, ang);

            float a = Mathf.Clamp01(radial * angular);
            px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }

        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
    }

    private static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / Mathf.Max(1e-5f, b - a));
        return t * t * (3f - 2f * t);
    }
}
