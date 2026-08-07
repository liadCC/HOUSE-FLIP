using HouseFlip.Building;
using UnityEngine;

namespace HouseFlip.Furniture
{
    /// <summary>Room categories used to group the catalog UI (GDD 11).</summary>
    public enum FurnitureCategory
    {
        LivingRoom = 0,
        Bedroom = 1,
        Kitchen = 2,
        Bathroom = 3,
        Decoration = 4
    }

    /// <summary>
    /// A catalog item the player can buy and place (GDD 11).
    ///
    /// Field names follow the GDD's snippet (cost, houseValueBonus, designPoints, size)
    /// while reusing the shared placement plumbing in <see cref="PlaceableData"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "House Flip/Furniture", fileName = "Furniture_")]
    public class FurnitureData : PlaceableData
    {
        public FurnitureCategory category = FurnitureCategory.LivingRoom;

        [Tooltip("Rooms this item is intended for. Placing it elsewhere still works but earns no design points.")]
        public FurnitureCategory[] preferredRooms;

        /// <summary>Alias for <see cref="PlaceableData.valueContribution"/>, matching the GDD field name.</summary>
        public float houseValueBonus
        {
            get => valueContribution;
            set => valueContribution = value;
        }

        public override bool IsFurniture => true;
    }
}
