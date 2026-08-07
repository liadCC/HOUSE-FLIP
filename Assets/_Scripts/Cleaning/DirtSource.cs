using HouseFlip.Core;
using HouseFlip.Economy;
using HouseFlip.Interaction;
using HouseFlip.Networking;
using HouseFlip.Player;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Cleaning
{
    /// <summary>
    /// A stain, dust pile, or trash bag (GDD 12). Cleaning drains its DirtValue, and the
    /// owning room recomputes its CleanlinessLevel from what is left.
    ///
    /// Hold [E] to clean continuously; each tick is a separate server call so two players
    /// scrubbing the same stain simply finish it twice as fast.
    /// </summary>
    public class DirtSource : NetworkBehaviour, IInteractable, IToolGated
    {
        [Header("Dirt")]
        [SerializeField] private float maxDirt = 1f;

        [Tooltip("Dirt removed per second of scrubbing.")]
        [SerializeField] private float cleanRate = 0.6f;

        [Header("Visuals")]
        [SerializeField] private Transform shrinkTarget;
        [SerializeField] private ParticleSystem cleanEffect;

        private readonly NetworkVariable<float> _remaining = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private RoomController _room;
        private Vector3 _originalScale;

        public float MaxDirt => Mathf.Max(0.0001f, maxDirt);
        public float RemainingDirt => _remaining.Value;
        public bool IsClean => _remaining.Value <= 0.0001f;

        public ToolType RequiredTool => ToolType.CleaningTool;

        private void Awake()
        {
            if (shrinkTarget == null)
            {
                shrinkTarget = transform;
            }

            _originalScale = shrinkTarget.localScale;
        }

        private void Start()
        {
            _room = RoomRegistry.FindRoom(transform.position);
            if (_room != null)
            {
                _room.RegisterDirt(this);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _remaining.Value = MaxDirt;
            }

            _remaining.OnValueChanged += OnRemainingChanged;
            RefreshVisuals(_remaining.Value);
        }

        public override void OnNetworkDespawn()
        {
            _remaining.OnValueChanged -= OnRemainingChanged;
        }

        public string GetPromptText()
        {
            if (IsClean)
            {
                return null;
            }

            PlayerController local = PlayerRegistry.LocalPlayer;
            if (local != null && local.Tools != null && local.Tools.CurrentTool != ToolType.CleaningTool)
            {
                return "Clean (Need Vacuum)";
            }

            return "Hold to Clean";
        }

        public void OnInteract(PlayerController player)
        {
            // A single tap still does a little work; holding [E] is handled in Update below.
            TryClean(player, Time.deltaTime);
        }

        private void Update()
        {
            // Continuous cleaning while the key is held, driven by the local player only.
            PlayerController local = PlayerRegistry.LocalPlayer;
            if (local == null || !local.IsOwner || !PlayerController.InputEnabled || IsClean)
            {
                return;
            }

            if (!Input.GetKey(KeyCode.E))
            {
                return;
            }

            var interactor = local.GetComponent<Interactor>();
            if (interactor == null || !ReferenceEquals(interactor.Current, this))
            {
                return;
            }

            TryClean(local, Time.deltaTime);
        }

        private void TryClean(PlayerController player, float deltaTime)
        {
            if (player == null || IsClean)
            {
                return;
            }

            if (player.Tools == null || player.Tools.CurrentTool != ToolType.CleaningTool)
            {
                return;
            }

            RenovationService.Instance?.RequestClean(this, cleanRate * deltaTime);
        }

        /// <summary>Server only.</summary>
        public void ServerClean(ulong cleanerClientId, float amount)
        {
            if (!IsServer || _remaining.Value <= 0f || amount <= 0f)
            {
                return;
            }

            float actual = Mathf.Min(_remaining.Value, amount);
            _remaining.Value -= actual;

            PlayerStatsTracker.Record(cleanerClientId, PlayerStat.DirtCleaned, actual);

            if (_room != null)
            {
                _room.RecomputeCleanliness();
            }

            if (_remaining.Value <= 0.0001f)
            {
                CleanedClientRpc();
            }
        }

        /// <summary>Server only. Used by the water-leak event to make a room filthy again.</summary>
        public void ServerAddDirt(float amount)
        {
            if (!IsServer || amount <= 0f)
            {
                return;
            }

            _remaining.Value = Mathf.Min(MaxDirt, _remaining.Value + amount);

            if (_room != null)
            {
                _room.RecomputeCleanliness();
            }
        }

        private void OnRemainingChanged(float previous, float current) => RefreshVisuals(current);

        private void RefreshVisuals(float remaining)
        {
            float ratio = Mathf.Clamp01(remaining / MaxDirt);

            if (shrinkTarget != null)
            {
                shrinkTarget.localScale = _originalScale * Mathf.Lerp(0.05f, 1f, ratio);
                shrinkTarget.gameObject.SetActive(ratio > 0.001f);
            }
        }

        [ClientRpc]
        private void CleanedClientRpc()
        {
            GameEvents.RaiseSfx(SfxId.Clean);

            if (cleanEffect != null)
            {
                cleanEffect.Play();
            }
        }
    }
}
