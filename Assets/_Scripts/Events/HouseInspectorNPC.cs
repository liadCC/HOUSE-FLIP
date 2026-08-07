using System.Collections.Generic;
using HouseFlip.Core;
using HouseFlip.Economy;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Events
{
    /// <summary>
    /// The inspector who walks the house during EVENT 3 (GDD 21, Event 3).
    ///
    /// Deliberately a dumb waypoint walker rather than a NavMesh agent: the MVP house is
    /// a single floor of open rooms, and this keeps the NPC from being blocked by a sofa
    /// a player just dropped in a doorway. The behaviour is server-driven and the body is
    /// replicated, so everyone watches the same walk.
    /// </summary>
    public class HouseInspectorNPC : NetworkBehaviour
    {
        [SerializeField] private float walkSpeed = 1.9f;
        [SerializeField] private float pauseAtRoom = 2.5f;
        [SerializeField] private float bodyHeightOffset = 0.1f;

        [Header("Verdict")]
        [Tooltip("Design points granted for a room scoring above the praise threshold.")]
        [SerializeField] private float praisePoints = 12f;

        [Tooltip("Design points removed for a room scoring below the scold threshold.")]
        [SerializeField] private float scoldPoints = 10f;

        [SerializeField] private float praiseThreshold = 70f;
        [SerializeField] private float scoldThreshold = 35f;

        private readonly List<RoomController> _route = new List<RoomController>();
        private int _routeIndex;
        private float _pauseTimer;
        private bool _walking;

        public bool IsWalking => _walking;

        /// <summary>Server only. Starts the tour.</summary>
        public void ServerBeginTour()
        {
            if (!IsServer)
            {
                return;
            }

            _route.Clear();
            foreach (RoomController room in RoomRegistry.All)
            {
                if (room != null)
                {
                    _route.Add(room);
                }
            }

            if (_route.Count == 0)
            {
                return;
            }

            _routeIndex = 0;
            _pauseTimer = 0f;
            _walking = true;

            transform.position = RoomPoint(_route[0]) + Vector3.left * 4f;
            SetVisibleClientRpc(true);
        }

        /// <summary>Server only.</summary>
        public void ServerEndTour()
        {
            if (!IsServer)
            {
                return;
            }

            _walking = false;
            SetVisibleClientRpc(false);
        }

        private void Update()
        {
            if (!IsServer || !_walking)
            {
                return;
            }

            if (_routeIndex >= _route.Count)
            {
                ServerEndTour();
                return;
            }

            RoomController target = _route[_routeIndex];
            if (target == null)
            {
                _routeIndex++;
                return;
            }

            Vector3 destination = RoomPoint(target);
            Vector3 flatDelta = destination - transform.position;
            flatDelta.y = 0f;

            if (flatDelta.magnitude > 0.35f)
            {
                Vector3 step = flatDelta.normalized * walkSpeed * Time.deltaTime;
                transform.position += step;
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(flatDelta.normalized, Vector3.up),
                    6f * Time.deltaTime);
                return;
            }

            // Arrived: stand and judge for a moment before moving on.
            _pauseTimer += Time.deltaTime;
            if (_pauseTimer < pauseAtRoom)
            {
                return;
            }

            _pauseTimer = 0f;
            JudgeRoom(target);
            _routeIndex++;
        }

        private Vector3 RoomPoint(RoomController room)
        {
            Bounds bounds = room.WorldBounds;
            return new Vector3(bounds.center.x, bounds.min.y + bodyHeightOffset, bounds.center.z);
        }

        /// <summary>Applies the small score modifiers described in GDD 21.</summary>
        private void JudgeRoom(RoomController room)
        {
            float score = room.BuildScore().Total;

            if (score >= praiseThreshold)
            {
                room.AddDesignPoints(praisePoints);
                AnnounceClientRpc($"Inspector approves of the {room.RoomName}", true);
            }
            else if (score <= scoldThreshold)
            {
                room.AddDesignPoints(-scoldPoints);
                AnnounceClientRpc($"Inspector is appalled by the {room.RoomName}", false);
            }

            GameEvents.RaiseHouseStateDirty();
        }

        [ClientRpc]
        private void SetVisibleClientRpc(bool visible)
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = visible;
            }
        }

        [ClientRpc]
        private void AnnounceClientRpc(string message, bool positive)
        {
            GameEvents.RaiseRandomEventStarted("INSPECTOR", message);
        }
    }
}
