using HouseFlip.Furniture;
using HouseFlip.Interaction;
using HouseFlip.Networking;
using HouseFlip.PhysicsGrab;
using HouseFlip.UI;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Player
{
    /// <summary>
    /// The player's half of the carry contract (GDD 8). Holds no authority: it asks the
    /// server to attach/detach and mirrors the answer for local UI.
    /// </summary>
    public class PlayerCarry : NetworkBehaviour
    {
        [SerializeField] private KeyCode dropKey = KeyCode.G;
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        [Tooltip("Sells the carried item back for part of its price.")]
        [SerializeField] private KeyCode sellKey = KeyCode.X;

        /// <summary>NetworkObjectId of the held object, or 0 for empty hands.</summary>
        private readonly NetworkVariable<ulong> _heldObjectId = new NetworkVariable<ulong>(
            0UL, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Interactor _interactor;
        private Camera _camera;

        public bool IsCarrying => _heldObjectId.Value != 0UL;

        public Grabbable Held => ResolveGrabbable(_heldObjectId.Value);

        public bool IsCarryingHeavy
        {
            get
            {
                Grabbable held = Held;
                return held != null && held.Category == MassCategory.Heavy;
            }
        }

        private void Awake()
        {
            _interactor = GetComponent<Interactor>();
        }

        public bool IsHolding(GameObject candidate)
        {
            if (candidate == null || _heldObjectId.Value == 0UL)
            {
                return false;
            }

            Grabbable held = Held;
            return held != null && (held.gameObject == candidate || candidate.transform.IsChildOf(held.transform));
        }

        private void Update()
        {
            if (!IsOwner || !PlayerController.InputEnabled)
            {
                return;
            }

            if (!IsCarrying)
            {
                return;
            }

            ShowCarryHint();

            if (Input.GetKeyDown(sellKey))
            {
                TrySellHeld();
            }
            else if (Input.GetButtonDown("Fire1"))
            {
                ReleaseServerRpc(true, AimDirection());
            }
            else if (Input.GetKeyDown(dropKey))
            {
                ReleaseServerRpc(false, AimDirection());
            }
            else if (Input.GetKeyDown(interactKey) && (_interactor == null || _interactor.Current == null))
            {
                // [E] with nothing else in front of us falls back to putting the object down.
                ReleaseServerRpc(false, AimDirection());
            }
        }

        private void ShowCarryHint()
        {
            Grabbable held = Held;
            if (held == null)
            {
                return;
            }

            // A lone player on a heavy object gets the GDD 8 message instead of silence.
            if (!held.IsFullyCrewed)
            {
                InteractionPromptUI.SetOverride("Too Heavy — Need Help");
                return;
            }

            var placed = held.GetComponent<PlacedFurniture>();
            InteractionPromptUI.SetOverride(placed != null && placed.RefundValue > 0f
                ? $"[LMB] Throw   [G] Drop   [X] Sell Back +${placed.RefundValue:N0}"
                : "[LMB] Throw   [G] Drop");
        }

        private void TrySellHeld()
        {
            Grabbable held = Held;
            if (held == null)
            {
                return;
            }

            var placed = held.GetComponent<PlacedFurniture>();
            if (placed == null)
            {
                return;
            }

            // Let go first: the server despawns the object, and a carrier pointing at a
            // despawned NetworkObject is how you get a null reference next frame.
            ReleaseServerRpc(false, AimDirection());
            RenovationService.Instance?.RequestSellFurniture(placed);
        }

        private Vector3 AimDirection()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            return _camera != null ? _camera.transform.forward : transform.forward;
        }

        public void RequestGrab(Grabbable grabbable)
        {
            if (!IsOwner || grabbable == null || IsCarrying)
            {
                return;
            }

            GrabServerRpc(new NetworkObjectReference(grabbable.NetworkObject));
        }

        [ServerRpc]
        private void GrabServerRpc(NetworkObjectReference reference)
        {
            if (_heldObjectId.Value != 0UL)
            {
                return;
            }

            if (!reference.TryGet(out NetworkObject networkObject))
            {
                return;
            }

            var grabbable = networkObject.GetComponent<Grabbable>();
            if (grabbable == null)
            {
                return;
            }

            if (grabbable.ServerTryAddCarrier(OwnerClientId))
            {
                _heldObjectId.Value = networkObject.NetworkObjectId;
            }
        }

        [ServerRpc]
        private void ReleaseServerRpc(bool thrown, Vector3 aimDirection)
        {
            ServerRelease(thrown, aimDirection);
        }

        private void ServerRelease(bool thrown, Vector3 aimDirection)
        {
            if (!IsServer || _heldObjectId.Value == 0UL)
            {
                return;
            }

            Grabbable held = ResolveGrabbable(_heldObjectId.Value);
            _heldObjectId.Value = 0UL;

            if (held != null)
            {
                held.ServerRemoveCarrier(OwnerClientId, thrown, aimDirection);
            }
        }

        public override void OnNetworkDespawn()
        {
            // Someone rage-quit while holding the fridge — put it down rather than
            // leaving it frozen in mid-air forever.
            if (IsServer)
            {
                ServerRelease(false, Vector3.zero);
            }

            if (IsOwner)
            {
                InteractionPromptUI.SetOverride(null);
            }
        }

        private static Grabbable ResolveGrabbable(ulong networkObjectId)
        {
            if (networkObjectId == 0UL || NetworkManager.Singleton == null)
            {
                return null;
            }

            if (NetworkManager.Singleton.SpawnManager == null)
            {
                return null;
            }

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects
                .TryGetValue(networkObjectId, out NetworkObject networkObject) && networkObject != null)
            {
                return networkObject.GetComponent<Grabbable>();
            }

            return null;
        }
    }
}
