using System.Collections.Generic;

namespace HouseFlip.Player
{
    /// <summary>
    /// Lookup for every spawned player, so systems can resolve a clientId to a body
    /// without walking the scene graph. Populated by <see cref="PlayerController"/>.
    /// </summary>
    public static class PlayerRegistry
    {
        private static readonly Dictionary<ulong, PlayerController> Players =
            new Dictionary<ulong, PlayerController>();

        public static PlayerController LocalPlayer { get; set; }

        public static IReadOnlyDictionary<ulong, PlayerController> All => Players;

        public static int Count => Players.Count;

        public static void Register(PlayerController player)
        {
            if (player != null)
            {
                Players[player.OwnerClientId] = player;
            }
        }

        public static void Unregister(PlayerController player)
        {
            if (player != null)
            {
                Players.Remove(player.OwnerClientId);
            }
        }

        public static PlayerController Get(ulong clientId)
        {
            return Players.TryGetValue(clientId, out PlayerController player) ? player : null;
        }

        public static void Clear()
        {
            Players.Clear();
            LocalPlayer = null;
        }
    }
}
