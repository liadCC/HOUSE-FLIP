using System.Collections.Generic;
using HouseFlip.Core;
using HouseFlip.GameFlow;
using HouseFlip.Player;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Events
{
    /// <summary>
    /// Picks and fires random events during the round (GDD 21). Host-authoritative:
    /// the server decides what fires and when, then broadcasts the popup.
    /// </summary>
    public class RandomEventManager : NetSingleton<RandomEventManager>
    {
        [SerializeField] private List<RandomEventHandler> handlers = new List<RandomEventHandler>();

        [Header("Scheduling")]
        [SerializeField] private float firstEventDelay = 150f;
        [SerializeField] private Vector2 intervalRange = new Vector2(180f, 300f);

        private bool _scheduling;
        private float _nextEventTime;
        private readonly HashSet<RandomEventHandler> _fired = new HashSet<RandomEventHandler>();

        /// <summary>Server only.</summary>
        public void ServerBeginScheduling()
        {
            if (!IsServer)
            {
                return;
            }

            _fired.Clear();
            _scheduling = true;
            _nextEventTime = firstEventDelay;
        }

        /// <summary>Server only. Resolves anything still running so a round never ends mid-flood.</summary>
        public void ServerStopScheduling()
        {
            if (!IsServer)
            {
                return;
            }

            _scheduling = false;

            foreach (RandomEventHandler handler in handlers)
            {
                if (handler != null && handler.IsActive)
                {
                    handler.ServerResolve(false);
                }
            }
        }

        private void Update()
        {
            if (!IsServer || !_scheduling)
            {
                return;
            }

            TimerManager timer = TimerManager.Instance;
            if (timer == null || !timer.IsRunning)
            {
                return;
            }

            if (timer.Elapsed < _nextEventTime)
            {
                return;
            }

            RandomEventHandler chosen = PickEligible(timer.Elapsed);
            if (chosen != null)
            {
                ServerFire(chosen);
            }

            _nextEventTime = timer.Elapsed + Random.Range(intervalRange.x, intervalRange.y);
        }

        private RandomEventHandler PickEligible(float elapsed)
        {
            var eligible = new List<RandomEventHandler>();
            float totalWeight = 0f;

            foreach (RandomEventHandler handler in handlers)
            {
                if (handler == null || handler.IsActive || handler.Definition == null)
                {
                    continue;
                }

                if (handler.Definition.oncePerRound && _fired.Contains(handler))
                {
                    continue;
                }

                if (!handler.Definition.IsInWindow(elapsed))
                {
                    continue;
                }

                eligible.Add(handler);
                totalWeight += Mathf.Max(0.01f, handler.Definition.weight);
            }

            if (eligible.Count == 0)
            {
                return null;
            }

            float roll = Random.Range(0f, totalWeight);
            foreach (RandomEventHandler handler in eligible)
            {
                roll -= Mathf.Max(0.01f, handler.Definition.weight);
                if (roll <= 0f)
                {
                    return handler;
                }
            }

            return eligible[eligible.Count - 1];
        }

        /// <summary>Server only. Fires a specific event immediately — also used by the debug menu.</summary>
        public void ServerFire(RandomEventHandler handler)
        {
            if (!IsServer || handler == null || handler.IsActive)
            {
                return;
            }

            _fired.Add(handler);
            handler.ServerBegin();

            GameEventDefinition definition = handler.Definition;
            AnnounceStartClientRpc(
                definition != null ? definition.eventName : handler.name,
                definition != null ? definition.popupMessage : "Something happened!");
        }

        /// <summary>Server only. Called by handlers when they finish.</summary>
        public void ServerNotifyResolved(RandomEventHandler handler, bool playersFixedIt)
        {
            if (!IsServer || handler == null)
            {
                return;
            }

            string eventName = handler.Definition != null ? handler.Definition.eventName : handler.name;
            AnnounceResolvedClientRpc(eventName, playersFixedIt);
        }

        [ClientRpc]
        private void AnnounceStartClientRpc(string eventName, string popupMessage)
        {
            GameEvents.RaiseRandomEventStarted(eventName, popupMessage);
            GameEvents.RaiseSfx(SfxId.EventAlarm);
        }

        [ClientRpc]
        private void AnnounceResolvedClientRpc(string eventName, bool playersFixedIt)
        {
            GameEvents.RaiseRandomEventResolved(eventName);
        }

        /// <summary>Registers a handler at runtime (used by the scene builder).</summary>
        public void RegisterHandler(RandomEventHandler handler)
        {
            if (handler != null && !handlers.Contains(handler))
            {
                handlers.Add(handler);
            }
        }

        /// <summary>Server only. Credit the player who resolved an event.</summary>
        public static void CreditResolver(ulong clientId)
        {
            PlayerStatsTracker.Record(clientId, PlayerStat.EventsResolved, 1f);
        }
    }
}
