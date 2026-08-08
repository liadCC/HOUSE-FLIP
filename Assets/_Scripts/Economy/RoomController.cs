using System.Collections.Generic;
using HouseFlip.Cleaning;
using HouseFlip.Core;
using HouseFlip.Demolition;
using HouseFlip.Repair;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Economy
{
    /// <summary>
    /// All per-room state lives here rather than in GameManager (GDD 25).
    /// Add a new room by dropping this component on a trigger volume — nothing
    /// else in the game needs to know it exists.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class RoomController : NetworkBehaviour
    {
        [SerializeField] private string roomName = "Room";

        [Tooltip("Rooms with a higher weight pull harder on the final house score.")]
        [SerializeField] private float scoreWeight = 1f;

        private BoxCollider _bounds;

        private readonly List<DirtSource> _dirt = new List<DirtSource>();
        private readonly List<Destructible> _destructibles = new List<Destructible>();
        private readonly List<RepairableFixture> _fixtures = new List<RepairableFixture>();

        private float _initialDirt;

        /// <summary>0 = filthy, 1 = spotless (GDD 12). Server-authoritative.</summary>
        public readonly NetworkVariable<float> Cleanliness = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> FurnitureCount = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<float> DesignPoints = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Permanent hit applied when a random event is left unresolved (GDD 21).</summary>
        public readonly NetworkVariable<float> CleanlinessPenalty = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public string RoomName => roomName;
        public float ScoreWeight => Mathf.Max(0.01f, scoreWeight);
        public Bounds WorldBounds => _bounds != null ? _bounds.bounds : new Bounds(transform.position, Vector3.one);

        private void Awake()
        {
            _bounds = GetComponent<BoxCollider>();
            _bounds.isTrigger = true;
            RoomRegistry.Register(this);
        }

        public override void OnDestroy()
        {
            RoomRegistry.Unregister(this);
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                RecomputeCleanliness();
            }

            Cleanliness.OnValueChanged += (_, __) => GameEvents.RaiseRoomStateChanged(this);
            FurnitureCount.OnValueChanged += (_, __) => GameEvents.RaiseRoomStateChanged(this);
            DesignPoints.OnValueChanged += (_, __) => GameEvents.RaiseRoomStateChanged(this);
        }

        public bool Contains(Vector3 worldPosition)
        {
            return _bounds != null && _bounds.bounds.Contains(worldPosition);
        }

        // ------------------------------------------------------------------
        // Registration (called by the objects themselves on Start)
        // ------------------------------------------------------------------

        public void RegisterDirt(DirtSource dirt)
        {
            if (dirt != null && !_dirt.Contains(dirt))
            {
                _dirt.Add(dirt);
                _initialDirt += dirt.MaxDirt;

                if (IsServer && IsSpawned)
                {
                    RecomputeCleanliness();
                }
            }
        }

        public void RegisterDestructible(Destructible destructible)
        {
            if (destructible != null && !_destructibles.Contains(destructible))
            {
                _destructibles.Add(destructible);
            }
        }

        public void RegisterFixture(RepairableFixture fixture)
        {
            if (fixture != null && !_fixtures.Contains(fixture))
            {
                _fixtures.Add(fixture);
            }
        }

        // ------------------------------------------------------------------
        // Server-side state changes
        // ------------------------------------------------------------------

        /// <summary>Server only. Recomputes cleanliness from every dirt source in the room.</summary>
        public void RecomputeCleanliness()
        {
            if (!IsServer)
            {
                return;
            }

            if (_initialDirt <= 0.001f)
            {
                // A room with nothing to clean starts spotless, but an unresolved water
                // leak can still have left a permanent penalty on it (GDD 21).
                Cleanliness.Value = Mathf.Clamp01(1f - CleanlinessPenalty.Value);
            }
            else
            {
                float remaining = 0f;
                foreach (DirtSource dirt in _dirt)
                {
                    if (dirt != null)
                    {
                        remaining += dirt.RemainingDirt;
                    }
                }

                float clean = 1f - (remaining / _initialDirt);
                Cleanliness.Value = Mathf.Clamp01(clean - CleanlinessPenalty.Value);
            }

            // Raised on both paths: the early return used to skip it, so flood damage to
            // a room with no dirt piles never reached the house value.
            GameEvents.RaiseHouseStateDirty();
        }

        /// <summary>Server only. Used by the water-leak event when the pipe is never fixed.</summary>
        public void ApplyCleanlinessPenalty(float amount)
        {
            if (!IsServer)
            {
                return;
            }

            CleanlinessPenalty.Value = Mathf.Clamp01(CleanlinessPenalty.Value + amount);
            RecomputeCleanliness();
        }

        /// <summary>Server only.</summary>
        public void AddFurniture(int delta, float designPoints)
        {
            if (!IsServer)
            {
                return;
            }

            FurnitureCount.Value = Mathf.Max(0, FurnitureCount.Value + delta);
            DesignPoints.Value = Mathf.Max(0f, DesignPoints.Value + designPoints);
            GameEvents.RaiseHouseStateDirty();
        }

        /// <summary>Server only.</summary>
        public void AddDesignPoints(float points)
        {
            if (!IsServer)
            {
                return;
            }

            DesignPoints.Value = Mathf.Max(0f, DesignPoints.Value + points);
            GameEvents.RaiseHouseStateDirty();
        }

        // ------------------------------------------------------------------
        // Scoring (GDD 17)
        // ------------------------------------------------------------------

        public RoomScore BuildScore()
        {
            return new RoomScore
            {
                RoomName = new FixedString32Bytes(Truncate(roomName)),
                Cleanliness = Mathf.Clamp01(Cleanliness.Value) * 100f,
                Furniture = FurnitureSubScore(),
                Design = DesignSubScore(),
                Condition = ConditionSubScore()
            };
        }

        private float FurnitureSubScore()
        {
            float target = Mathf.Max(1, GameConstants.FurnitureTargetPerRoom);
            return Mathf.Clamp01(FurnitureCount.Value / target) * 100f;
        }

        private float DesignSubScore()
        {
            return Mathf.Clamp01(DesignPoints.Value / GameConstants.DesignTargetPerRoom) * 100f;
        }

        /// <summary>
        /// Condition penalises unrepaired fixtures and load-bearing structure that was
        /// smashed. Deliberately demolishing a non-structural wall costs nothing here —
        /// that is renovation, not damage.
        /// </summary>
        private float ConditionSubScore()
        {
            int problems = 0;
            int considered = 0;

            foreach (RepairableFixture fixture in _fixtures)
            {
                if (fixture == null)
                {
                    continue;
                }

                considered++;
                if (!fixture.IsRepaired)
                {
                    problems++;
                }
            }

            foreach (Destructible destructible in _destructibles)
            {
                if (destructible == null || !destructible.IsStructural)
                {
                    continue;
                }

                considered++;
                if (destructible.IsDestroyed)
                {
                    problems++;
                }
            }

            if (considered == 0)
            {
                return 100f;
            }

            return Mathf.Clamp01(1f - (float)problems / considered) * 100f;
        }

        public int CountUnrepairedFixtures()
        {
            int count = 0;
            foreach (RepairableFixture fixture in _fixtures)
            {
                if (fixture != null && !fixture.IsRepaired)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountStructuralDamage()
        {
            int count = 0;
            foreach (Destructible destructible in _destructibles)
            {
                if (destructible != null && destructible.IsStructural && destructible.IsDestroyed)
                {
                    count++;
                }
            }

            return count;
        }

        public float RemainingDirt()
        {
            float remaining = 0f;
            foreach (DirtSource dirt in _dirt)
            {
                if (dirt != null)
                {
                    remaining += dirt.RemainingDirt;
                }
            }

            return remaining;
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Room";
            }

            // FixedString32Bytes holds 29 bytes of payload; keep well clear of the limit.
            return value.Length <= 24 ? value : value.Substring(0, 24);
        }

        private void OnDrawGizmosSelected()
        {
            BoxCollider box = _bounds != null ? _bounds : GetComponent<BoxCollider>();
            if (box == null)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.9f, 0.6f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
