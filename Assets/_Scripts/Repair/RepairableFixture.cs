using HouseFlip.Core;
using HouseFlip.Economy;
using HouseFlip.Interaction;
using HouseFlip.Networking;
using HouseFlip.Player;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Repair
{
    /// <summary>
    /// A broken fixture that must be fixed with the right tool (GDD 13).
    /// Costs money, adds house value, and clears the room's Condition penalty.
    /// </summary>
    public class RepairableFixture : NetworkBehaviour, IInteractable, IToolGated
    {
        [Header("Repair")]
        [SerializeField] private string fixtureName = "Fixture";
        [SerializeField] private ToolType requiredTool = ToolType.Wrench;
        [SerializeField] private float repairCost = 200f;
        [SerializeField] private float houseValueBonus = 900f;

        [Tooltip("Seconds of held [E] needed before the repair completes.")]
        [SerializeField] private float repairDuration = 1.2f;

        [Header("Visual states")]
        [SerializeField] private GameObject brokenVisual;
        [SerializeField] private GameObject fixedVisual;
        [SerializeField] private ParticleSystem brokenEffect;

        private readonly NetworkVariable<bool> _repaired = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _progress = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private RoomController _room;

        public bool IsRepaired => _repaired.Value;

        /// <summary>Doubles as the <see cref="IToolGated"/> hint, so [E] resolves to this fixture
        /// when the right tool is out and to whatever else shares the object when it is not.</summary>
        public ToolType RequiredTool => requiredTool;
        public float RepairCost => repairCost;
        public float HouseValueBonus => houseValueBonus;
        public string FixtureName => fixtureName;

        private void Start()
        {
            _room = RoomRegistry.FindRoom(transform.position);
            if (_room != null)
            {
                _room.RegisterFixture(this);
            }
        }

        public override void OnNetworkSpawn()
        {
            _repaired.OnValueChanged += OnRepairedChanged;
            RefreshVisuals(_repaired.Value);

            if (IsServer)
            {
                GameEvents.RaiseHouseStateDirty();
            }
        }

        public override void OnNetworkDespawn()
        {
            _repaired.OnValueChanged -= OnRepairedChanged;
        }

        public string GetPromptText()
        {
            if (_repaired.Value)
            {
                return null;
            }

            PlayerController local = PlayerRegistry.LocalPlayer;
            if (local != null && local.Tools != null && local.Tools.CurrentTool != requiredTool)
            {
                return $"Fix {fixtureName} (Need {requiredTool.DisplayName()})";
            }

            if (_progress.Value > 0.01f)
            {
                return $"Fixing… {(_progress.Value / Mathf.Max(0.01f, repairDuration)) * 100f:0}%";
            }

            return $"Fix {fixtureName} (${repairCost:0})";
        }

        public void OnInteract(PlayerController player) => TryRepair(player, Time.deltaTime);

        private void Update()
        {
            PlayerController local = PlayerRegistry.LocalPlayer;
            if (local == null || !local.IsOwner || !PlayerController.InputEnabled || _repaired.Value)
            {
                return;
            }

            if (!Input.GetKey(KeyCode.E))
            {
                return;
            }

            var interactor = local.GetComponent<Interactor>();
            if (interactor == null || !ReferenceEquals(interactor.Current, this))
            {
                return;
            }

            TryRepair(local, Time.deltaTime);
        }

        private void TryRepair(PlayerController player, float deltaTime)
        {
            if (player == null || _repaired.Value)
            {
                return;
            }

            if (player.Tools == null || player.Tools.CurrentTool != requiredTool)
            {
                return;
            }

            RenovationService.Instance?.RequestRepair(this, deltaTime);
        }

        /// <summary>Server only. Advances the repair; charges the budget on completion.</summary>
        public void ServerAdvanceRepair(ulong repairerClientId, float deltaTime)
        {
            if (!IsServer || _repaired.Value)
            {
                return;
            }

            _progress.Value += Mathf.Max(0f, deltaTime);
            if (_progress.Value < repairDuration)
            {
                return;
            }

            // Charge only at the moment of completion — half-finished work is free.
            BudgetManager budget = BudgetManager.Instance;
            if (budget != null && !budget.TrySpend(repairCost, repairerClientId, SpendCategory.Renovation))
            {
                _progress.Value = repairDuration; // Hold at full so it completes the instant funds arrive.
                return;
            }

            _repaired.Value = true;
            _progress.Value = 0f;

            HouseValueManager.Instance?.OnRepairComplete(houseValueBonus);
            PlayerStatsTracker.Record(repairerClientId, PlayerStat.RepairsCompleted, 1f);
            PlayerStatsTracker.Record(repairerClientId, PlayerStat.HouseValueAdded, houseValueBonus);

            GameEvents.RaiseHouseStateDirty();
            RepairedClientRpc();
        }

        /// <summary>Server only. Used by random events to break something mid-round.</summary>
        public void ServerBreak()
        {
            if (!IsServer)
            {
                return;
            }

            _repaired.Value = false;
            _progress.Value = 0f;
            GameEvents.RaiseHouseStateDirty();
        }

        private void OnRepairedChanged(bool previous, bool current) => RefreshVisuals(current);

        private void RefreshVisuals(bool repaired)
        {
            if (brokenVisual != null) brokenVisual.SetActive(!repaired);
            if (fixedVisual != null) fixedVisual.SetActive(repaired);

            if (brokenEffect != null)
            {
                if (repaired && brokenEffect.isPlaying)
                {
                    brokenEffect.Stop();
                }
                else if (!repaired && !brokenEffect.isPlaying)
                {
                    brokenEffect.Play();
                }
            }
        }

        [ClientRpc]
        private void RepairedClientRpc() => GameEvents.RaiseSfx(SfxId.Repair);
    }
}
