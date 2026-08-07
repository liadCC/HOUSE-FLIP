using UnityEngine;

namespace HouseFlip.Building
{
    /// <summary>Structural pieces the player rebuilds after demolition (GDD 10).</summary>
    [CreateAssetMenu(menuName = "House Flip/Building Piece", fileName = "Build_")]
    public class BuildingData : PlaceableData
    {
        [Tooltip("Structural pieces must sit flush against the floor grid; decor can float on shelves.")]
        public bool requiresFloorContact = true;

        public override bool IsFurniture => false;
    }
}
