using System.Collections.Generic;
using HouseFlip.Core;
using HouseFlip.Economy;
using UnityEngine;

namespace HouseFlip.Events
{
    /// <summary>
    /// EVENT 1 — WATER LEAK (GDD 21).
    ///
    /// A pipe bursts in a random room, the room floods, and players have to find it and
    /// fix it with a wrench. Left running to the end of the round, the room takes a
    /// permanent cleanliness hit on top of the damage the standing water already did.
    /// </summary>
    public class WaterLeakEvent : RandomEventHandler
    {
        [SerializeField] private List<BurstPipe> pipes = new List<BurstPipe>();

        private BurstPipe _activePipe;

        private void Awake()
        {
            if (pipes.Count == 0)
            {
                pipes.AddRange(FindObjectsByType<BurstPipe>(FindObjectsSortMode.None));
            }
        }

        protected override void OnServerBegin()
        {
            _activePipe = PickPipe();
            if (_activePipe == null)
            {
                Debug.LogWarning("[WaterLeakEvent] No BurstPipe in the scene — event cannot run.");
                return;
            }

            _activePipe.Fixed += OnPipeFixed;
            _activePipe.ServerSetLeaking(true);
        }

        private BurstPipe PickPipe()
        {
            var candidates = new List<BurstPipe>();
            foreach (BurstPipe pipe in pipes)
            {
                if (pipe != null && !pipe.IsLeaking)
                {
                    candidates.Add(pipe);
                }
            }

            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }

        private void OnPipeFixed(BurstPipe pipe, ulong resolverClientId)
        {
            ServerResolve(true, resolverClientId);
        }

        protected override void OnServerResolve(bool playersFixedIt, ulong resolverClientId)
        {
            if (_activePipe == null)
            {
                return;
            }

            _activePipe.Fixed -= OnPipeFixed;

            if (!playersFixedIt)
            {
                // Nobody dealt with it: the room is left water-damaged for good (GDD 21).
                RoomController room = _activePipe.Room;
                room?.ApplyCleanlinessPenalty(GameConstants.UnresolvedLeakCleanlinessPenalty);
            }

            _activePipe.ServerSetLeaking(false);
            _activePipe = null;

            GameEvents.RaiseHouseStateDirty();
        }

        protected override void ApplyActiveState(bool active)
        {
            // Presentation lives on the pipe itself; nothing global to toggle here.
        }

        /// <summary>Lets the scene builder wire pipes without touching the inspector.</summary>
        public void RegisterPipe(BurstPipe pipe)
        {
            if (pipe != null && !pipes.Contains(pipe))
            {
                pipes.Add(pipe);
            }
        }
    }
}
