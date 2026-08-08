using HouseFlip.Building;
using HouseFlip.Core;
using HouseFlip.Economy;
using HouseFlip.Interaction;
using HouseFlip.PhysicsGrab;
using HouseFlip.Player;
using HouseFlip.Polish;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Furniture
{
    /// <summary>
    /// Attached by the server to anything spawned through the placement system.
    /// Owns the bookkeeping so the room's furniture count and the house value stay
    /// correct even when a piece is sold back or destroyed.
    ///
    /// Deliberately *not* an <see cref="IInteractable"/>: every placed item is also a
    /// <see cref="PhysicsGrab.Grabbable"/>, and two bare-hands interactions on one object
    /// would make [E] ambiguous. Selling back is bound to a key on the carry system
    /// instead — you pick the thing up, then decide to get rid of it.
    /// </summary>
    public class PlacedFurniture : NetworkBehaviour
    {
        [SerializeField] private float refundRatio = 0.6f;

        [Tooltip("How often the server re-checks which room this item is standing in.")]
        [SerializeField] private float roomRecheckInterval = 0.5f;

        private RoomController _room;
        private PlaceableData _data;
        private bool _counted;

        /// <summary>
        /// What the room actually accepted, which is not what was offered: both room
        /// totals are clamped to a per-room cap, so an item added to a full room credits
        /// nothing. Remembering the real figure is what keeps add and remove inverse.
        /// </summary>
        private float _creditedValue;
        private float _creditedDesign;

        private Rigidbody _body;
        private Grabbable _grabbable;
        private float _nextRoomCheck;

        /// <summary>
        /// Server-written so the carry UI can read it. <see cref="_data"/> is only ever
        /// assigned on the server, so a client asking the item what it is worth got zero
        /// and the "Sell Back" hint never appeared for anyone but the host.
        /// </summary>
        private readonly NetworkVariable<float> _refund = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public PlaceableData Data => _data;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _grabbable = GetComponent<Grabbable>();
        }

        public override void OnNetworkSpawn()
        {
            // Land with a bounce so a purchase feels like it arrived (GDD 18 — Polish).
            // Kept small: the collider scales with the transform, and a big punch would
            // briefly push the item through whatever it was placed against.
            ScalePunch.PunchOn(gameObject, 0.16f);
        }

        /// <summary>Server only. Called immediately after spawn.</summary>
        public void ServerInitialise(PlaceableData data, RoomController room)
        {
            if (!IsServer)
            {
                return;
            }

            _data = data;
            _refund.Value = data != null ? data.cost * refundRatio : 0f;

            ServerApplyContribution(room);
        }

        /// <summary>Server only. Credits this item to a room and records what actually landed.</summary>
        private void ServerApplyContribution(RoomController room)
        {
            _room = room;

            if (room == null || _data == null || _counted)
            {
                return;
            }

            float valueBefore = room.FurnitureValue.Value;
            float designBefore = room.DesignPoints.Value;

            _counted = true;
            room.AddFurniture(1, DesignPointsForRoom(_data, room));

            // Value is credited to the room, not to a global total, so the per-room cap
            // applies. Placing twelve cabinets in one kitchen must not pay twelve times.
            room.AddFurnitureValue(_data.valueContribution);

            _creditedValue = room.FurnitureValue.Value - valueBefore;
            _creditedDesign = room.DesignPoints.Value - designBefore;

            GameEvents.RaiseHouseStateDirty();
        }

        /// <summary>
        /// Placed items stay grabbable, so a sofa bought for the living room can be carried
        /// into the bedroom. The room owns the furniture count, design points and value, so
        /// credit left behind scores a room for furniture that is no longer standing in it.
        /// </summary>
        private void Update()
        {
            if (!IsServer || !_counted || Time.time < _nextRoomCheck)
            {
                return;
            }

            _nextRoomCheck = Time.time + Mathf.Max(0.1f, roomRecheckInterval);

            // Only re-home an item that has come to rest: mid-carry and mid-throw it would
            // thrash the room totals across every room it passes over.
            if (_grabbable != null && _grabbable.IsHeld)
            {
                return;
            }

            if (_body != null && !_body.isKinematic && _body.linearVelocity.sqrMagnitude > 0.04f)
            {
                return;
            }

            RoomController current = RoomRegistry.FindRoom(transform.position);
            if (current == null || current == _room)
            {
                return;
            }

            ServerRemoveContribution();
            ServerApplyContribution(current);
        }

        /// <summary>
        /// Design points only land when the item suits the room — a toilet in the living
        /// room still adds value, but it is not good taste.
        /// </summary>
        private static float DesignPointsForRoom(PlaceableData data, RoomController room)
        {
            if (data is not FurnitureData furniture || room == null)
            {
                return data != null ? data.designPoints : 0f;
            }

            if (furniture.preferredRooms == null || furniture.preferredRooms.Length == 0)
            {
                return furniture.designPoints;
            }

            foreach (FurnitureCategory preferred in furniture.preferredRooms)
            {
                if (RoomMatches(room, preferred))
                {
                    return furniture.designPoints;
                }
            }

            return furniture.designPoints * 0.25f;
        }

        private static bool RoomMatches(RoomController room, FurnitureCategory category)
        {
            string name = room.RoomName.Replace(" ", string.Empty);
            return string.Equals(name, category.ToString(), System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Refund the player would receive, for the carry-mode hint.</summary>
        public float RefundValue => _refund.Value;

        /// <summary>Server only. Refunds part of the price and removes the object.</summary>
        public void ServerSellBack(ulong sellerClientId)
        {
            // The despawn below is not instantaneous for a second RPC already queued this
            // frame, so clearing _data first makes a repeat sale a no-op rather than a
            // second refund for the same object.
            if (!IsServer || _data == null)
            {
                return;
            }

            PlaceableData sold = _data;
            _data = null;

            ServerRemoveContribution();

            BudgetManager.Instance?.Refund(
                sold.cost * refundRatio,
                sold.IsFurniture ? SpendCategory.Furniture : SpendCategory.Renovation);

            PlayerStatsTracker.Record(sellerClientId, PlayerStat.FurniturePlaced, -1f);

            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        private void ServerRemoveContribution()
        {
            if (_room == null || !_counted)
            {
                return;
            }

            _counted = false;

            // Give back exactly what the room took, not the item's nominal figures. The
            // room clamps both totals to a per-room cap, so an item added to a full room
            // credits nothing while subtracting its full value — a few place-and-sell
            // cycles in a finished room would strip value the room had legitimately earned.
            _room.AddFurniture(-1, -_creditedDesign);
            _room.AddFurnitureValue(-_creditedValue);

            _creditedValue = 0f;
            _creditedDesign = 0f;

            GameEvents.RaiseHouseStateDirty();
        }

        public override void OnNetworkDespawn()
        {
            // Covers destruction by any other route (a thrown fridge, a collapsed wall).
            if (IsServer && _counted)
            {
                ServerRemoveContribution();
            }
        }
    }
}
