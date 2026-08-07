using HouseFlip.Core;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Economy
{
    /// <summary>
    /// Live estimated house value (GDD 16). Recalculated on the server whenever anything
    /// in the house changes, then replicated. Clients never do this maths themselves —
    /// they read <see cref="Current"/>.
    /// </summary>
    public class HouseValueManager : NetSingleton<HouseValueManager>
    {
        private readonly NetworkVariable<float> _houseValue = new NetworkVariable<float>(
            GameConstants.HouseBaseValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // Server-side accumulators for one-shot bonuses that are not derivable from
        // the current room state (a repaired tap looks identical to one that was never broken).
        private float _renovationBonus;
        private float _furnitureBonus;

        private bool _dirty;

        public float Current => _houseValue.Value;
        public float RenovationBonus => _renovationBonus;
        public float FurnitureBonus => _furnitureBonus;

        public override void OnNetworkSpawn()
        {
            _houseValue.OnValueChanged += OnValueChanged;
            GameEvents.RaiseHouseValueChanged(_houseValue.Value);

            if (IsServer)
            {
                GameEvents.HouseStateDirty += MarkDirty;
                MarkDirty();
            }
        }

        public override void OnNetworkDespawn()
        {
            _houseValue.OnValueChanged -= OnValueChanged;

            if (IsServer)
            {
                GameEvents.HouseStateDirty -= MarkDirty;
            }
        }

        private void OnValueChanged(float previous, float current) => GameEvents.RaiseHouseValueChanged(current);

        private void MarkDirty() => _dirty = true;

        private void LateUpdate()
        {
            // Coalesce: demolishing a wall can fire a dozen change notifications in one
            // frame, and there is no reason to run the full sweep for each of them.
            if (IsServer && _dirty)
            {
                _dirty = false;
                Recalculate();
            }
        }

        /// <summary>Server only.</summary>
        public void ServerResetForNewRound()
        {
            if (!IsServer)
            {
                return;
            }

            _renovationBonus = 0f;
            _furnitureBonus = 0f;
            _houseValue.Value = GameConstants.HouseBaseValue;
            MarkDirty();
        }

        /// <summary>Server only. Called when a repair or a built object adds lasting value.</summary>
        public void OnRepairComplete(float bonus)
        {
            if (!IsServer)
            {
                return;
            }

            _renovationBonus += Mathf.Max(0f, bonus);
            MarkDirty();
        }

        /// <summary>Server only.</summary>
        public void OnFurniturePlaced(float bonus)
        {
            if (!IsServer)
            {
                return;
            }

            _furnitureBonus += Mathf.Max(0f, bonus);
            MarkDirty();
        }

        /// <summary>Server only. Removing furniture claws the bonus back.</summary>
        public void OnFurnitureRemoved(float bonus)
        {
            if (!IsServer)
            {
                return;
            }

            _furnitureBonus = Mathf.Max(0f, _furnitureBonus - Mathf.Max(0f, bonus));
            MarkDirty();
        }

        /// <summary>Server only. Called by demolition when something load-bearing is wrecked.</summary>
        public void OnStructureDamaged()
        {
            if (IsServer)
            {
                MarkDirty();
            }
        }

        /// <summary>Server only. Full sweep of the current house state.</summary>
        public void Recalculate()
        {
            if (!IsServer)
            {
                return;
            }

            HouseValueBreakdown breakdown = BuildBreakdown();
            _houseValue.Value = breakdown.FinalValue;
        }

        /// <summary>
        /// Produces the itemised list the inspection screen prints (GDD 19).
        /// Safe to call on the server at any time.
        /// </summary>
        public HouseValueBreakdown BuildBreakdown()
        {
            float cleanlinessBonus = 0f;
            float designBonus = 0f;
            float dirtPenalty = 0f;
            float damagePenalty = 0f;

            foreach (RoomController room in RoomRegistry.All)
            {
                if (room == null)
                {
                    continue;
                }

                cleanlinessBonus += Mathf.Clamp01(room.Cleanliness.Value) * GameConstants.MaxCleanlinessValuePerRoom;
                designBonus += room.DesignPoints.Value * GameConstants.DesignPointValue;
                dirtPenalty += room.RemainingDirt() * GameConstants.DirtPenaltyPerUnit;
                damagePenalty += room.CountStructuralDamage() * GameConstants.StructuralDamagePenalty;
                damagePenalty += room.CountUnrepairedFixtures() * GameConstants.BrokenFixturePenalty;
            }

            return new HouseValueBreakdown
            {
                BaseValue = GameConstants.HouseBaseValue,
                RenovationBonus = _renovationBonus,
                FurnitureBonus = _furnitureBonus,
                DesignBonus = designBonus,
                CleanlinessBonus = cleanlinessBonus,
                DamagePenalty = damagePenalty + dirtPenalty
            };
        }
    }

    /// <summary>Itemised house value, mirroring the inspection screen layout in GDD 19.</summary>
    public struct HouseValueBreakdown : INetworkSerializable
    {
        public float BaseValue;
        public float RenovationBonus;
        public float FurnitureBonus;
        public float DesignBonus;
        public float CleanlinessBonus;
        public float DamagePenalty;

        public float FinalValue => Mathf.Max(
            0f,
            BaseValue + RenovationBonus + FurnitureBonus + DesignBonus + CleanlinessBonus - DamagePenalty);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref BaseValue);
            serializer.SerializeValue(ref RenovationBonus);
            serializer.SerializeValue(ref FurnitureBonus);
            serializer.SerializeValue(ref DesignBonus);
            serializer.SerializeValue(ref CleanlinessBonus);
            serializer.SerializeValue(ref DamagePenalty);
        }
    }
}
