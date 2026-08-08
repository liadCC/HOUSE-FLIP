using HouseFlip.Core;
using HouseFlip.Economy;
using HouseFlip.Events;
using HouseFlip.Furniture;
using HouseFlip.Player;
using HouseFlip.UI;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.GameFlow
{
    /// <summary>
    /// Session state machine driving the core loop in GDD 3.
    ///
    /// Lobby → Renovating → Inspection → Results → Lobby.
    /// The host owns the state; clients react to the replicated value.
    /// </summary>
    public class GameManager : NetSingleton<GameManager>
    {
        [SerializeField] private float housePurchasePrice = GameConstants.HousePurchasePrice;

        private readonly NetworkVariable<GameState> _state = new NetworkVariable<GameState>(
            GameState.Lobby, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public GameState State => _state.Value;
        public float HousePurchasePrice => housePurchasePrice;

        /// <summary>Latest report, populated on every client when the inspection screen opens.</summary>
        public InspectionReport LastReport { get; private set; }
        public RoomScore[] LastRoomScores { get; private set; } = new RoomScore[0];
        public PlayerAward[] LastAwards { get; private set; } = new PlayerAward[0];

        public override void OnNetworkSpawn()
        {
            _state.OnValueChanged += OnStateChanged;
            GameEvents.RaiseGameStateChanged(_state.Value);
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            GameEvents.RaiseGameStateChanged(current);
        }

        // ==================================================================
        // Round lifecycle
        // ==================================================================

        /// <summary>Server only. Buys the house and starts the clock (GDD 3).</summary>
        public void StartRound()
        {
            if (!IsServer || _state.Value != GameState.Lobby)
            {
                // Restarting from Renovating would rewind the budget and the clock while the
                // players were still working, so the entry point is gated on the phase itself
                // rather than trusting every caller to check.
                return;
            }

            PlayerController.ResetInputGate();

            ServerClearHouse();

            BudgetManager.Instance?.ServerResetForNewRound();
            HouseValueManager.Instance?.ServerResetForNewRound();

            foreach (var entry in PlayerRegistry.All)
            {
                entry.Value?.Stats?.ResetStats();
            }

            RandomEventManager.Instance?.ServerBeginScheduling();
            TimerManager.Instance?.ServerStart();

            _state.Value = GameState.Renovating;
        }

        /// <summary>
        /// Server only. Strips out everything the previous round bought or fixed.
        ///
        /// The scene is never reloaded between rounds, so every room, dirt pile, fixture and
        /// placed item survives into the next one. Resetting only the budget and the house
        /// value (as this used to) hands the team a full wallet on top of an already finished
        /// house: round two opens clean, furnished and repaired, and sells for a large profit
        /// nobody earned. GDD 27 requires the second round to be a real round.
        /// </summary>
        private static void ServerClearHouse()
        {
            // Despawn first: PlacedFurniture backs its contribution out of the room as it
            // despawns, so doing this before the room reset avoids leaving a stale count.
            foreach (PlacedFurniture placed in FindObjectsByType<PlacedFurniture>(FindObjectsSortMode.None))
            {
                if (placed != null && placed.NetworkObject != null && placed.NetworkObject.IsSpawned)
                {
                    placed.NetworkObject.Despawn(true);
                }
            }

            foreach (RoomController room in RoomRegistry.All)
            {
                room?.ServerResetForNewRound();
            }
        }

        /// <summary>
        /// Server only. Called by <see cref="TimerManager"/> at 00:00 (GDD 18), or early
        /// if the players decide they are done.
        /// </summary>
        public void TriggerInspection()
        {
            if (!IsServer || _state.Value != GameState.Renovating)
            {
                return;
            }

            TimerManager.Instance?.ServerStop();
            RandomEventManager.Instance?.ServerStopScheduling();

            _state.Value = GameState.Inspection;

            InspectionReport report = BuildReport(out RoomScore[] roomScores);
            ShowInspectionClientRpc(report, roomScores);
        }

        /// <summary>Server only. Sells the house and hands out awards (GDD 19, 20).</summary>
        public void SellHouse()
        {
            if (!IsServer || _state.Value != GameState.Inspection)
            {
                return;
            }

            InspectionReport report = BuildReport(out _);
            PlayerAward[] awards = AwardsCalculator.Build(PlayerRegistry.All, !report.IsProfitable);

            AwardsCalculator.LogAwards(awards);

            _state.Value = GameState.Results;
            ShowResultsClientRpc(report, awards);
        }

        /// <summary>Server only. Back to the lobby, ready for a new house.</summary>
        public void ReturnToLobby()
        {
            if (!IsServer)
            {
                return;
            }

            _state.Value = GameState.Lobby;
            PlayerController.ResetInputGate();
        }

        /// <summary>Any client may click through the end-of-round screens.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestAdvanceServerRpc()
        {
            switch (_state.Value)
            {
                case GameState.Inspection:
                    SellHouse();
                    break;
                case GameState.Results:
                    ReturnToLobby();
                    break;

                // Deliberately no Lobby case. Every client shows a Continue button on the
                // results screen and the button is hidden by a NetworkVariable delta that is
                // not ordered against these RPCs, so two players clicking it together sends a
                // second request that arrives once the state is already Lobby. Starting the
                // round here would skip the lobby and the ready gate entirely, dropping
                // everyone straight into round two. Starting a round is LobbyManager's job.
            }
        }

        /// <summary>Client-side helper so UI buttons do not need to know about RPCs.</summary>
        public void RequestAdvance()
        {
            if (IsSpawned)
            {
                RequestAdvanceServerRpc();
            }
        }

        // ==================================================================
        // Report building
        // ==================================================================

        private InspectionReport BuildReport(out RoomScore[] roomScores)
        {
            HouseValueManager houseValue = HouseValueManager.Instance;
            BudgetManager budget = BudgetManager.Instance;

            if (houseValue != null)
            {
                houseValue.Recalculate();
            }

            roomScores = BuildRoomScores(out float weightedAverage);

            var report = new InspectionReport
            {
                Value = houseValue != null ? houseValue.BuildBreakdown() : default,
                HousePurchasePrice = housePurchasePrice,
                RenovationSpent = budget != null ? budget.SpentOnRenovation : 0f,
                FurnitureSpent = budget != null ? budget.SpentOnFurniture : 0f,
                FinalHouseScore = weightedAverage
            };

            return report;
        }

        private static RoomScore[] BuildRoomScores(out float weightedAverage)
        {
            var scores = new System.Collections.Generic.List<RoomScore>();
            float totalWeight = 0f;
            float weightedSum = 0f;

            foreach (RoomController room in RoomRegistry.All)
            {
                if (room == null)
                {
                    continue;
                }

                RoomScore score = room.BuildScore();
                scores.Add(score);

                weightedSum += score.Total * room.ScoreWeight;
                totalWeight += room.ScoreWeight;
            }

            weightedAverage = totalWeight > 0f ? weightedSum / totalWeight : 0f;
            return scores.ToArray();
        }

        // ==================================================================
        // Client presentation
        // ==================================================================

        [ClientRpc]
        private void ShowInspectionClientRpc(InspectionReport report, RoomScore[] roomScores)
        {
            LastReport = report;
            LastRoomScores = roomScores ?? new RoomScore[0];

            PlayerCameraRig.SetCursorLocked(false);
            InspectionScreenUI.Show(report, LastRoomScores);
        }

        [ClientRpc]
        private void ShowResultsClientRpc(InspectionReport report, PlayerAward[] awards)
        {
            LastReport = report;
            LastAwards = awards ?? new PlayerAward[0];

            GameEvents.RaiseSfx(report.IsProfitable ? SfxId.SuccessFanfare : SfxId.FailSound);

            PlayerCameraRig.SetCursorLocked(false);
            AwardsScreenUI.Show(report, LastAwards);
        }
    }
}
