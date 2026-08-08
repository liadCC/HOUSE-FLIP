using HouseFlip.Core;
using HouseFlip.Interaction;
using HouseFlip.Player;
using HouseFlip.Polish;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.PhysicsGrab
{
    /// <summary>
    /// Pick up, carry, co-carry and throw (GDD 8).
    ///
    /// Authority model: the server owns the carrier list and physically moves the object.
    /// Clients only ever ask. That keeps two players yanking the same sofa in opposite
    /// directions from turning into a desync — the server just averages their anchors.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : NetworkBehaviour, IInteractable, IToolGated
    {
        [SerializeField] private MassCategory category = MassCategory.Light;

        [Tooltip("Verb shown in the interaction prompt.")]
        [SerializeField] private string grabVerb = "Grab";

        [Header("Feel")]
        [SerializeField] private float followSharpness = 14f;
        [SerializeField] private float maxFollowSpeed = 14f;

        private Rigidbody _rigidbody;
        private int[] _originalLayers;
        private Transform[] _layerTargets;

        /// <summary>Client ids currently holding this object. Server-writable only.</summary>
        private NetworkList<ulong> _carriers;

        public MassCategory Category => category;

        /// <summary>Carrying is a bare-hands job, which is also what makes [E] pick "Grab"
        /// over "Smash" when the player has put the hammer away.</summary>
        public ToolType RequiredTool => ToolType.None;

        public int RequiredCarriers => category.RequiredCarriers();
        public int CarrierCount => _carriers != null ? _carriers.Count : 0;
        public bool IsHeld => CarrierCount > 0;
        public bool IsFullyCrewed => CarrierCount >= RequiredCarriers;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.mass = category.RigidbodyMass();
            _carriers = new NetworkList<ulong>();

            CacheLayers();
        }

        private void CacheLayers()
        {
            _layerTargets = GetComponentsInChildren<Transform>(true);
            _originalLayers = new int[_layerTargets.Length];
            for (int i = 0; i < _layerTargets.Length; i++)
            {
                _originalLayers[i] = _layerTargets[i].gameObject.layer;
            }
        }

        public override void OnNetworkSpawn()
        {
            _carriers.OnListChanged += OnCarriersChanged;
            ApplyCarryPhysics();
        }

        public override void OnNetworkDespawn()
        {
            // A carrier only remembers this object's NetworkObjectId. Once the object is
            // gone that id resolves to null forever, so the holder stays flagged as
            // carrying something they can no longer see, drop or replace — hand them back
            // empty hands before the id goes stale. Reachable whenever the object dies out
            // from under a carrier: a co-carrier selling a sofa back while the other player
            // is still holding it, or the thing being smashed mid-haul.
            if (IsServer && _carriers != null)
            {
                for (int i = 0; i < _carriers.Count; i++)
                {
                    PlayerRegistry.Get(_carriers[i])?.Carry?.ServerNotifyHeldDespawned(NetworkObjectId);
                }
            }

            if (_carriers != null)
            {
                _carriers.OnListChanged -= OnCarriersChanged;
            }
        }

        private void OnCarriersChanged(NetworkListEvent<ulong> changeEvent) => ApplyCarryPhysics();

        // ------------------------------------------------------------------
        // Interaction
        // ------------------------------------------------------------------

        public string GetPromptText()
        {
            if (IsFullyCrewed)
            {
                return null;
            }

            if (category == MassCategory.Heavy)
            {
                return IsHeld ? "Help Carry" : $"{grabVerb} (Needs 2)";
            }

            return IsHeld ? null : grabVerb;
        }

        public void OnInteract(PlayerController player)
        {
            if (player == null || player.Carry == null)
            {
                return;
            }

            player.Carry.RequestGrab(this);
        }

        // ------------------------------------------------------------------
        // Server-side carry bookkeeping
        // ------------------------------------------------------------------

        /// <summary>Server only. Returns true if the client was added as a carrier.</summary>
        public bool ServerTryAddCarrier(ulong clientId)
        {
            if (!IsServer || _carriers.Contains(clientId) || _carriers.Count >= RequiredCarriers)
            {
                return false;
            }

            _carriers.Add(clientId);
            NotifySfxClientRpc(SfxId.Pickup);
            return true;
        }

        /// <summary>Server only. Removes a carrier and optionally launches the object.</summary>
        public void ServerRemoveCarrier(ulong clientId, bool thrown, Vector3 aimDirection)
        {
            if (!IsServer)
            {
                return;
            }

            for (int i = 0; i < _carriers.Count; i++)
            {
                if (_carriers[i] == clientId)
                {
                    _carriers.RemoveAt(i);
                    break;
                }
            }

            if (_carriers.Count > 0)
            {
                // Still crewed by someone else; it just stops moving until they get help.
                return;
            }

            RestorePhysics();

            if (thrown)
            {
                // Exaggerated on purpose (GDD 8) — a launched toilet is the point of the game.
                float impulse = GameConstants.ThrowImpulse * GameConstants.ThrowComedyMultiplier;
                Vector3 direction = aimDirection.sqrMagnitude > 0.001f ? aimDirection.normalized : Vector3.forward;
                _rigidbody.AddForce((direction + Vector3.up * 0.35f).normalized * impulse, ForceMode.VelocityChange);
                _rigidbody.AddTorque(Random.insideUnitSphere * 8f, ForceMode.VelocityChange);
                NotifySfxClientRpc(SfxId.Throw);
            }
            else
            {
                NotifySfxClientRpc(SfxId.Drop);
            }
        }

        /// <summary>Server only. Drops the object if a carrier disconnects mid-haul.</summary>
        public void ServerForceRelease(ulong clientId) => ServerRemoveCarrier(clientId, false, Vector3.zero);

        private void FixedUpdate()
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            if (!IsFullyCrewed)
            {
                return;
            }

            Vector3 anchor = ComputeAnchor(out bool valid);
            if (!valid)
            {
                return;
            }

            // Kinematic follow rather than spring forces: predictable, never explodes,
            // and reads clearly when two players fight over the same fridge.
            Vector3 next = Vector3.Lerp(
                _rigidbody.position, anchor, 1f - Mathf.Exp(-followSharpness * Time.fixedDeltaTime));

            Vector3 delta = next - _rigidbody.position;
            float maxStep = maxFollowSpeed * Time.fixedDeltaTime;
            if (delta.magnitude > maxStep)
            {
                next = _rigidbody.position + delta.normalized * maxStep;
            }

            _rigidbody.MovePosition(next);

            Quaternion targetRotation = ComputeAnchorRotation();
            _rigidbody.MoveRotation(Quaternion.Slerp(
                _rigidbody.rotation, targetRotation, 1f - Mathf.Exp(-8f * Time.fixedDeltaTime)));
        }

        private Vector3 ComputeAnchor(out bool valid)
        {
            Vector3 sum = Vector3.zero;
            int found = 0;

            for (int i = 0; i < _carriers.Count; i++)
            {
                PlayerController carrier = PlayerRegistry.Get(_carriers[i]);
                if (carrier == null)
                {
                    continue;
                }

                sum += CarryPointFor(carrier);
                found++;
            }

            valid = found > 0;
            return found > 0 ? sum / found : Vector3.zero;
        }

        private Quaternion ComputeAnchorRotation()
        {
            for (int i = 0; i < _carriers.Count; i++)
            {
                PlayerController carrier = PlayerRegistry.Get(_carriers[i]);
                if (carrier != null)
                {
                    return Quaternion.LookRotation(carrier.transform.forward, Vector3.up);
                }
            }

            return _rigidbody.rotation;
        }

        private static Vector3 CarryPointFor(PlayerController carrier)
        {
            Transform t = carrier.transform;
            return t.position + Vector3.up * 1.1f + t.forward * GameConstants.CarryDistance;
        }

        // ------------------------------------------------------------------
        // Physics state
        // ------------------------------------------------------------------

        private void ApplyCarryPhysics()
        {
            if (IsFullyCrewed)
            {
                _rigidbody.useGravity = false;
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                SetLayerRecursive(GameLayers.Carried);
            }
            else
            {
                // Not just "nobody is holding it". A heavy object dropped back to a single
                // carrier is no longer moved by FixedUpdate, and leaving it on the Carried
                // layer takes it out of the interaction raycast mask — so nobody could ever
                // walk up and take the second handle, and it would hang in mid-air with
                // gravity still switched off. Anything short of a full crew goes back to
                // being an ordinary physics object that a second player can see and grab.
                RestorePhysics();
            }
        }

        private void RestorePhysics()
        {
            _rigidbody.useGravity = true;
            RestoreLayers();
        }

        private void SetLayerRecursive(int layer)
        {
            if (_layerTargets == null)
            {
                return;
            }

            foreach (Transform t in _layerTargets)
            {
                if (t != null)
                {
                    t.gameObject.layer = layer;
                }
            }
        }

        private void RestoreLayers()
        {
            if (_layerTargets == null || _originalLayers == null)
            {
                return;
            }

            for (int i = 0; i < _layerTargets.Length; i++)
            {
                if (_layerTargets[i] != null)
                {
                    _layerTargets[i].gameObject.layer = _originalLayers[i];
                }
            }
        }

        [ClientRpc]
        private void NotifySfxClientRpc(SfxId id)
        {
            GameEvents.RaiseSfx(id);

            // Heft: hurling a fridge should kick harder than tossing a lamp.
            if (id == SfxId.Throw)
            {
                float weight = category == MassCategory.Heavy ? 0.32f
                    : category == MassCategory.Medium ? 0.18f : 0.08f;
                ImpactFeedback.Shake(transform.position, weight);
            }
        }
    }
}
