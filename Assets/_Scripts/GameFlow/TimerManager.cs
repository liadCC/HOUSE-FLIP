using HouseFlip.Core;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.GameFlow
{
    /// <summary>
    /// The 25-minute session clock (GDD 18). The host drives it; clients read the
    /// replicated value. At zero the host calls <see cref="GameManager.TriggerInspection"/>.
    /// </summary>
    public class TimerManager : NetSingleton<TimerManager>
    {
        [SerializeField] private float sessionSeconds = GameConstants.SessionSeconds;

        private readonly NetworkVariable<float> _remaining = new NetworkVariable<float>(
            GameConstants.SessionSeconds,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _running = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private bool _expiredHandled;

        public float Remaining => _remaining.Value;
        public float SessionLength => sessionSeconds;
        public bool IsRunning => _running.Value;

        /// <summary>Seconds since the round started — used by the random event scheduler.</summary>
        public float Elapsed => Mathf.Max(0f, sessionSeconds - _remaining.Value);

        public override void OnNetworkSpawn()
        {
            _remaining.OnValueChanged += OnRemainingChanged;
            GameEvents.RaiseTimerChanged(_remaining.Value);
        }

        public override void OnNetworkDespawn()
        {
            _remaining.OnValueChanged -= OnRemainingChanged;
        }

        private void OnRemainingChanged(float previous, float current) => GameEvents.RaiseTimerChanged(current);

        /// <summary>Server only.</summary>
        public void ServerStart()
        {
            if (!IsServer)
            {
                return;
            }

            _remaining.Value = sessionSeconds;
            _running.Value = true;
            _expiredHandled = false;
        }

        /// <summary>Server only.</summary>
        public void ServerStop()
        {
            if (IsServer)
            {
                _running.Value = false;
            }
        }

        private void Update()
        {
            if (!IsServer || !_running.Value)
            {
                return;
            }

            _remaining.Value = Mathf.Max(0f, _remaining.Value - Time.deltaTime);

            if (_remaining.Value <= 0f && !_expiredHandled)
            {
                _expiredHandled = true;
                _running.Value = false;
                GameManager.Instance?.TriggerInspection();
            }
        }

        public static string Format(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int minutes = Mathf.FloorToInt(seconds / 60f);
            int remainder = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes:00}:{remainder:00}";
        }
    }
}
