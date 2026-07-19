using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace TeamSentinels.CognitiveTracking
{
    /// <summary>
    /// Runtime builders and widgets for the modern-soldier HUD: translucent
    /// backing panels, the bottom compass tape, the held-weapon readout and the
    /// vertical health bar. Everything is procedural — no asset dependencies.
    /// </summary>
    internal static class HudBuilder
    {
        public static Material PanelMat(Color color, int queue)
        {
            Shader sh = Resources.Load<Shader>("HudPanel");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh) { color = color, renderQueue = queue };
            return m;
        }

        public static Material SolidMat(Color color, int queue)
        {
            Shader sh = Resources.Load<Shader>("HudDummy");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            var m = new Material(sh) { color = color, renderQueue = queue };
            return m;
        }

        public static Transform Flat(Transform parent, string name, Material mat, PrimitiveType type = PrimitiveType.Quad)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = parent.gameObject.layer;
            Object.Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var t = go.transform;
            t.SetParent(parent, false);
            return t;
        }

        /// <summary>Translucent backing panel with a thin accent border, military style.</summary>
        public static Transform Panel(Transform parent, string name, float width, float height, Material bgMat, Material borderMat)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);

            var bg = Flat(root, "BG", bgMat);
            bg.localScale = new Vector3(width, height, 1f);

            const float t = 0.0016f; // border thickness
            float z = -0.0004f;      // border floats just in front of the bg
            var top = Flat(root, "BorderTop", borderMat);
            top.localPosition = new Vector3(0f, height * 0.5f, z);
            top.localScale = new Vector3(width + t, t, 1f);
            var bottom = Flat(root, "BorderBottom", borderMat);
            bottom.localPosition = new Vector3(0f, -height * 0.5f, z);
            bottom.localScale = new Vector3(width + t, t, 1f);
            var left = Flat(root, "BorderLeft", borderMat);
            left.localPosition = new Vector3(-width * 0.5f, 0f, z);
            left.localScale = new Vector3(t, height + t, 1f);
            var right = Flat(root, "BorderRight", borderMat);
            right.localPosition = new Vector3(width * 0.5f, 0f, z);
            right.localScale = new Vector3(t, height + t, 1f);
            return root;
        }

        /// <summary>World-space TMP text that renders on top of everything.</summary>
        public static TextMeshPro Text(Transform parent, string name, float fontSize, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(0.4f, 0.05f);
            if (tmp.font != null)
            {
                // Use TMP's Overlay shader variant (ZTest Always) so the text is
                // never hidden behind world geometry. Assigning the shader on the
                // instanced font material keeps the font atlas and SDF settings.
                var overlay = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
                var m = tmp.fontMaterial;
                if (overlay != null) m.shader = overlay;
                m.renderQueue = 4010;
            }
            return tmp;
        }

        /// <summary>
        /// Procedural rifle silhouette, ~0.55 m long, barrel along +Z, built from
        /// cubes. Scaled by the caller for the mannequin hand or the weapon panel.
        /// </summary>
        public static Transform MiniGun(Transform parent, Material mat)
        {
            var root = new GameObject("MiniGun").transform;
            root.SetParent(parent, false);

            var receiver = Flat(root, "Receiver", mat, PrimitiveType.Cube);
            receiver.localPosition = new Vector3(0f, 0.012f, 0.06f);
            receiver.localScale = new Vector3(0.024f, 0.042f, 0.20f);

            var barrel = Flat(root, "Barrel", mat, PrimitiveType.Cube);
            barrel.localPosition = new Vector3(0f, 0.024f, 0.21f);
            barrel.localScale = new Vector3(0.014f, 0.016f, 0.13f);

            var stock = Flat(root, "Stock", mat, PrimitiveType.Cube);
            stock.localPosition = new Vector3(0f, 0.002f, -0.075f);
            stock.localScale = new Vector3(0.020f, 0.034f, 0.09f);

            var grip = Flat(root, "Grip", mat, PrimitiveType.Cube);
            grip.localPosition = new Vector3(0f, -0.028f, 0.012f);
            grip.localRotation = Quaternion.Euler(20f, 0f, 0f);
            grip.localScale = new Vector3(0.018f, 0.06f, 0.026f);

            var mag = Flat(root, "Mag", mat, PrimitiveType.Cube);
            mag.localPosition = new Vector3(0f, -0.034f, 0.085f);
            mag.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            mag.localScale = new Vector3(0.018f, 0.068f, 0.034f);

            var sight = Flat(root, "Sight", mat, PrimitiveType.Cube);
            sight.localPosition = new Vector3(0f, 0.042f, 0.05f);
            sight.localScale = new Vector3(0.008f, 0.018f, 0.05f);
            return root;
        }
    }

    /// <summary>Bottom-centre compass tape: 15° ticks, cardinal labels, degree readout, pitch marker.</summary>
    internal class CompassWidget
    {
        const float WindowDeg = 55f;       // degrees visible either side of heading

        readonly Transform _root;
        readonly Transform[] _ticks = new Transform[24];
        readonly TextMeshPro[] _labels = new TextMeshPro[8];
        static readonly string[] LabelNames = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        readonly TextMeshPro _readout;
        readonly Transform _pitchMarker;
        readonly float _halfWidth;
        readonly float _pitchHalf;
        int _lastReadout = -1;

        public CompassWidget(Transform slot, float width, float height, Material bgMat, Material borderMat, Material tickMat, Color textColor)
        {
            _root = HudBuilder.Panel(slot, "Compass", width, height, bgMat, borderMat);
            _halfWidth = width * 0.5f - 0.016f;

            for (int i = 0; i < _ticks.Length; i++)
            {
                bool major = i % 3 == 0; // every 45°
                var tick = HudBuilder.Flat(_root, "Tick" + (i * 15), tickMat);
                tick.localScale = new Vector3(0.0012f, major ? 0.010f : 0.0055f, 1f);
                _ticks[i] = tick;
            }
            for (int i = 0; i < _labels.Length; i++)
            {
                _labels[i] = HudBuilder.Text(_root, "Lbl" + LabelNames[i], 0.10f, textColor);
                _labels[i].fontStyle = FontStyles.Bold;
                _labels[i].text = LabelNames[i];
            }

            _readout = HudBuilder.Text(_root, "Heading", 0.13f, textColor);
            _readout.rectTransform.localPosition = new Vector3(0f, height * 0.5f + 0.008f, 0f);

            // centre caret under the readout
            var caret = HudBuilder.Flat(_root, "Caret", tickMat);
            caret.localPosition = new Vector3(0f, height * 0.5f - 0.004f, -0.0004f);
            caret.localScale = new Vector3(0.0014f, 0.008f, 1f);

            // pitch tape on the right edge
            var pitchRail = HudBuilder.Flat(_root, "PitchRail", tickMat);
            _pitchHalf = height * 0.5f - 0.007f;
            pitchRail.localPosition = new Vector3(width * 0.5f - 0.008f, 0f, -0.0004f);
            pitchRail.localScale = new Vector3(0.0012f, _pitchHalf * 2f, 1f);
            _pitchMarker = HudBuilder.Flat(_root, "PitchMarker", tickMat);
            _pitchMarker.localScale = new Vector3(0.006f, 0.0022f, 1f);
        }

        public void Update(float headingDeg, float pitchDeg, float panelWidth, float panelHeight)
        {
            headingDeg = Mathf.Repeat(headingDeg, 360f);

            for (int i = 0; i < _ticks.Length; i++)
            {
                float delta = Mathf.DeltaAngle(headingDeg, i * 15f);
                bool visible = Mathf.Abs(delta) <= WindowDeg;
                _ticks[i].gameObject.SetActive(visible);
                if (visible)
                {
                    bool major = i % 3 == 0;
                    _ticks[i].localPosition = new Vector3(
                        delta / WindowDeg * _halfWidth, major ? -0.0095f : -0.012f, -0.0004f);
                }
            }
            for (int i = 0; i < _labels.Length; i++)
            {
                float delta = Mathf.DeltaAngle(headingDeg, i * 45f);
                bool visible = Mathf.Abs(delta) <= WindowDeg * 0.92f;
                _labels[i].gameObject.SetActive(visible);
                if (visible)
                    _labels[i].rectTransform.localPosition = new Vector3(
                        delta / WindowDeg * _halfWidth, 0.0045f, -0.0004f);
            }

            int shown = Mathf.RoundToInt(headingDeg) % 360;
            if (shown != _lastReadout)
            {
                _lastReadout = shown;
                _readout.text = shown.ToString("000") + "°";
            }

            float p = Mathf.Clamp(pitchDeg / 60f, -1f, 1f);
            _pitchMarker.localPosition = new Vector3(panelWidth * 0.5f - 0.008f, p * _pitchHalf, -0.0005f);
        }
    }

    /// <summary>Bottom-right held-weapon readout: silhouette, name and ammo.</summary>
    internal class WeaponWidget
    {
        readonly Transform _root;
        readonly TextMeshPro _nameText;
        readonly TextMeshPro _ammoText;
        string _lastName, _lastAmmo;

        public WeaponWidget(Transform slot, float width, float height, Material bgMat, Material borderMat, Material gunMat, Color textColor)
        {
            _root = HudBuilder.Panel(slot, "WeaponPanel", width, height, bgMat, borderMat);

            var gun = HudBuilder.MiniGun(_root, gunMat);
            gun.localPosition = new Vector3(-0.008f, 0.004f, -0.004f);
            gun.localRotation = Quaternion.Euler(0f, -90f, 0f); // side profile, barrel to the right
            gun.localScale = Vector3.one * 0.11f;

            _nameText = HudBuilder.Text(_root, "WeaponName", 0.10f, textColor);
            _nameText.rectTransform.localPosition = new Vector3(0f, height * 0.5f - 0.009f, -0.0004f);
            _nameText.rectTransform.sizeDelta = new Vector2(width - 0.008f, 0.014f);
            _nameText.enableAutoSizing = true;
            _nameText.fontSizeMax = 0.10f;
            _nameText.fontSizeMin = 0.04f;

            _ammoText = HudBuilder.Text(_root, "Ammo", 0.17f, textColor, TextAlignmentOptions.MidlineRight);
            _ammoText.rectTransform.sizeDelta = new Vector2(0.06f, 0.03f);
            _ammoText.rectTransform.localPosition = new Vector3(width * 0.5f - 0.036f, -0.014f, -0.0004f);
            _ammoText.fontStyle = FontStyles.Bold;

            _root.gameObject.SetActive(false);
        }

        public void Update(bool visible, string weaponName, int currentAmmo, int maxAmmo, bool infinite)
        {
            if (_root.gameObject.activeSelf != visible)
                _root.gameObject.SetActive(visible);
            if (!visible) return;

            if (weaponName != _lastName)
            {
                _lastName = weaponName;
                _nameText.text = weaponName;
            }
            string ammo = infinite ? "∞" : (currentAmmo < 0 ? "--" : currentAmmo + " / " + maxAmmo);
            if (ammo != _lastAmmo)
            {
                _lastAmmo = ammo;
                _ammoText.text = ammo;
            }
        }
    }

    /// <summary>Vertical health bar with percentage label, colored green→yellow→red.</summary>
    internal class HealthBarWidget
    {
        readonly Transform _fill;
        readonly Material _fillMat;
        readonly TextMeshPro _label;
        readonly float _barHeight;
        readonly float _bottomY;
        int _lastShown = -1;

        public HealthBarWidget(Transform slot, Vector3 localPos, float barHeight, Material railMat, Color textColor)
        {
            _barHeight = barHeight;
            var root = new GameObject("HealthBar").transform;
            root.SetParent(slot, false);
            root.localPosition = localPos;
            _bottomY = -barHeight * 0.5f;

            var rail = HudBuilder.Flat(root, "Rail", railMat);
            rail.localScale = new Vector3(0.0105f, barHeight + 0.004f, 1f);
            rail.localPosition = new Vector3(0f, 0f, -0.0002f);

            _fillMat = HudBuilder.PanelMat(HealthColor(1f), 3993);
            _fill = HudBuilder.Flat(root, "Fill", _fillMat);
            _label = HudBuilder.Text(root, "Pct", 0.10f, textColor);
            _label.rectTransform.localPosition = new Vector3(0f, -barHeight * 0.5f - 0.009f, -0.0004f);
            Update(1f);
        }

        public static Color HealthColor(float h)
        {
            Color green = new Color(0.20f, 0.95f, 0.35f, 0.95f);
            Color yellow = new Color(0.95f, 0.85f, 0.10f, 0.95f);
            Color red = new Color(0.95f, 0.15f, 0.10f, 0.95f);
            return h > 0.5f ? Color.Lerp(yellow, green, (h - 0.5f) * 2f)
                            : Color.Lerp(red, yellow, h * 2f);
        }

        public void Update(float frac)
        {
            frac = Mathf.Clamp01(frac);
            float h = Mathf.Max(frac * _barHeight, 0.0001f);
            _fill.localScale = new Vector3(0.0075f, h, 1f);
            _fill.localPosition = new Vector3(0f, _bottomY + h * 0.5f, -0.0004f);
            _fillMat.color = HealthColor(frac);

            int shown = Mathf.RoundToInt(frac * 100f);
            if (shown != _lastShown)
            {
                _lastShown = shown;
                _label.text = shown.ToString();
            }
        }
    }
}
