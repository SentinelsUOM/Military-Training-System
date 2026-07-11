// Module 2 | Sentinels | University of Moratuwa | 2026

using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gives the trainee a moving NavMesh obstacle so terrorist NavMeshAgents steer
/// AROUND the player instead of walking through them. This is the physical backstop
/// to the standoff logic in <see cref="TerroristController"/>: even if combat code
/// ever steered an agent inward, the obstacle stops bodies overlapping.
///
/// Carving is OFF on purpose — the player moves every frame, and a carving obstacle
/// would constantly rebuild the NavMesh (expensive). With carving off the agents use
/// local (velocity-based) avoidance, which is the correct choice for a moving obstacle.
///
/// Self-bootstrapping: spawns itself after the first scene load and persists across
/// scene reloads (mission restart), so there is nothing to wire in the Inspector.
/// </summary>
[RequireComponent(typeof(NavMeshObstacle))]
public class PlayerNavMeshObstacle : MonoBehaviour
{
    [Tooltip("Capsule radius (m) the agents keep clear around the player.")]
    public float radius = 0.35f;

    [Tooltip("Capsule height (m).")]
    public float height = 1.8f;

    private static PlayerNavMeshObstacle _instance;
    private NavMeshObstacle _obstacle;
    private Transform _player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[PlayerNavMeshObstacle]");
        go.AddComponent<PlayerNavMeshObstacle>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        _obstacle = GetComponent<NavMeshObstacle>();
        _obstacle.shape   = NavMeshObstacleShape.Capsule;
        _obstacle.radius  = radius;
        _obstacle.height  = height;
        _obstacle.center  = Vector3.zero;
        _obstacle.carving = false;   // moving obstacle → velocity-based avoidance, not carving
        _obstacle.enabled = false;   // off until we've located the player
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        // (Re)acquire the player — Camera.main is the head and always moves in VR;
        // fall back to the PlayerHealth rig. Re-finds after a scene reload.
        if (_player == null)
        {
            var cam = Camera.main;
            _player = cam != null ? cam.transform
                                  : FindFirstObjectByType<PlayerHealth>()?.transform;
        }

        if (_player == null) { if (_obstacle.enabled) _obstacle.enabled = false; return; }

        if (!_obstacle.enabled) _obstacle.enabled = true;

        // Follow the player's footprint, dropped to mid-body height so the capsule
        // spans floor-to-shoulders regardless of the head camera's height.
        Vector3 p = _player.position;
        transform.position = new Vector3(p.x, p.y - height * 0.5f, p.z);
    }
}
