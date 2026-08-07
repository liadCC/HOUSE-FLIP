using HouseFlip.Core;
using UnityEngine;

namespace HouseFlip.Events
{
    /// <summary>
    /// EVENT 3 — INSPECTION WARNING (GDD 21).
    ///
    /// Pops a warning, leaves the session timer alone, and three minutes later sends an
    /// inspector walking through the house handing out small score modifiers. The event
    /// resolves itself once the tour finishes — there is nothing for players to fix, only
    /// pressure to tidy up before he arrives.
    /// </summary>
    public class InspectionWarningEvent : RandomEventHandler
    {
        [SerializeField] private HouseInspectorNPC inspector;
        [SerializeField] private float leadTimeSeconds = GameConstants.InspectorWarningLeadSeconds;

        private float _countdown;
        private bool _tourStarted;

        private void Awake()
        {
            if (inspector == null)
            {
                inspector = FindFirstObjectByType<HouseInspectorNPC>();
            }
        }

        protected override void OnServerBegin()
        {
            _countdown = leadTimeSeconds;
            _tourStarted = false;
        }

        protected override void OnServerResolve(bool playersFixedIt, ulong resolverClientId)
        {
            if (inspector != null)
            {
                inspector.ServerEndTour();
            }

            _tourStarted = false;
        }

        protected override void ApplyActiveState(bool active)
        {
            // The popup is broadcast by RandomEventManager; nothing extra to show here.
        }

        private void Update()
        {
            if (!IsServer || !IsActive)
            {
                return;
            }

            if (!_tourStarted)
            {
                _countdown -= Time.deltaTime;
                if (_countdown > 0f)
                {
                    return;
                }

                _tourStarted = true;

                if (inspector == null)
                {
                    ServerResolve(false);
                    return;
                }

                inspector.ServerBeginTour();
                AnnounceArrivalClientRpc();
                return;
            }

            // Tour over — close the event out so it stops occupying the active slot.
            if (inspector == null || !inspector.IsWalking)
            {
                ServerResolve(true);
            }
        }

        [Unity.Netcode.ClientRpc]
        private void AnnounceArrivalClientRpc()
        {
            GameEvents.RaiseRandomEventStarted("INSPECTOR ARRIVING", "🚪 The inspector is here!");
        }

        public void SetInspector(HouseInspectorNPC npc) => inspector = npc;
    }
}
