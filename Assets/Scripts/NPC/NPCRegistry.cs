using System.Collections.Generic;

/// <summary>
/// Global list of all active NPC responders.
///
/// NPCs self-register on Awake and unregister on OnDestroy,
/// so the list always reflects what is live in the scene.
/// EventManager queries this to find candidates for each event.
/// </summary>
public static class NPCRegistry
{
    static readonly List<INPCResponder> _all = new List<INPCResponder>();

    public static void Register(INPCResponder npc)
    {
        if (!_all.Contains(npc))
            _all.Add(npc);
    }

    public static void Unregister(INPCResponder npc) => _all.Remove(npc);

    /// All currently registered responders. Do not modify the returned list.
    public static IReadOnlyList<INPCResponder> GetAll() => _all;
}
