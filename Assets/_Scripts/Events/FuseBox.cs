using System;
using HouseFlip.Core;
using HouseFlip.Interaction;
using HouseFlip.Networking;
using HouseFlip.Player;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Events
{
    /// <summary>
    /// The fuse box players must find during the POWER OUTAGE event (GDD 21, Event 2).
    /// </summary>
    public class FuseBox : NetworkBehaviour, IInteractable, IToolGated
    {
        [SerializeField] private ParticleSystem sparkEffect;
        [SerializeField] private Renderer indicatorRenderer;
        [SerializeField] private Color okColor = new Color(0.35f, 0.9f, 0.4f);
        [SerializeField] private Color trippedColor = new Color(0.95f, 0.3f, 0.25f);

        private readonly NetworkVariable<bool> _tripped = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private MaterialPropertyBlock _block;

        /// <summary>Server-side notification for the owning event handler.</summary>
        public event Action<FuseBox, ulong> Restored;

        public ToolType RequiredTool => ToolType.Screwdriver;

        public bool IsTripped => _tripped.Value;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        public override void OnNetworkSpawn()
        {
            _tripped.OnValueChanged += OnTrippedChanged;
            ApplyVisuals(_tripped.Value);
        }

        public override void OnNetworkDespawn()
        {
            _tripped.OnValueChanged -= OnTrippedChanged;
        }

        /// <summary>Server only.</summary>
        public void ServerSetTripped(bool tripped)
        {
            if (IsServer)
            {
                _tripped.Value = tripped;
            }
        }

        public string GetPromptText()
        {
            if (!_tripped.Value)
            {
                return null;
            }

            PlayerController local = PlayerRegistry.LocalPlayer;
            if (local != null && local.Tools != null && local.Tools.CurrentTool != ToolType.Screwdriver)
            {
                return "Restore Power (Need Screwdriver)";
            }

            return "Restore Power";
        }

        public void OnInteract(PlayerController player)
        {
            if (player == null || !_tripped.Value)
            {
                return;
            }

            if (player.Tools == null || player.Tools.CurrentTool != ToolType.Screwdriver)
            {
                return;
            }

            RestorePowerServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RestorePowerServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;

            if (!_tripped.Value)
            {
                return;
            }

            if (!ActorValidator.Validate(sender, transform.position, ToolType.Screwdriver, out PlayerController _))
            {
                return;
            }

            ServerSetTripped(false);
            RandomEventManager.CreditResolver(sender);

            Restored?.Invoke(this, sender);
        }

        private void OnTrippedChanged(bool previous, bool current) => ApplyVisuals(current);

        private void ApplyVisuals(bool tripped)
        {
            if (indicatorRenderer != null)
            {
                Color color = tripped ? trippedColor : okColor;
                indicatorRenderer.GetPropertyBlock(_block);
                _block.SetColor("_Color", color);
                _block.SetColor("_BaseColor", color);
                _block.SetColor("_EmissionColor", color);
                indicatorRenderer.SetPropertyBlock(_block);
            }

            if (tripped)
            {
                GameEvents.RaiseSfx(SfxId.ElectricalSpark);

                // The lights going out should land as a jolt, wherever you are standing.
                Polish.ImpactFeedback.ShakeLocal(0.4f);
            }

            if (sparkEffect != null)
            {
                if (tripped && !sparkEffect.isPlaying)
                {
                    sparkEffect.Play();
                }
                else if (!tripped && sparkEffect.isPlaying)
                {
                    sparkEffect.Stop();
                }
            }
        }
    }
}
