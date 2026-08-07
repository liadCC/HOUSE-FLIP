using HouseFlip.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Player
{
    /// <summary>
    /// Per-player counters used by the awards screen (GDD 20).
    ///
    /// Accumulated on the server only — clients read the totals once, at results time,
    /// so a client cannot inflate its own "most objects destroyed" count.
    /// </summary>
    public class PlayerStatsTracker : NetworkBehaviour
    {
        private readonly float[] _stats = new float[PlayerStatExtensions.Count];

        public readonly NetworkVariable<FixedString32Bytes> DisplayName =
            new NetworkVariable<FixedString32Bytes>(
                default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            if (IsServer && DisplayName.Value.Length == 0)
            {
                DisplayName.Value = new FixedString32Bytes($"Player {OwnerClientId + 1}");
            }

            if (IsServer)
            {
                GameEvents.StatRecorded += OnStatRecorded;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                GameEvents.StatRecorded -= OnStatRecorded;
            }
        }

        private void OnStatRecorded(ulong clientId, PlayerStat stat, float amount)
        {
            if (clientId == OwnerClientId)
            {
                Add(stat, amount);
            }
        }

        /// <summary>Server only.</summary>
        public void Add(PlayerStat stat, float amount)
        {
            if (!IsServer)
            {
                return;
            }

            _stats[(int)stat] += amount;
        }

        public float Get(PlayerStat stat) => _stats[(int)stat];

        public void ResetStats()
        {
            if (!IsServer)
            {
                return;
            }

            for (int i = 0; i < _stats.Length; i++)
            {
                _stats[i] = 0f;
            }
        }

        /// <summary>
        /// Convenience for gameplay code: record against whichever player caused the action.
        /// Safe to call from the server for any client id.
        /// </summary>
        public static void Record(ulong clientId, PlayerStat stat, float amount)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            GameEvents.RaiseStatRecorded(clientId, stat, amount);
        }
    }
}
