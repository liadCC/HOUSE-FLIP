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

        private int _assigned;

        private void Start()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                return;
            }

            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnServerStarted += OnServerStarted;
        }

        private void OnDestroy()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null)
            {
                manager.OnClientConnectedCallback -= OnClientConnected;
                manager.OnServerStarted -= OnServerStarted;
            }
        }

        private void OnServerStarted()
        {
            _assigned = 0;
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

            int slot = _assigned++;

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
