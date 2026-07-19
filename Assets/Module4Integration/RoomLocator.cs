// Module 4 Integration | Sentinels | University of Moratuwa | 2026
//
// Lives in Assembly-CSharp (no asmdef in this folder) so it can see both the
// Module 4 data types and Assembly-CSharp gameplay.

using System.Collections.Generic;
using TeamSentinels.Module4.Data;
using UnityEngine;

/// <summary>
/// Resolves a world position to the room it sits in, using the built scenario's
/// top-down layout. Populated by <see cref="Module4SessionController"/> when a
/// session starts, and queried by <see cref="Module4Bridge"/> to tag events (and
/// therefore incidents) with a readable room instead of "unknown".
/// </summary>
public static class RoomLocator
{
    private static readonly List<RoomBox> _rooms = new List<RoomBox>();

    /// <summary>Loads the room boxes for the current scenario. Pass the LayoutSnapshot rooms.</summary>
    public static void SetRooms(IEnumerable<RoomBox> rooms)
    {
        _rooms.Clear();
        if (rooms != null) _rooms.AddRange(rooms);
    }

    public static void Clear() => _rooms.Clear();

    /// <summary>
    /// Readable label of the room containing <paramref name="pos"/> (matched on the
    /// X/Z plane), or null if the position isn't inside any known room.
    /// </summary>
    public static string RoomLabelAt(Vector3 pos)
    {
        foreach (var r in _rooms)
        {
            if (r == null) continue;
            float hx = r.width * 0.5f;
            float hz = r.depth * 0.5f;
            if (Mathf.Abs(pos.x - r.centerX) <= hx && Mathf.Abs(pos.z - r.centerZ) <= hz)
                return Label(r);
        }
        return null;
    }

    // Turns a room type into a human phrase; standard rooms fall back to their id.
    private static string Label(RoomBox r)
    {
        switch ((r.type ?? "").ToLowerInvariant())
        {
            case "entry":        return "the entry room";
            case "corridor":     return "the corridor";
            case "hostageroom":  return "the hostage room";
            default:             return string.IsNullOrEmpty(r.id) ? "a room" : r.id;
        }
    }
}
