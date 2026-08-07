using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Events
{
    /// <summary>
    /// Base class for a random event's behaviour (GDD 21).
    ///
    /// The scheduler only ever calls <see cref="ServerBegin"/> and <see cref="ServerResolve"/>,
    /// so a new event needs no scheduler changes — which is the point of the split.
    /// </summary>
    public abstract class RandomEventHandler : NetworkBehaviour
    {
        [SerializeField] protected GameEventDefinition definition;

        private readonly NetworkVariable<bool> _active = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public GameEventDefinition Definition => definition;
        public bool IsActive => _active.Value;

        public override void OnNetworkSpawn()
        {
            _active.OnValueChanged += OnActiveChanged;
            ApplyActiveState(_active.Value);
        }

        public override void OnNetworkDespawn()
        {
            _active.OnValueChanged -= OnActiveChanged;
        }

        private void OnActiveChanged(bool previous, bool current) => ApplyActiveState(current);

        /// <summary>Server only. Kicks the event off.</summary>
        public void ServerBegin()
        {
            if (!IsServer || _active.Value)
            {
                return;
            }

            _active.Value = true;
            OnServerBegin();

            if (definition != null)
            {
                definition.onEventStart?.Invoke();
            }
        }

        /// <summary>Server only. Ends the event, either because players fixed it or the round did.</summary>
        public void ServerResolve(bool playersFixedIt, ulong resolverClientId = ulong.MaxValue)
        {
            if (!IsServer || !_active.Value)
            {
                return;
            }

            _active.Value = false;
            OnServerResolve(playersFixedIt, resolverClientId);

            if (definition != null)
            {
                definition.onEventResolved?.Invoke();
            }

            RandomEventManager.Instance?.ServerNotifyResolved(this, playersFixedIt);
        }

        /// <summary>Server-side setup for the event.</summary>
        protected abstract void OnServerBegin();

        /// <summary>Server-side teardown. <paramref name="playersFixedIt"/> is false on a timeout.</summary>
        protected abstract void OnServerResolve(bool playersFixedIt, ulong resolverClientId);

        /// <summary>Presentation, run on every peer whenever the active flag flips.</summary>
        protected abstract void ApplyActiveState(bool active);
    }
}
