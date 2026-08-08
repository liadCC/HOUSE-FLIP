using HouseFlip.Building;
using HouseFlip.Core;
using HouseFlip.Economy;
using HouseFlip.Interaction;
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

        private RoomController _room;
        private PlaceableData _data;
        private bool _counted;

        public PlaceableData Data => _data;

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
            _room = room;

            if (_room != null && !_counted)
            {
                _counted = true;
                _room.AddFurniture(1, DesignPointsForRoom(data, room));
            }

            HouseValueManager.Instance?.OnFurniturePlaced(data.valueContribution);
            GameEvents.RaiseHouseStateDirty();
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
        public float RefundValue => _data != null ? _data.cost * refundRatio : 0f;

        /// <summary>Server only. Refunds part of the price and removes the object.</summary>
        public void ServerSellBack(ulong sellerClientId)
        {
            if (!IsServer || _data == null)
            {
                return;
            }

            ServerRemoveContribution();

            BudgetManager.Instance?.Refund(
                _data.cost * refundRatio,
                _data.IsFurniture ? SpendCategory.Furniture : SpendCategory.Renovation);

            PlayerStatsTracker.Record(sellerClientId, PlayerStat.FurniturePlaced, -1f);

            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        private void ServerRemoveContribution()
        {
            if (_room != null && _counted)
            {
                _counted = false;
                _room.AddFurniture(-1, -DesignPointsForRoom(_data, _room));
            }

            HouseValueManager.Instance?.OnFurnitureRemoved(_data.valueContribution);
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
