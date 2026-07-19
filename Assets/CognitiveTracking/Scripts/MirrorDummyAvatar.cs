using UnityEngine;

namespace TeamSentinels.CognitiveTracking
{
    /// <summary>
    /// Procedural green mannequin built entirely from primitives at runtime.
    /// Driven by the three tracked points available on a standard VR rig
    /// (head + both hands); the rest of the body (torso lean, crouch, elbows,
    /// knees, feet) is estimated with lightweight analytic IK.
    ///
    /// Works in its own local space: floor at y = 0, facing +Z, units in metres.
    /// The parent (CognitiveMirrorHud) scales it down to HUD size.
    /// </summary>
    public class MirrorDummyAvatar : MonoBehaviour
    {
        // ── Materials ────────────────────────────────────────────────────────
        Material _matLegs, _matTorso, _matArms, _matAccent, _matVisor;
        Color _baseColor;
        float _health = 1f;

        // ── Body parts ───────────────────────────────────────────────────────
        Transform _baseDisc;
        Transform _lThigh, _lShin, _rThigh, _rShin, _lFoot, _rFoot;
        Transform _torsoSeg, _neckSeg;
        Transform _lUpperArm, _lForeArm, _rUpperArm, _rForeArm, _lHand, _rHand;
        Transform _headBall, _visor;
        Transform _lGun, _rGun;
        Material _matGun;

        /// <summary>Set by the HUD when the corresponding real hand holds a weapon.</summary>
        public bool LeftWeaponVisible { get; set; }
        public bool RightWeaponVisible { get; set; }

        // ── Solver state ─────────────────────────────────────────────────────
        float _standingEyeY = 1.55f;   // auto-calibrated eye height (metres)
        float _torsoYaw;               // smoothed body yaw, degrees (local)
        float _torsoYawVel;
        Vector3 _lPlant, _rPlant;      // planted foot positions
        bool _feetInit;

        const float RefHeight = 1.70f; // proportions authored for a 1.70 m person

        public static MirrorDummyAvatar Create(Transform parent, Color bodyColor, bool renderOnTop, bool showBaseDisc)
        {
            var go = new GameObject("MirrorDummy");
            go.transform.SetParent(parent, false);
            var avatar = go.AddComponent<MirrorDummyAvatar>();
            avatar.Build(bodyColor, renderOnTop, showBaseDisc);
            return avatar;
        }

        // ── Construction ─────────────────────────────────────────────────────

        void Build(Color bodyColor, bool renderOnTop, bool showBaseDisc)
        {
            _baseColor = bodyColor;

            Shader shader = Resources.Load<Shader>("HudDummy");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            // Painter's order (render queue) matters when drawing with
            // ZTest Always: legs -> torso -> arms -> head/hands, so the most
            // informative parts (hands, head) are never hidden.
            _matLegs   = MakeMat(shader, renderOnTop, 4000);
            _matTorso  = MakeMat(shader, renderOnTop, 4002);
            _matArms   = MakeMat(shader, renderOnTop, 4004);
            _matAccent = MakeMat(shader, renderOnTop, 4006);
            _matVisor  = MakeMat(shader, renderOnTop, 4007);
            ApplyColors();

            _baseDisc  = NewPart(PrimitiveType.Cylinder, "BaseDisc", _matLegs);
            _lThigh    = NewPart(PrimitiveType.Capsule,  "L_Thigh",  _matLegs);
            _lShin     = NewPart(PrimitiveType.Capsule,  "L_Shin",   _matLegs);
            _rThigh    = NewPart(PrimitiveType.Capsule,  "R_Thigh",  _matLegs);
            _rShin     = NewPart(PrimitiveType.Capsule,  "R_Shin",   _matLegs);
            _lFoot     = NewPart(PrimitiveType.Cube,     "L_Foot",   _matLegs);
            _rFoot     = NewPart(PrimitiveType.Cube,     "R_Foot",   _matLegs);
            _torsoSeg  = NewPart(PrimitiveType.Capsule,  "Torso",    _matTorso);
            _neckSeg   = NewPart(PrimitiveType.Capsule,  "Neck",     _matTorso);
            _lUpperArm = NewPart(PrimitiveType.Capsule,  "L_Upper",  _matArms);
            _lForeArm  = NewPart(PrimitiveType.Capsule,  "L_Fore",   _matArms);
            _rUpperArm = NewPart(PrimitiveType.Capsule,  "R_Upper",  _matArms);
            _rForeArm  = NewPart(PrimitiveType.Capsule,  "R_Fore",   _matArms);
            _lHand     = NewPart(PrimitiveType.Cube,     "L_Hand",   _matAccent);
            _rHand     = NewPart(PrimitiveType.Cube,     "R_Hand",   _matAccent);
            _headBall  = NewPart(PrimitiveType.Sphere,   "Head",     _matAccent);
            _visor     = NewPart(PrimitiveType.Cube,     "Visor",    _matVisor);

            // Held-weapon silhouettes (hidden until a real gun is grabbed).
            _matGun = new Material(shader) { renderQueue = _matAccent.renderQueue };
            _matGun.color = new Color(0.34f, 0.48f, 0.38f);
            if (_matGun.HasProperty("_ZTest"))
                _matGun.SetFloat("_ZTest", _matAccent.GetFloat("_ZTest"));
            _lGun = HudBuilder.MiniGun(transform, _matGun);
            _lGun.name = "L_Weapon";
            _rGun = HudBuilder.MiniGun(transform, _matGun);
            _rGun.name = "R_Weapon";
            _lGun.gameObject.SetActive(false);
            _rGun.gameObject.SetActive(false);

            _baseDisc.gameObject.SetActive(showBaseDisc);
        }

