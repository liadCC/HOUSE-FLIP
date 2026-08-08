using HouseFlip.Core;
using HouseFlip.GameFlow;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Player
{
    /// <summary>
    /// Third-person character controller (GDD 6). The owning client drives movement and
    /// a ClientNetworkTransform replicates the result, so the local player never feels
    /// rubber-banding. Everything that touches shared state goes through RPCs instead.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 7.5f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float rotationSpeed = 14f;
        [SerializeField] private float jumpHeight = 1.35f;
        [SerializeField] private float gravity = -22f;

        [Header("Carrying")]
        [Tooltip("Speed multiplier applied while hauling something heavy around.")]
        [SerializeField] private float carryHeavySpeedFactor = 0.55f;

        [Header("Identity")]
        [SerializeField] private Renderer[] tintedRenderers;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private Transform _cameraTransform;

        /// <summary>Player colour, assigned by the server on spawn so HUD and body agree.</summary>
        public readonly NetworkVariable<Color> PlayerColor = new NetworkVariable<Color>(
            Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public PlayerCarry Carry { get; private set; }
        public PlayerToolController Tools { get; private set; }
        public PlayerStatsTracker Stats { get; private set; }

        public bool IsSprinting { get; private set; }
        public bool IsGrounded => _controller != null && _controller.isGrounded;

        /// <summary>Set false during the inspection/results phases (GDD 18).</summary>
        public static bool InputEnabled { get; private set; } = true;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            Carry = GetComponent<PlayerCarry>();
            Tools = GetComponent<PlayerToolController>();
            Stats = GetComponent<PlayerStatsTracker>();
        }

        public override void OnNetworkSpawn()
        {
            PlayerColor.OnValueChanged += OnColorChanged;
            ApplyColor(PlayerColor.Value);

            if (IsOwner)
            {
                PlayerRegistry.LocalPlayer = this;
                GameEvents.GameStateChanged += OnGameStateChanged;

                var rig = FindFirstObjectByType<PlayerCameraRig>();
                if (rig != null)
                {
                    rig.SetTarget(transform);
                    _cameraTransform = rig.CameraTransform;
                }
            }

            PlayerRegistry.Register(this);
        }

        public override void OnNetworkDespawn()
        {
            PlayerColor.OnValueChanged -= OnColorChanged;
            PlayerRegistry.Unregister(this);

            if (IsOwner)
            {
                GameEvents.GameStateChanged -= OnGameStateChanged;
                if (PlayerRegistry.LocalPlayer == this)
                {
                    PlayerRegistry.LocalPlayer = null;
                }
            }
        }

        private void OnGameStateChanged(GameState state)
        {
            // Freeze everyone the moment the timer hits zero (GDD 18).
            InputEnabled = state == GameState.Renovating;
        }

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            if (!InputEnabled)
            {
                _horizontalVelocity = Vector3.zero;
                ApplyGravityOnly();
                return;
            }

            ReadMovement();
        }

        private void ReadMovement()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 input = new Vector3(h, 0f, v);
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            // Move relative to where the camera is looking, flattened onto the floor.
            Vector3 desiredDirection = Vector3.zero;
            if (input.sqrMagnitude > 0.0001f)
            {
                Vector3 forward = _cameraTransform != null ? _cameraTransform.forward : Vector3.forward;
                Vector3 right = _cameraTransform != null ? _cameraTransform.right : Vector3.right;

                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();

                desiredDirection = forward * input.z + right * input.x;
            }

            IsSprinting = Input.GetKey(KeyCode.LeftShift) && desiredDirection.sqrMagnitude > 0.01f;

            float targetSpeed = IsSprinting ? sprintSpeed : walkSpeed;
            if (Carry != null && Carry.IsCarryingHeavy)
            {
                targetSpeed *= carryHeavySpeedFactor;
            }

            Vector3 targetVelocity = desiredDirection * targetSpeed;
            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity, targetVelocity, acceleration * Time.deltaTime);

            if (desiredDirection.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(desiredDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, look, 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
            }

            if (_controller.isGrounded)
            {
                // Small downward bias keeps isGrounded stable on slopes and seams.
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }

                if (Input.GetKeyDown(KeyCode.Space))
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }

            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = _horizontalVelocity;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }

        private void ApplyGravityOnly()
        {
            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }

            _verticalVelocity += gravity * Time.deltaTime;
            _controller.Move(new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);
        }

        private void OnColorChanged(Color previous, Color current) => ApplyColor(current);

        private void ApplyColor(Color color)
        {
            if (tintedRenderers == null)
            {
                return;
            }

            foreach (Renderer renderer in tintedRenderers)
            {
                if (renderer != null)
                {
                    // MaterialPropertyBlock avoids instancing a material per player.
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_Color", color);
                    block.SetColor("_BaseColor", color);
                    renderer.SetPropertyBlock(block);
                }
            }
        }

        /// <summary>Called by the server when a session begins so a stale freeze cannot persist.</summary>
        public static void ResetInputGate() => InputEnabled = true;

        /// <summary>
        /// Server-requested reposition (spawn points, and anything that needs to move a
        /// player later).
        ///
        /// This has to be an RPC rather than the server writing the transform directly:
        /// players are owner-authoritative (GDD 22), so a server-side move would be
        /// overwritten by the owner's very next transform update. Only the owner can
        /// actually move itself, so we ask it to.
        /// </summary>
        [ClientRpc]
        public void TeleportClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            ApplyTeleport(position, rotation);
        }

        private void ApplyTeleport(Vector3 position, Quaternion rotation)
        {
            // CharacterController owns the transform while enabled and will silently
            // discard a direct write, so it has to be switched off for the assignment.
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;

            transform.SetPositionAndRotation(position, rotation);

            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;

            _controller.enabled = wasEnabled;
        }
    }
}
