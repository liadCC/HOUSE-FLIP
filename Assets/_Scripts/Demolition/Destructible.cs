using HouseFlip.Core;
using HouseFlip.Economy;
using HouseFlip.Interaction;
using HouseFlip.Networking;
using HouseFlip.Painting;
using HouseFlip.Player;
using HouseFlip.Polish;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Demolition
{
    /// <summary>
    /// State-based destruction (GDD 9). Explicitly *not* voxel or per-pixel: an object
    /// has hit points, an optional damaged mesh, and a destroyed state that swaps in rubble.
    ///
    /// Hit points live in a server-owned NetworkVariable so four players swinging at the
    /// same cabinet cannot each destroy it independently.
    /// </summary>
    public class Destructible : NetworkBehaviour, IInteractable, IToolGated
    {
        [Header("Durability")]
        [SerializeField] private int maxHitPoints = 3;

        [Tooltip("Structural objects hurt the room's Condition score when smashed. Non-structural walls are free to remove.")]
        [SerializeField] private bool isStructural;

        [Header("Visual states")]
        [SerializeField] private GameObject intactVisual;
        [SerializeField] private GameObject damagedVisual;

        [Tooltip("Spawned in place of the object when it is destroyed. Optional.")]
        [SerializeField] private GameObject rubblePrefab;

        [Tooltip("Leave the wreck in the world instead of deleting it.")]
        [SerializeField] private bool disableInsteadOfDestroy = true;

        [Header("Feedback")]
        [SerializeField] private ParticleSystem debrisEffect;
        [SerializeField] private float knockbackRadius = 2.2f;
        [SerializeField] private float knockbackForce = 3.5f;

        [Header("Interaction")]
        [SerializeField] private string smashVerb = "Smash";

        private readonly NetworkVariable<int> _hitPoints = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private RoomController _room;

        public bool IsStructural => isStructural;
        public bool IsDestroyed => _hitPoints.Value <= 0 && IsSpawned;

        public ToolType RequiredTool => ToolType.Hammer;

        private void Awake()
        {
            if (maxHitPoints < 1)
            {
                maxHitPoints = 1;
            }
        }

        private void Start()
        {
            _room = RoomRegistry.FindRoom(transform.position);
            if (_room != null)
            {
                _room.RegisterDestructible(this);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _hitPoints.Value = maxHitPoints;
            }

            _hitPoints.OnValueChanged += OnHitPointsChanged;
            RefreshVisuals(_hitPoints.Value);
        }

        public override void OnNetworkDespawn()
        {
            _hitPoints.OnValueChanged -= OnHitPointsChanged;
        }

        // ------------------------------------------------------------------
        // Interaction
        // ------------------------------------------------------------------

        public string GetPromptText()
        {
            if (IsDestroyed)
            {
                return null;
            }

            PlayerController local = PlayerRegistry.LocalPlayer;
            if (local != null && local.Tools != null && local.Tools.CurrentTool != ToolType.Hammer)
            {
                return "Smash (Need Hammer)";
            }

            return $"{smashVerb} ({_hitPoints.Value})";
        }

        public void OnInteract(PlayerController player)
        {
            if (player == null || IsDestroyed)
            {
                return;
            }

            if (player.Tools == null || player.Tools.CurrentTool != ToolType.Hammer)
            {
                return;
            }

            RenovationService.Instance?.RequestDemolishHit(this);
        }

        // ------------------------------------------------------------------
        // Server-side damage
        // ------------------------------------------------------------------

        /// <summary>Server only. Applies one hammer swing.</summary>
        public void ServerApplyHit(ulong attackerClientId, int damage = 1)
        {
            if (!IsServer || _hitPoints.Value <= 0)
            {
                return;
            }

            _hitPoints.Value = Mathf.Max(0, _hitPoints.Value - Mathf.Max(1, damage));
            PlaySwingClientRpc();

            if (_hitPoints.Value > 0)
            {
                return;
            }

            ServerCompleteDestruction(attackerClientId);
        }

        private void ServerCompleteDestruction(ulong attackerClientId)
        {
            PlayerStatsTracker.Record(attackerClientId, PlayerStat.ObjectsDestroyed, 1f);

            if (isStructural)
            {
                // Wrecking a load-bearing wall is damage, not renovation (GDD 16).
                PlayerStatsTracker.Record(attackerClientId, PlayerStat.DamageCaused, 1f);
                HouseValueManager.Instance?.OnStructureDamaged();
            }

            if (rubblePrefab != null)
            {
                GameObject rubble = Instantiate(rubblePrefab, transform.position, transform.rotation);
                var rubbleNetworkObject = rubble.GetComponent<NetworkObject>();
                if (rubbleNetworkObject != null)
                {
                    rubbleNetworkObject.Spawn(true);
                }
            }

            DestroyEffectsClientRpc();
            ServerApplyKnockback();

            GameEvents.RaiseHouseStateDirty();

            // Structural damage is not recorded anywhere — it is counted every sweep by
            // walking the room's live destructibles (RoomController.CountStructuralDamage /
            // ConditionSubScore). Despawning the wreck deletes the only evidence, so the
            // $2000 penalty and the Condition hit evaporate before the next recalculation
            // and smashing a load-bearing wall ends up free. Non-structural wrecks are not
            // counted either way and can go.
            bool mustRemainForScoring = isStructural;

            if (!disableInsteadOfDestroy && !mustRemainForScoring
                && NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        /// <summary>
        /// Server only. Rebuilds a smashed object for a new round.
        ///
        /// Structural wrecks are deliberately never despawned, because the damage penalty
        /// is recounted each sweep from the live object rather than recorded anywhere. That
        /// makes them the one piece of round state with no way back — without this, a wall
        /// smashed in round 1 keeps charging its $2000 against every future round.
        /// </summary>
        public void ServerRestore()
        {
            if (!IsServer || _hitPoints.Value == maxHitPoints)
            {
                return;
            }

            _hitPoints.Value = maxHitPoints;

            // RefreshVisuals runs from the OnValueChanged callback, but only on peers that
            // see the change. Re-run it here so a server with no remote clients also puts
            // its colliders and meshes back.
            RefreshVisuals(maxHitPoints);
            GameEvents.RaiseHouseStateDirty();
        }

        private void ServerApplyKnockback()
        {
            // Small nudge to everything nearby so demolition ripples outward (GDD 9).
            Collider[] hits = Physics.OverlapSphere(transform.position, knockbackRadius);
            foreach (Collider hit in hits)
            {
                Rigidbody body = hit.attachedRigidbody;
                if (body != null && !body.isKinematic && body.gameObject != gameObject)
                {
                    body.AddExplosionForce(knockbackForce, transform.position, knockbackRadius, 0.2f,
                        ForceMode.VelocityChange);
                }
            }
        }

        // ------------------------------------------------------------------
        // Presentation
        // ------------------------------------------------------------------

        private void OnHitPointsChanged(int previous, int current) => RefreshVisuals(current);

        private void RefreshVisuals(int hitPoints)
        {
            bool destroyed = hitPoints <= 0;
            bool damaged = !destroyed && hitPoints < maxHitPoints;

            if (intactVisual != null)
            {
                intactVisual.SetActive(!destroyed && !(damaged && damagedVisual != null));
            }

            if (damagedVisual != null)
            {
                damagedVisual.SetActive(!destroyed && damaged);
            }

            if (destroyed)
            {
                // Both visuals are already off by the lines above; what still has to go is
                // the collision. This used to be gated on disableInsteadOfDestroy, so any
                // wreck that stayed in the world became an invisible wall the player could
                // not walk through — the hole they just smashed was still solid.
                foreach (Collider collider in GetComponentsInChildren<Collider>())
                {
                    collider.enabled = false;
                }
            }
        }

        [ClientRpc]
        private void PlaySwingClientRpc()
        {
            GameEvents.RaiseSfx(SfxId.HammerHit);

            // Land the swing before the object is gone: flash, a chip of debris, a nudge.
            HitFlash.FlashOn(gameObject, CurrentTint);
            ImpactFeedback.Play(transform.position, 0.16f, CurrentTint, debrisPieces: 3, debrisForce: 2.2f);
        }

        [ClientRpc]
        private void DestroyEffectsClientRpc()
        {
            GameEvents.RaiseSfx(SfxId.ObjectBreak);

            // Structural collapse hits harder than a cabinet giving up.
            float trauma = isStructural ? 0.7f : 0.45f;
            ImpactFeedback.Play(transform.position, trauma, CurrentTint, debrisPieces: 12, debrisForce: 4.5f);

            if (debrisEffect != null)
            {
                debrisEffect.transform.SetParent(null, true);
                debrisEffect.Play();
            }
        }

        /// <summary>
        /// The object's current colour, used both for debris tint and as the colour the
        /// hit flash restores to.
        ///
        /// A painted wall is the awkward case: <see cref="PaintableWall"/> writes its
        /// colour through a MaterialPropertyBlock, so the shared material still reports
        /// the original grey plaster. Reading the material would silently strip a wall's
        /// paint the first time somebody hit it.
        /// </summary>
        private Color CurrentTint
        {
            get
            {
                var painted = GetComponent<PaintableWall>();
                if (painted != null && painted.CurrentColorIndex != PaintColors.Unpainted)
                {
                    return painted.CurrentColor;
                }

                Renderer renderer = GetComponentInChildren<Renderer>();
                return renderer != null && renderer.sharedMaterial != null
                    ? renderer.sharedMaterial.color
                    : new Color(0.72f, 0.68f, 0.62f);
            }
        }
    }
}