        static Material MakeMat(Shader shader, bool renderOnTop, int queue)
        {
            var m = new Material(shader);
            if (m.HasProperty("_ZTest"))
                m.SetFloat("_ZTest", renderOnTop
                    ? (float)UnityEngine.Rendering.CompareFunction.Always
                    : (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            m.renderQueue = renderOnTop ? queue : -1;
            return m;
        }

        Transform NewPart(PrimitiveType type, string partName, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = partName;
            go.layer = gameObject.layer;
            Destroy(go.GetComponent<Collider>()); // HUD only — never collide

            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            var t = go.transform;
            t.SetParent(transform, false);
            return t;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>0 = dead (red), 1 = full health (green). Hook for the future health display.</summary>
        public void SetHealth01(float health)
        {
            _health = Mathf.Clamp01(health);
            ApplyColors();
        }

        public void SetBaseColor(Color c)
        {
            _baseColor = c;
            ApplyColors();
        }

        void ApplyColors()
        {
            // Full health = configured green; drains through yellow to red.
            Color body = _health > 0.5f
                ? Color.Lerp(new Color(0.95f, 0.85f, 0.1f), _baseColor, (_health - 0.5f) * 2f)
                : Color.Lerp(new Color(0.9f, 0.12f, 0.1f), new Color(0.95f, 0.85f, 0.1f), _health * 2f);

            if (_matLegs)   _matLegs.color   = body * 0.85f;
            if (_matTorso)  _matTorso.color  = body;
            if (_matArms)   _matArms.color   = body * 0.95f;
            if (_matAccent) _matAccent.color = Color.Lerp(body, Color.white, 0.30f);
            if (_matVisor)  _matVisor.color  = body * 0.30f;
        }

        /// <summary>
        /// Update the mannequin. All poses are in avatar-local space:
        /// floor at y = 0, metres, +Z roughly forward.
        /// Invalid hands fall back to a relaxed pose at the hip.
        /// </summary>
        public void UpdatePose(Pose head, Pose leftHand, bool leftValid, Pose rightHand, bool rightValid)
        {
            // ── Calibration & proportions ────────────────────────────────────
            _standingEyeY = Mathf.Clamp(Mathf.Max(_standingEyeY, head.position.y), 1.15f, 2.10f);
            float s = (_standingEyeY + 0.10f) / RefHeight; // person height / reference

            Vector3 up = Vector3.up;
            Quaternion headRot = head.rotation;
            Vector3 headC = head.position + headRot * new Vector3(0f, -0.02f, -0.06f);

            // ── Torso orientation ────────────────────────────────────────────
            Vector3 headFwdFlat = headRot * Vector3.forward;
            headFwdFlat.y = 0f;
            float targetYaw = headFwdFlat.sqrMagnitude > 1e-4f
                ? Mathf.Atan2(headFwdFlat.x, headFwdFlat.z) * Mathf.Rad2Deg
                : _torsoYaw;
            _torsoYaw = Mathf.SmoothDampAngle(_torsoYaw, targetYaw, ref _torsoYawVel, 0.25f);

            float crouch = Mathf.Clamp01((_standingEyeY - head.position.y) / (0.45f * _standingEyeY));
            Quaternion torsoRot = Quaternion.Euler(crouch * 42f, _torsoYaw, 0f);

            Vector3 right = torsoRot * Vector3.right;
            Vector3 fwd = torsoRot * Vector3.forward;
            Vector3 fwdFlat = new Vector3(fwd.x, 0f, fwd.z).normalized;
            if (fwdFlat.sqrMagnitude < 1e-4f) fwdFlat = Vector3.forward;

            // ── Spine ────────────────────────────────────────────────────────
            Vector3 neck = headC + Vector3.down * (0.11f * s);
            Vector3 pelvis = neck + torsoRot * (Vector3.down * (0.48f * s));
            if (pelvis.y < 0.30f * s) pelvis.y = 0.30f * s;

            Vector3 lShoulder = neck + torsoRot * new Vector3(-0.18f, -0.04f, 0f) * s;
            Vector3 rShoulder = neck + torsoRot * new Vector3(0.18f, -0.04f, 0f) * s;
            Vector3 lHip = pelvis - right * (0.10f * s);
            Vector3 rHip = pelvis + right * (0.10f * s);

            // ── Arms (two-bone IK, elbows hinted down/back/out) ──────────────
            float upperLen = 0.28f * s, foreLen = 0.27f * s;
            Vector3 lHandPos = leftValid
                ? leftHand.position
                : pelvis - right * (0.26f * s) + fwdFlat * (0.10f * s) + up * (0.12f * s);
            Vector3 rHandPos = rightValid
                ? rightHand.position
                : pelvis + right * (0.26f * s) + fwdFlat * (0.10f * s) + up * (0.12f * s);

            Vector3 lElbow = SolveMiddleJoint(lShoulder, lHandPos, upperLen, foreLen,
                lShoulder - fwdFlat * 0.35f - up * 1.0f - right * 0.5f);
            Vector3 rElbow = SolveMiddleJoint(rShoulder, rHandPos, upperLen, foreLen,
                rShoulder - fwdFlat * 0.35f - up * 1.0f + right * 0.5f);

            // ── Legs (feet plant under hips, knees hinted forward/out) ───────
            Vector3 lPlantTarget = new Vector3(lHip.x, 0f, lHip.z) - fwdFlat * (0.02f * s);
            Vector3 rPlantTarget = new Vector3(rHip.x, 0f, rHip.z) - fwdFlat * (0.02f * s);
            if (!_feetInit)
            {
                _lPlant = lPlantTarget;
                _rPlant = rPlantTarget;
                _feetInit = true;
            }
            float footBlend = 1f - Mathf.Exp(-6f * Time.deltaTime);
            _lPlant = Vector3.Lerp(_lPlant, lPlantTarget, footBlend);
            _rPlant = Vector3.Lerp(_rPlant, rPlantTarget, footBlend);

            float ankleY = 0.05f * s;
            float thighLen = 0.42f * s, shinLen = 0.40f * s;
            Vector3 lAnkle = _lPlant + up * ankleY;
            Vector3 rAnkle = _rPlant + up * ankleY;
            Vector3 lKnee = SolveMiddleJoint(lHip, lAnkle, thighLen, shinLen,
                lHip + fwdFlat * 1.0f - right * 0.2f);
            Vector3 rKnee = SolveMiddleJoint(rHip, rAnkle, thighLen, shinLen,
                rHip + fwdFlat * 1.0f + right * 0.2f);

            // ── Place geometry ───────────────────────────────────────────────
            PlaceCapsule(_torsoSeg, neck, pelvis, 0.13f * s);
            PlaceCapsule(_neckSeg, headC, neck + Vector3.down * (0.02f * s), 0.045f * s);

            PlaceCapsule(_lUpperArm, lShoulder, lElbow, 0.045f * s);
            PlaceCapsule(_lForeArm, lElbow, lHandPos, 0.040f * s);
            PlaceCapsule(_rUpperArm, rShoulder, rElbow, 0.045f * s);
            PlaceCapsule(_rForeArm, rElbow, rHandPos, 0.040f * s);

            PlaceCapsule(_lThigh, lHip, lKnee, 0.058f * s);
            PlaceCapsule(_lShin, lKnee, lAnkle, 0.050f * s);
            PlaceCapsule(_rThigh, rHip, rKnee, 0.058f * s);
            PlaceCapsule(_rShin, rKnee, rAnkle, 0.050f * s);

            Quaternion footRot = Quaternion.LookRotation(fwdFlat, up);
            _lFoot.localPosition = _lPlant + up * (0.030f * s) + fwdFlat * (0.05f * s);
            _lFoot.localRotation = footRot;
            _lFoot.localScale = new Vector3(0.09f, 0.055f, 0.22f) * s;
            _rFoot.localPosition = _rPlant + up * (0.030f * s) + fwdFlat * (0.05f * s);
            _rFoot.localRotation = footRot;
            _rFoot.localScale = new Vector3(0.09f, 0.055f, 0.22f) * s;

            _lHand.localPosition = lHandPos;
            _lHand.localRotation = leftValid ? leftHand.rotation : torsoRot;
            _lHand.localScale = new Vector3(0.07f, 0.035f, 0.11f) * s;
            _rHand.localPosition = rHandPos;
            _rHand.localRotation = rightValid ? rightHand.rotation : torsoRot;
            _rHand.localScale = new Vector3(0.07f, 0.035f, 0.11f) * s;

            bool showLGun = LeftWeaponVisible;
            bool showRGun = RightWeaponVisible;
            if (_lGun.gameObject.activeSelf != showLGun) _lGun.gameObject.SetActive(showLGun);
            if (_rGun.gameObject.activeSelf != showRGun) _rGun.gameObject.SetActive(showRGun);
            if (showLGun)
            {
                _lGun.localPosition = lHandPos + _lHand.localRotation * new Vector3(0f, 0.015f, 0.02f) * s;
                _lGun.localRotation = _lHand.localRotation;
                _lGun.localScale = Vector3.one * s;
            }
            if (showRGun)
            {
                _rGun.localPosition = rHandPos + _rHand.localRotation * new Vector3(0f, 0.015f, 0.02f) * s;
                _rGun.localRotation = _rHand.localRotation;
                _rGun.localScale = Vector3.one * s;
            }

            _headBall.localPosition = headC;
            _headBall.localRotation = headRot;
            _headBall.localScale = Vector3.one * (0.22f * s);
            _visor.localPosition = headC + headRot * new Vector3(0f, 0.005f, 0.10f) * s;
            _visor.localRotation = headRot;
            _visor.localScale = new Vector3(0.12f, 0.035f, 0.04f) * s;

            _baseDisc.localPosition = new Vector3(0f, 0.002f, 0f);
            _baseDisc.localScale = new Vector3(0.62f * s, 0.0025f, 0.62f * s);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        static void PlaceCapsule(Transform t, Vector3 a, Vector3 b, float radius)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f)
            {
                t.localPosition = a;
                t.localScale = new Vector3(radius * 2f, radius, radius * 2f);
                return;
            }
            t.localPosition = (a + b) * 0.5f;
            t.localRotation = Quaternion.FromToRotation(Vector3.up, d / len);
            t.localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
        }

        /// <summary>Analytic two-bone IK: returns the middle joint (elbow/knee) position.</summary>
        static Vector3 SolveMiddleJoint(Vector3 root, Vector3 target, float len1, float len2, Vector3 bendHint)
        {
            Vector3 d = target - root;
            float dist = d.magnitude;
            if (dist < 1e-4f) return root + Vector3.down * len1;

            Vector3 dn = d / dist;
            float reach = Mathf.Clamp(dist, Mathf.Abs(len1 - len2) + 0.01f, len1 + len2 - 0.001f);
            float along = (len1 * len1 - len2 * len2 + reach * reach) / (2f * reach);
            float liftSq = len1 * len1 - along * along;
            float lift = liftSq > 0f ? Mathf.Sqrt(liftSq) : 0f;

            Vector3 bend = Vector3.ProjectOnPlane(bendHint - root, dn);
            if (bend.sqrMagnitude < 1e-6f)
            {
                bend = Vector3.ProjectOnPlane(Vector3.forward, dn);
                if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(Vector3.up, dn);
            }
            return root + dn * along + bend.normalized * lift;
        }

        void OnDestroy()
        {
            Destroy(_matLegs);
            Destroy(_matTorso);
            Destroy(_matArms);
            Destroy(_matAccent);
            Destroy(_matVisor);
            Destroy(_matGun);
        }
    }
}
