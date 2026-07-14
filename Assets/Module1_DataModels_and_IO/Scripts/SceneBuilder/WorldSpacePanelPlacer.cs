// =============================================================================
// WorldSpacePanelPlacer.cs
// Module 1 - Dynamic Scenario Generation (Evaluator UI)
// Team Sentinels | University of Moratuwa | 2026
//
// Anchors a world-space Canvas in front of the player's head at startup so the
// evaluator panel is reachable in VR no matter where the XR rig spawns or which
// way the trainee is facing after a recenter.
//
// LOCATION NOTE:
//   Lives alongside EvaluatorConfigPanel.cs (Assembly-CSharp) for the same
//   reason - see the header of that file.
// =============================================================================

using System.Collections;
using UnityEngine;

namespace TeamSentinels.ScenarioGeneration.Scene
{
    /// <summary>
    /// Places this transform a fixed distance in front of the XR camera, facing
    /// the player. Screen-space canvases are never rendered by Unity in VR, so
    /// the evaluator panel must be world-space; this keeps it from ending up
    /// behind the trainee.
    /// </summary>
    public class WorldSpacePanelPlacer : MonoBehaviour
    {
        [Tooltip("Metres in front of the player's head to place the panel.")]
        public float distance = 1.4f;

        [Tooltip("Metres above (+) or below (-) eye height.")]
        public float heightOffset = -0.15f;

        [Tooltip("Place the panel automatically when the scene starts.")]
        public bool placeOnStart = true;

        [Tooltip("Frames to wait before placing, so the headset has reported a " +
                 "valid head pose. Placing on frame 0 would anchor the panel to " +
                 "the origin instead of the player.")]
        public int warmupFrames = 10;

        private IEnumerator Start()
        {
            if (!placeOnStart)
                yield break;

            for (int i = 0; i < warmupFrames; i++)
                yield return null;

            PlaceInFrontOfPlayer();
        }

        /// <summary>
        /// Re-centres the panel in front of the player. Safe to call from a UI
        /// button or an input action if the trainee walks away from it.
        /// </summary>
        public void PlaceInFrontOfPlayer()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[WorldSpacePanelPlacer] No Camera.main; leaving panel where it is.", this);
                return;
            }

            // Flatten to the horizontal plane so the panel stays upright even if
            // the trainee is looking up or down when the scene starts.
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            transform.position = cam.transform.position
                                 + forward * distance
                                 + Vector3.up * heightOffset;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
