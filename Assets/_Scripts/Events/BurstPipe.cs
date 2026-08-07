using System;
using HouseFlip.Core;
using HouseFlip.Economy;
using HouseFlip.Interaction;
using HouseFlip.Networking;
using HouseFlip.Player;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Events
{
    /// <summary>
    /// The pipe that bursts during the WATER LEAK event (GDD 21, Event 1).
    /// One of these sits hidden in each room; the event picks one at random.
    /// </summary>
    public class BurstPipe : NetworkBehaviour, IInteractable, IToolGated
    {
        [SerializeField] private Transform waterVisual;
        [SerializeField] private ParticleSystem sprayEffect;
        [SerializeField] private float maxFloodHeight = 0.6f;
        [SerializeField] private float floodRiseRate = 0.03f;

        private readonly NetworkVariable<bool> _leaking = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _floodLevel = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private RoomController _room;

        /// <summary>Server-side notification for the owning event handler.</summary>
        public event Action<BurstPipe, ulong> Fixed;

        public ToolType RequiredTool => ToolType.Wrench;

        public bool IsLeaking => _leaking.Value;
        public RoomController Room => _room;
        public float FloodLevel => _floodLevel.Value;

        private void Start()
        {
            _room = RoomRegistry.FindRoom(transform.position);
        }

        public override void OnNetworkSpawn()
        {
            _leaking.OnValueChanged += OnLeakingChanged;
            _floodLevel.OnValueChanged += OnFloodChanged;

            ApplyLeakVisuals(_leaking.Value);
            ApplyFloodVisuals(_floodLevel.Value);
        }

        public override void OnNetworkDespawn()
        {
            _leaking.OnValueChanged -= OnLeakingChanged;
            _floodLevel.OnValueChanged -= OnFloodChanged;
        }

        /// <summary>Server only.</summary>
        public void ServerSetLeaking(bool leaking)
        {
            if (!IsServer)
            {
                return;
            }

            _leaking.Value = leaking;

            if (!leaking)
            {
                _floodLevel.Value = 0f;
            }
        }

        private void Update()
        {
            if (!IsServer || !_leaking.Value)
            {
                return;
            }

            _floodLevel.Value = Mathf.Min(maxFloodHeight, _floodLevel.Value + floodRiseRate * Time.deltaTime);

            // Standing water degrades the room the whole time it is there (GDD 21).
            if (_room != null)
            {
                _room.ApplyCleanlinessPenalty(
                    GameConstants.FloodCleanlinessDrainPerSecond * Time.deltaTime);
            }
        }

        public string GetPromptText()
        {
            if (!_leaking.Value)
            {
                return null;
            }

            PlayerController local = PlayerRegistry.LocalPlayer;
            if (local != null && local.Tools != null && local.Tools.CurrentTool != ToolType.Wrench)
            {
                return "Stop Leak (Need Wrench)";
            }

            return "Stop Leak";
        }

        public void OnInteract(PlayerController player)
        {
            if (player == null || !_leaking.Value)
            {
                return;
            }

            if (player.Tools == null || player.Tools.CurrentTool != ToolType.Wrench)
            {
                return;
            }

            FixPipeServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void FixPipeServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;

            if (!_leaking.Value)
            {
                return;
            }

            if (!ActorValidator.Validate(sender, transform.position, ToolType.Wrench, out PlayerController _))
            {
                return;
            }

            ServerSetLeaking(false);
            RandomEventManager.CreditResolver(sender);
            PlayerStatsTracker.Record(sender, PlayerStat.RepairsCompleted, 1f);

            Fixed?.Invoke(this, sender);
        }

        private void OnLeakingChanged(bool previous, bool current) => ApplyLeakVisuals(current);

        private void OnFloodChanged(float previous, float current) => ApplyFloodVisuals(current);

        private void ApplyLeakVisuals(bool leaking)
        {
            if (sprayEffect != null)
            {
                if (leaking && !sprayEffect.isPlaying)
                {
                    sprayEffect.Play();
                }
                else if (!leaking && sprayEffect.isPlaying)
                {
                    sprayEffect.Stop();
                }
            }

            if (leaking)
            {
                GameEvents.RaiseSfx(SfxId.WaterGurgle);
            }
        }

        private void ApplyFloodVisuals(float level)
        {
            if (waterVisual == null)
            {
                return;
            }

            bool visible = level > 0.001f;
            waterVisual.gameObject.SetActive(visible);

            if (visible)
            {
                Vector3 position = waterVisual.localPosition;
                position.y = level * 0.5f;
                waterVisual.localPosition = position;

                Vector3 scale = waterVisual.localScale;
                scale.y = Mathf.Max(0.01f, level);
                waterVisual.localScale = scale;
            }
        }
    }
}
