// Module 4 Integration | Sentinels | University of Moratuwa | 2026
//
// Lives in Assembly-CSharp (no asmdef in this folder).

using UnityEngine;

/// <summary>
/// Builds and animates a clearly-visible "safe spot" beacon for the extraction
/// point, so the trainee can SEE where to bring a rescued hostage — in the Game
/// view / VR, not just as an editor gizmo.
///
/// Visuals (all built in Start from the public fields, so a spawner can set
/// radius/colour right after AddComponent):
///   • a bright UNLIT floor pad (visible regardless of room lighting)
///   • a tall translucent light beam, visible from across the level
///   • a billboarded "HOSTAGE SAFE ZONE" label that always faces the player
///
/// Attach alongside an ExtractionZone + trigger collider. SceneBuilder adds this
/// automatically to the safe zone it spawns at the trainee start position.
/// </summary>
public class SafeZoneBeacon : MonoBehaviour
{
    [Tooltip("Radius of the floor pad (metres). Match the ExtractionZone trigger radius.")]
    public float radius = 2.5f;

    [Tooltip("Beacon colour.")]
    public Color color = new Color(0.2f, 1f, 0.4f, 1f);

    [Tooltip("Height of the vertical light beam (metres).")]
    public float beamHeight = 6f;

    [Tooltip("Floating label text shown above the beacon.")]
    public string label = "HOSTAGE SAFE ZONE";

    [Tooltip("Gentle pulse speed of the floor pad.")]
    public float pulseSpeed = 2f;

    private Transform _labelTf;
    private Transform _padTf;
    private float _basePadScale;

    private void Start()
    {
        BuildPad();
        BuildBeam();
        BuildLabel();
    }

    // ── Visual construction ─────────────────────────────────────────────────

    private void BuildPad()
    {
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = "Pad";
        pad.transform.SetParent(transform, false);
        pad.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        pad.transform.localScale    = new Vector3(radius * 2f, 0.02f, radius * 2f);
        StripCollider(pad);
        Paint(pad, color);
        _padTf = pad.transform;
        _basePadScale = radius * 2f;
    }

    private void BuildBeam()
    {
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "Beam";
        beam.transform.SetParent(transform, false);
        beam.transform.localPosition = new Vector3(0f, beamHeight * 0.5f, 0f);
        beam.transform.localScale    = new Vector3(0.25f, beamHeight * 0.5f, 0.25f);
        StripCollider(beam);
        // Slightly translucent beam so it reads as a "light column".
        Paint(beam, new Color(color.r, color.g, color.b, 0.35f), transparent: true);
    }

    private void BuildLabel()
    {
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(transform, false);
        labelGo.transform.localPosition = new Vector3(0f, beamHeight + 0.4f, 0f);

        var tm = labelGo.AddComponent<TextMesh>();
        tm.text          = label;
        tm.characterSize = 0.25f;
        tm.fontSize      = 64;
        tm.anchor        = TextAnchor.MiddleCenter;
        tm.alignment     = TextAlignment.Center;
        tm.color         = Color.white;

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

        // Gentle breathing pulse on the pad so it draws the eye.
        if (_padTf != null)
        {
            float k = 1f + 0.08f * Mathf.Sin(Time.time * pulseSpeed);
            _padTf.localScale = new Vector3(_basePadScale * k, 0.02f, _basePadScale * k);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

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
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        mat.color = c;

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
