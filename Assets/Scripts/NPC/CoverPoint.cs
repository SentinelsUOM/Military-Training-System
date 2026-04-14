using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a world-space position that a terrorist can use for cover.
///
/// Setup:
///   1. Place an empty GameObject at each cover position (corner of a wall, crate, doorframe).
///   2. Add this component.
///   3. Set protectedDirection to face away from the expected threat (e.g. pointing toward the wall).
///   4. Tag the GameObject "CoverPoint" (optional — component auto-registers itself).
///
/// TerroristController calls CoverPoint.SelectBest() to find the highest-scoring
/// available cover when transitioning to TakeCover state.
///
/// Score formula (higher = better):
///   shieldScore = dot( protectedDirection, -threatDir )   — 1.0 = perfect cover
///   score       = shieldScore − (distance × 0.1)
/// </summary>
public class CoverPoint : MonoBehaviour
{
    [Tooltip("Direction this cover protects from (should face away from likely threat direction).")]
    public Vector3 protectedDirection = Vector3.forward;

    [Tooltip("Only one NPC may claim this point at a time.")]
    public bool isOccupied { get; private set; }

    // ── Static registry ───────────────────────────────────────────────────────

    static readonly List<CoverPoint> _all = new List<CoverPoint>();

    void OnEnable()  => _all.Add(this);
    void OnDisable() => _all.Remove(this);

    /// <summary>
    /// Find the best unoccupied cover point for an NPC at <paramref name="npcPos"/>
    /// facing a threat from direction <paramref name="threatDir"/>.
    /// Returns null if no cover points exist or all are occupied.
    /// </summary>
    public static CoverPoint SelectBest(Vector3 npcPos, Vector3 threatDir)
    {
        CoverPoint best      = null;
        float      bestScore = float.MinValue;

        Vector3 normThreat = threatDir.normalized;

        foreach (var cp in _all)
        {
            if (cp == null || cp.isOccupied) continue;

            float dist        = Vector3.Distance(npcPos, cp.transform.position);
            float shieldScore = Vector3.Dot(cp.protectedDirection.normalized, -normThreat);
            float score       = shieldScore - dist * 0.1f;

            if (score > bestScore)
            {
                bestScore = score;
                best      = cp;
            }
        }

        return best;
    }

    /// <summary>Mark this point as in use.  Call when the NPC starts moving here.</summary>
    public void Claim()   => isOccupied = true;

    /// <summary>Release so another NPC can use it.</summary>
    public void Release() => isOccupied = false;

    void OnDrawGizmos()
    {
        Gizmos.color = isOccupied ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawRay(transform.position, protectedDirection * 0.8f);
    }
}
