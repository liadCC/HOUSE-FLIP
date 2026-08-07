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

        [SerializeField]
        private Color[] playerColors =
        {
            new Color(0.95f, 0.35f, 0.30f),
            new Color(0.32f, 0.62f, 0.95f),
            new Color(0.40f, 0.85f, 0.45f),
            new Color(0.98f, 0.80f, 0.30f)
        };

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

            var controller = playerObject.GetComponent<PlayerController>();
            if (controller != null && playerColors.Length > 0)
            {
                controller.PlayerColor.Value = playerColors[slot % playerColors.Length];
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

            // The character controller fights direct transform writes, so disable it
            // for the single frame in which we teleport.
            var characterController = playerObject.GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            playerObject.transform.SetPositionAndRotation(point.position, point.rotation);

            if (characterController != null)
            {
                characterController.enabled = true;
            }
        }

        public void SetSpawnPoints(Transform[] points) => spawnPoints = points;
    }
}
