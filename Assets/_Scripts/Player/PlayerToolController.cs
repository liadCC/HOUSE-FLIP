using HouseFlip.Core;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Player
{
    /// <summary>
    /// Tool belt (GDD 6). The owner picks the tool; the choice replicates so other
    /// players can see who is holding the hammer and who is holding the mop.
    ///
    /// The server re-reads this value when validating a swing, so a client cannot
    /// claim "I had the wrench" after the fact.
    /// </summary>
    public class PlayerToolController : NetworkBehaviour
    {
        [Tooltip("Optional held models, indexed to match the ToolType enum (0 = None).")]
        [SerializeField] private GameObject[] toolVisuals;

        private readonly NetworkVariable<ToolType> _currentTool = new NetworkVariable<ToolType>(
            ToolType.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> _paintColorIndex = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public ToolType CurrentTool => _currentTool.Value;
        public int PaintColorIndex => _paintColorIndex.Value;

        public override void OnNetworkSpawn()
        {
            _currentTool.OnValueChanged += OnToolChanged;
            RefreshVisuals(_currentTool.Value);
        }

        public override void OnNetworkDespawn()
        {
            _currentTool.OnValueChanged -= OnToolChanged;
        }

        private void Update()
        {
            if (!IsOwner || !PlayerController.InputEnabled)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) Equip(ToolType.Hammer);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) Equip(ToolType.Screwdriver);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) Equip(ToolType.Wrench);
            else if (Input.GetKeyDown(KeyCode.Alpha4)) Equip(ToolType.CleaningTool);
            else if (Input.GetKeyDown(KeyCode.Alpha5)) Equip(ToolType.PaintRoller);
            else if (Input.GetKeyDown(KeyCode.Alpha0)) Equip(ToolType.None);
            else ScrollBelt();
        }

        /// <summary>
        /// Mouse wheel walks the hotbar, as in every game that has one. Scrolling up moves
        /// left along the bar, matching Minecraft — the direction people already have in
        /// their fingers.
        /// </summary>
        private void ScrollBelt()
        {
            float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) < 0.01f)
            {
                return;
            }

            // Never toggles: a scroll is a move to a specific slot, and stopping on the
            // slot you were already holding would put the tool away instead.
            Select(ToolBelt.Cycle(_currentTool.Value, scroll > 0f ? -1 : 1));
        }

        public void Equip(ToolType tool)
        {
            if (!IsOwner)
            {
                return;
            }

            // Toggling the equipped tool puts it away, which is handy for grabbing things.
            _currentTool.Value = _currentTool.Value == tool ? ToolType.None : tool;
        }

        /// <summary>Equips exactly what it is given, with no put-away toggle.</summary>
        public void Select(ToolType tool)
        {
            if (IsOwner)
            {
                _currentTool.Value = tool;
            }
        }

        public void SetPaintColorIndex(int index)
        {
            if (IsOwner)
            {
                _paintColorIndex.Value = Mathf.Max(0, index);
            }
        }

        /// <summary>
        /// Server only. Plays a swing on every peer.
        ///
        /// Called for discrete actions (a hammer blow, a paint stroke) but never for the
        /// hold-to-use jobs — cleaning and repairing tick every frame, and an RPC per
        /// frame per player would cost far more than the animation is worth.
        /// </summary>
        public void ServerNotifyToolUsed()
        {
            if (IsServer)
            {
                PlaySwingClientRpc();
            }
        }

        [ClientRpc]
        private void PlaySwingClientRpc()
        {
            var animator = GetComponent<CharacterAnimator>();
            if (animator != null)
            {
                animator.PlaySwing();
            }
        }

        private void OnToolChanged(ToolType previous, ToolType current) => RefreshVisuals(current);

        private void RefreshVisuals(ToolType tool)
        {
            if (toolVisuals == null)
            {
                return;
            }

            for (int i = 0; i < toolVisuals.Length; i++)
            {
                if (toolVisuals[i] != null)
                {
                    toolVisuals[i].SetActive(i == (int)tool);
                }
            }
        }
    }
}
