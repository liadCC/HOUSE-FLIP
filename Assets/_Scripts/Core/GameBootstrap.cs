using HouseFlip.Economy;
using HouseFlip.Player;
using HouseFlip.Polish;
using HouseFlip.UI;
using UnityEngine;

namespace HouseFlip.Core
{
    /// <summary>
    /// Clears every piece of static state before a play session starts.
    ///
    /// The project leans on statics deliberately — an event bus, a player lookup, a room
    /// registry, a debris pool — and Unity does not reset those between runs when
    /// "Enter Play Mode Options" has domain reload disabled, which is the default for
    /// fast iteration. Without this, the second run of a session inherits event
    /// subscribers pointing at destroyed objects and a debris pool full of dead
    /// GameObjects, and fails in ways that look like gameplay bugs.
    ///
    /// SubsystemRegistration runs before any scene loads, which is the only point early
    /// enough to be safe.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics()
        {
            GameEvents.ResetAll();
            PlayerRegistry.Clear();
            RoomRegistry.Clear();
            DebrisBurst.ResetPool();
            InteractionPromptUI.Clear();
        }
    }
}
