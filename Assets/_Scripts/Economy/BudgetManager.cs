using HouseFlip.Core;
using HouseFlip.Player;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Economy
{
    /// <summary>
    /// The single shared wallet (GDD 15). Host-authoritative: every spend goes through
    /// <see cref="TrySpend"/> on the server, and clients only ever read the replicated
    /// value. There is deliberately no client-side deduction anywhere in the codebase.
    /// </summary>
    public class BudgetManager : NetSingleton<BudgetManager>
    {
        private readonly NetworkVariable<float> _budget = new NetworkVariable<float>(
            GameConstants.StartingBudget,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>Running total of everything spent, split for the results screen (GDD 19).</summary>
        private readonly NetworkVariable<float> _spentOnRenovation = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _spentOnFurniture = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public float Current => _budget.Value;
        public float SpentOnRenovation => _spentOnRenovation.Value;
        public float SpentOnFurniture => _spentOnFurniture.Value;
        public float TotalSpent => _spentOnRenovation.Value + _spentOnFurniture.Value;

        public override void OnNetworkSpawn()
        {
            _budget.OnValueChanged += OnBudgetChanged;
            GameEvents.RaiseBudgetChanged(_budget.Value);
        }

        public override void OnNetworkDespawn()
        {
            _budget.OnValueChanged -= OnBudgetChanged;
        }

        private void OnBudgetChanged(float previous, float current) => GameEvents.RaiseBudgetChanged(current);

        /// <summary>Server only. Resets the wallet for a fresh round.</summary>
        public void ServerResetForNewRound()
        {
            if (!IsServer)
            {
                return;
            }

            _budget.Value = GameConstants.StartingBudget;
            _spentOnRenovation.Value = 0f;
            _spentOnFurniture.Value = 0f;
        }

        /// <summary>
        /// Server only. Attempts a purchase. Returns false and leaves the budget untouched
        /// when the group cannot afford it (GDD 15).
        /// </summary>
        public bool TrySpend(float cost, ulong spenderClientId, SpendCategory category)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[BudgetManager] TrySpend called on a client — ignored.");
                return false;
            }

            if (cost < 0f)
            {
                return false;
            }

            if (_budget.Value < cost)
            {
                NotifyRejectedClientRpc(
                    "No Budget!",
                    new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new[] { spenderClientId } }
                    });
                return false;
            }

            _budget.Value -= cost;

            if (category == SpendCategory.Furniture)
            {
                _spentOnFurniture.Value += cost;
            }
            else
            {
                _spentOnRenovation.Value += cost;
            }

            PlayerStatsTracker.Record(spenderClientId, PlayerStat.MoneySpent, cost);
            NotifyPurchaseClientRpc(cost);
            return true;
        }

        /// <summary>Server only. Refunds without counting as income (used when a placement is undone).</summary>
        public void Refund(float amount, SpendCategory category)
        {
            if (!IsServer || amount <= 0f)
            {
                return;
            }

            _budget.Value += amount;

            if (category == SpendCategory.Furniture)
            {
                _spentOnFurniture.Value = Mathf.Max(0f, _spentOnFurniture.Value - amount);
            }
            else
            {
                _spentOnRenovation.Value = Mathf.Max(0f, _spentOnRenovation.Value - amount);
            }
        }

        public bool CanAfford(float cost) => _budget.Value >= cost;

        [ClientRpc]
        private void NotifyRejectedClientRpc(string reason, ClientRpcParams rpcParams = default)
        {
            GameEvents.RaisePurchaseRejected(reason);
        }

        [ClientRpc]
        private void NotifyPurchaseClientRpc(float amount)
        {
            GameEvents.RaisePurchaseCompleted(amount);
            GameEvents.RaiseSfx(SfxId.CashRegister);
        }
    }

    public enum SpendCategory
    {
        Renovation = 0,
        Furniture = 1
    }
}
