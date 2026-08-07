using System;
using HouseFlip.Core;
using HouseFlip.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace HouseFlip.Networking
{
    /// <summary>
    /// Starts a host or joins one (GDD 4, lobby system). Direct-IP for the MVP; Steam
    /// lobbies are a post-MVP concern (GDD 26) and slot in behind this same API.
    /// </summary>
    public class NetworkBootstrap : LocalSingleton<NetworkBootstrap>
    {
        [SerializeField] private string defaultAddress = "127.0.0.1";
        [SerializeField] private ushort defaultPort = 7777;
        [SerializeField] private int maxPlayers = 4;

        public string Address { get; private set; }
        public ushort Port { get; private set; }

        public bool IsRunning => NetworkManager.Singleton != null &&
                                 (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient ||
                                  NetworkManager.Singleton.IsServer);

        public event Action<string> StatusChanged;

        protected override void Awake()
        {
            base.Awake();
            Address = defaultAddress;
            Port = defaultPort;
        }

        private void Start()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                Debug.LogError("[NetworkBootstrap] No NetworkManager in the scene.");
                return;
            }

            manager.ConnectionApprovalCallback = ApproveConnection;
            manager.OnClientDisconnectCallback += OnClientDisconnect;
        }

        protected override void OnDestroy()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null)
            {
                manager.OnClientDisconnectCallback -= OnClientDisconnect;
            }

            base.OnDestroy();
        }

        public void SetConnectionTarget(string address, ushort port)
        {
            if (!string.IsNullOrWhiteSpace(address))
            {
                Address = address.Trim();
            }

            if (port != 0)
            {
                Port = port;
            }
        }

        public bool StartHost()
        {
            if (!ConfigureTransport())
            {
                return false;
            }

            bool started = NetworkManager.Singleton.StartHost();
            StatusChanged?.Invoke(started ? $"Hosting on {Address}:{Port}" : "Failed to start host");
            return started;
        }

        public bool StartClient()
        {
            if (!ConfigureTransport())
            {
                return false;
            }

            bool started = NetworkManager.Singleton.StartClient();
            StatusChanged?.Invoke(started ? $"Connecting to {Address}:{Port}…" : "Failed to start client");
            return started;
        }

        public void Shutdown()
        {
            if (NetworkManager.Singleton != null && IsRunning)
            {
                NetworkManager.Singleton.Shutdown();
            }

            PlayerRegistry.Clear();
            StatusChanged?.Invoke("Disconnected");
        }

        private bool ConfigureTransport()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                return false;
            }

            var transport = manager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[NetworkBootstrap] NetworkManager is missing a UnityTransport component.");
                return false;
            }

            transport.SetConnectionData(Address, Port);
            return true;
        }

        /// <summary>Caps the session at four players (GDD 4).</summary>
        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            int connected = NetworkManager.Singleton.ConnectedClientsIds.Count;

            response.Approved = connected < maxPlayers;
            response.CreatePlayerObject = true;
            response.Pending = false;

            if (!response.Approved)
            {
                response.Reason = "Game is full (4 players).";
            }
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer
                && clientId == NetworkManager.Singleton.LocalClientId)
            {
                string reason = NetworkManager.Singleton.DisconnectReason;
                StatusChanged?.Invoke(string.IsNullOrEmpty(reason) ? "Disconnected" : reason);
            }
        }
    }
}
