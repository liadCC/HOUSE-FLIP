using System;
using HouseFlip.Economy;
using HouseFlip.GameFlow;

namespace HouseFlip.Core
{
    /// <summary>
    /// Static publish/subscribe bus (GDD 25). Managers raise events here instead of
    /// holding references to one another, so new systems can be bolted on without
    /// touching the existing ones.
    ///
    /// Rule of thumb: gameplay code *raises*, UI and managers *listen*. Nothing in
    /// here is authoritative — the server-owned NetworkVariables are the source of
    /// truth, and these events only announce that they changed.
    /// </summary>
    public static class GameEvents
    {
        // --- Economy ------------------------------------------------------
        public static event Action<float> BudgetChanged;
        public static event Action<float> HouseValueChanged;
        public static event Action<string> PurchaseRejected;
        public static event Action<float> PurchaseCompleted;

        // --- Session ------------------------------------------------------
        public static event Action<float> TimerChanged;
        public static event Action<GameState> GameStateChanged;

        // --- House --------------------------------------------------------
        /// <summary>Anything that could move the house value changed. Server-side only.</summary>
        public static event Action HouseStateDirty;
        public static event Action<RoomController> RoomStateChanged;

        // --- Player stats (GDD 20) ---------------------------------------
        public static event Action<ulong, PlayerStat, float> StatRecorded;

        // --- Random events (GDD 21) --------------------------------------
        public static event Action<string, string> RandomEventStarted;
        public static event Action<string> RandomEventResolved;

        // --- Audio (GDD 24) ----------------------------------------------
        public static event Action<SfxId> SfxRequested;

        public static void RaiseBudgetChanged(float value) => BudgetChanged?.Invoke(value);
        public static void RaiseHouseValueChanged(float value) => HouseValueChanged?.Invoke(value);
        public static void RaisePurchaseRejected(string reason) => PurchaseRejected?.Invoke(reason);
        public static void RaisePurchaseCompleted(float amount) => PurchaseCompleted?.Invoke(amount);

        public static void RaiseTimerChanged(float secondsRemaining) => TimerChanged?.Invoke(secondsRemaining);
        public static void RaiseGameStateChanged(GameState state) => GameStateChanged?.Invoke(state);

        public static void RaiseHouseStateDirty() => HouseStateDirty?.Invoke();
        public static void RaiseRoomStateChanged(RoomController room) => RoomStateChanged?.Invoke(room);

        public static void RaiseStatRecorded(ulong clientId, PlayerStat stat, float amount)
            => StatRecorded?.Invoke(clientId, stat, amount);

        public static void RaiseRandomEventStarted(string eventName, string popupMessage)
            => RandomEventStarted?.Invoke(eventName, popupMessage);

        public static void RaiseRandomEventResolved(string eventName) => RandomEventResolved?.Invoke(eventName);

        public static void RaiseSfx(SfxId id) => SfxRequested?.Invoke(id);

        /// <summary>
        /// Domain reload is disabled in some Unity setups, which would leave stale
        /// subscribers pointing at destroyed objects. Wiping the bus on load keeps
        /// entering play mode deterministic.
        /// </summary>
        public static void ResetAll()
        {
            BudgetChanged = null;
            HouseValueChanged = null;
            PurchaseRejected = null;
            PurchaseCompleted = null;
            TimerChanged = null;
            GameStateChanged = null;
            HouseStateDirty = null;
            RoomStateChanged = null;
            StatRecorded = null;
            RandomEventStarted = null;
            RandomEventResolved = null;
            SfxRequested = null;
        }
    }
}
