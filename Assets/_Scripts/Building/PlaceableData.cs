using UnityEngine;

namespace HouseFlip.Building
{
    /// <summary>
    /// Shared data contract for anything the player can place on the grid.
    /// Building pieces and furniture differ in what they contribute, not in how they
    /// are placed, so the placer only ever talks to this base type (GDD 25).
    /// </summary>
    public abstract class PlaceableData : ScriptableObject
    {
        [Header("Identity")]
        public string itemName = "Item";
        public Sprite icon;

        [Header("Economy")]
        public float cost = 100f;

        [Tooltip("House value added when this object is placed.")]
        public float valueContribution = 250f;

        [Tooltip("Design points added to the room it lands in.")]
        public float designPoints;

        [Header("Placement")]
        [Tooltip("Footprint in grid cells.")]
        public Vector2Int size = Vector2Int.one;

        [Tooltip("Height used for the overlap test, in metres.")]
        public float height = 1f;

        public GameObject prefab;

        /// <summary>Which budget line this purchase lands on for the results screen.</summary>
        public abstract bool IsFurniture { get; }
    }
}
