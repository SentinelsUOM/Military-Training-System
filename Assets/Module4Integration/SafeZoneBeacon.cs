// Module 4 Integration | Sentinels | University of Moratuwa | 2026
//
// Lives in Assembly-CSharp (no asmdef in this folder).

using UnityEngine;

/// <summary>
/// Draws the extraction point as a clearly-visible CIRCULAR ZONE on the ground —
/// a translucent disc with a bright pulsing ring outline and a floating label —
/// so the trainee can see exactly where to bring a rescued hostage. Reads as a
/// marked zone on the floor (military LZ style) rather than the old light
/// beam / bar.
///
/// All visuals are built in Start() from the public fields, so a spawner can set
/// radius/colour right after AddComponent. Attach alongside an ExtractionZone +
/// trigger collider; SceneBuilder adds this automatically at the trainee's
/// actual mission start position.
/// </summary>
public class SafeZoneBeacon : MonoBehaviour
{
    [Tooltip("Radius of the zone circle (metres). Match the ExtractionZone trigger radius.")]
    public float radius = 2.5f;

    [Tooltip("Zone colour.")]
    public Color color = new Color(0.2f, 1f, 0.4f, 1f);

    [Tooltip("Floating label text shown above the zone centre.")]
    public string label = "SAFE ZONE";

    [Tooltip("Pulse speed of the ring outline.")]
    public float pulseSpeed = 2f;

    [Tooltip("Width of the ring outline (metres).")]
    public float ringWidth = 0.12f;

    const int RingSegments = 64;

    private Transform _labelTf;
    private LineRenderer _ring;
    private Material _ringMat;

    private void Start()
    {
        BuildDisc();
        BuildRing();
        BuildLabel();
    }

    // ── Visual construction ─────────────────────────────────────────────────

    private void BuildDisc()
    {
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "ZoneDisc";
        disc.transform.SetParent(transform, false);
        disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        disc.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
        StripCollider(disc);
        // Faint translucent fill so the area reads as a zone without hiding the floor.
        Paint(disc, new Color(color.r, color.g, color.b, 0.16f), transparent: true);
    }

    private void BuildRing()
    {
        var ringGo = new GameObject("ZoneRing");
        ringGo.transform.SetParent(transform, false);
        ringGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);

        _ring = ringGo.AddComponent<LineRenderer>();
        _ring.useWorldSpace = false;
        _ring.loop = true;
        _ring.positionCount = RingSegments;
        _ring.startWidth = ringWidth;
        _ring.endWidth = ringWidth;
        _ring.alignment = LineAlignment.TransformZ; // flat on the ground
        ringGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        for (int i = 0; i < RingSegments; i++)
        {
            float a = i / (float)RingSegments * Mathf.PI * 2f;
            _ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        _ringMat = new Material(sh);
        SetMatColor(_ringMat, color);
        _ring.material = _ringMat;
        _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _ring.receiveShadows = false;
    }

    private void BuildLabel()
    {
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);

        var tm = labelGo.AddComponent<TextMesh>();
        tm.text          = label;
        tm.characterSize = 0.14f;
        tm.fontSize      = 64;
        tm.anchor        = TextAnchor.MiddleCenter;
        tm.alignment     = TextAlignment.Center;
        tm.color         = new Color(color.r, color.g, color.b, 1f);

        _labelTf = labelGo.transform;
    }

    // ── Per-frame animation ─────────────────────────────────────────────────

    private void Update()
    {
        // Billboard the label toward the active camera so it's always readable.
        if (_labelTf != null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCam = _labelTf.position - cam.transform.position;
                if (toCam.sqrMagnitude > 0.0001f)
                    _labelTf.rotation = Quaternion.LookRotation(toCam);
            }
        }

        // Pulse the ring brightness so the zone draws the eye without a beam.
        if (_ringMat != null)
        {
            float k = 0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed));
            SetMatColor(_ringMat, new Color(color.r * k, color.g * k, color.b * k, 1f));
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static void SetMatColor(Material mat, Color c)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        mat.color = c;
    }

    private static void StripCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
        }
    }

    /// <summary>
    /// Paints a primitive with a bright UNLIT material so it's visible in the
    /// Game view / VR regardless of scene lighting. URP-first with fallbacks.
    /// </summary>
    private static void Paint(GameObject go, Color c, bool transparent = false)
    {
        var rend = go.GetComponent<Renderer>();
        if (rend == null) return;

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        if (sh == null) sh = Shader.Find("Sprites/Default");

        var mat = new Material(sh);
        SetMatColor(mat, c);

        if (transparent && mat.HasProperty("_Surface"))
        {
            // URP Unlit transparent surface setup.
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        rend.material = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
    }
}
