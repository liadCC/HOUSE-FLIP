using HouseFlip.Core;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.GameFlow
{
    /// <summary>
    /// Ready-up gate before a round starts (GDD 4). The host begins the round once every
    /// connected player has readied.
    /// </summary>
    public class LobbyManager : NetSingleton<LobbyManager>
    {
        private NetworkList<ulong> _readyClients;

        public int ReadyCount => _readyClients != null ? _readyClients.Count : 0;

        public int ConnectedCount =>
            NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsIds.Count : 0;

        public bool EveryoneReady => ConnectedCount > 0 && ReadyCount >= ConnectedCount;

        protected override void Awake()
        {
            base.Awake();
            _readyClients = new NetworkList<ulong>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            RemoveReady(clientId);
            TryStartRound();
        }

        public bool IsReady(ulong clientId)
        {
            return _readyClients != null && _readyClients.Contains(clientId);
        }

        public bool IsLocalPlayerReady()
        {
            return NetworkManager.Singleton != null && IsReady(NetworkManager.Singleton.LocalClientId);
        }

        /// <summary>Called from the lobby UI on the local client.</summary>
        public void ToggleReady()
        {
            if (IsSpawned)
            {
                ToggleReadyServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ToggleReadyServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;

            // A ready-up only means anything in the lobby. Accepting one during a round would
            // bank it in the list, and because the list is only cleared when a round starts,
            // it would still be there on the return to lobby — so the next round could
            // auto-start off readies nobody gave it while sitting on the lobby screen.
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Lobby)
            {
                return;
            }

            if (IsReady(sender))
            {
                RemoveReady(sender);
            }
            else
            {
                _readyClients.Add(sender);
            }

            TryStartRound();
        }

        private void RemoveReady(ulong clientId)
        {
            for (int i = 0; i < _readyClients.Count; i++)
            {
                if (_readyClients[i] == clientId)
                {
                    _readyClients.RemoveAt(i);
                    return;
                }
            }
        }

        private void TryStartRound()
        {
            if (!IsServer || GameManager.Instance == null)
            {
                return;
            }

            if (GameManager.Instance.State != GameState.Lobby)
            {
                return;
            }

            if (EveryoneReady)
            {
                _readyClients.Clear();
                GameManager.Instance.StartRound();
            }
        }

        /// <summary>Host override: start regardless of who has readied.</summary>
        public void ForceStart()
        {
            if (IsSpawned)
            {
                ForceStartServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ForceStartServerRpc(ServerRpcParams rpcParams = default)
        {
            // Only the host may skip the ready gate.
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Lobby)
            {
                _readyClients.Clear();
                GameManager.Instance.StartRound();
            }
        }
    }
}
