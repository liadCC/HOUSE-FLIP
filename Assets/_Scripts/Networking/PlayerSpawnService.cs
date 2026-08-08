using HouseFlip.Core;
using HouseFlip.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Networking
{
    /// <summary>
    /// Server-side setup for each joining player: a colour, a name, and a spawn point.
    /// Netcode creates the player object itself; this decorates it.
    /// </summary>
    public class PlayerSpawnService : MonoBehaviour
    {
        [SerializeField] private Transform[] spawnPoints;

        [Tooltip("Left empty, the shared ArtPalette colours are used.")]
        [SerializeField] private Color[] playerColors;

        /// <summary>Slot index currently held by each connected client.</summary>
        private readonly System.Collections.Generic.Dictionary<ulong, int> _slots =
            new System.Collections.Generic.Dictionary<ulong, int>();

        private void Start()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                return;
            }

            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
            manager.OnServerStarted += OnServerStarted;
        }

        private void OnDestroy()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null)
            {
                manager.OnClientConnectedCallback -= OnClientConnected;
                manager.OnClientDisconnectCallback -= OnClientDisconnected;
                manager.OnServerStarted -= OnServerStarted;
            }
        }

        private void OnServerStarted()
        {
            _slots.Clear();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            // Freeing the slot is what makes it reusable. A counter that only ever went up
            // would hand the fifth *join* of a four-player session slot 4, which wraps back
            // onto slot 0 — the reconnecting player would come back in someone else's colour
            // and land on top of them at their spawn point.
            _slots.Remove(clientId);
        }

        /// <summary>Lowest slot index nobody currently occupies.</summary>
        private int TakeSlot(ulong clientId)
        {
            if (_slots.TryGetValue(clientId, out int existing))
            {
                return existing;
            }

            int slot = 0;
            while (_slots.ContainsValue(slot))
            {
                slot++;
            }

            _slots[clientId] = slot;
            return slot;
        }

        private void OnClientConnected(ulong clientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer)
            {
                return;
            }

            NetworkObject playerObject = manager.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObject == null)
            {
                return;
            }

            int slot = TakeSlot(clientId);

            // Fall back to the palette so the four players are always distinguishable
            // even if nobody filled the inspector array in.
            Color[] colors = playerColors != null && playerColors.Length > 0
                ? playerColors
                : Art.ArtPalette.PlayerColors;

            var controller = playerObject.GetComponent<PlayerController>();
            if (controller != null && colors.Length > 0)
            {
                controller.PlayerColor.Value = colors[slot % colors.Length];
            }

            var stats = playerObject.GetComponent<PlayerStatsTracker>();
            if (stats != null)
            {
                stats.DisplayName.Value = new FixedString32Bytes($"Player {slot + 1}");
            }

            MoveToSpawnPoint(playerObject, slot);
        }

        private void MoveToSpawnPoint(NetworkObject playerObject, int slot)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return;
            }

            Transform point = spawnPoints[slot % spawnPoints.Length];
            if (point == null)
            {
                return;
            }

            var controller = playerObject.GetComponent<PlayerController>();
            if (controller == null)
            {
                return;
            }

            // Ask the owner to move rather than writing the transform here. Players are
            // owner-authoritative (GDD 22), so a server-side write would be overwritten
            // by the owner's next update and everyone would end up stacked on the
            // prefab's origin — which sits inside the house, not out in the yard.
            controller.TeleportClientRpc(point.position, point.rotation);
        }

        public void SetSpawnPoints(Transform[] points) => spawnPoints = points;
    }
}
