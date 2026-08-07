using System.Collections.Generic;
using HouseFlip.Furniture;
using UnityEngine;

namespace HouseFlip.Building
{
    public enum CatalogKind
    {
        Building = 0,
        Furniture = 1
    }

    /// <summary>
    /// The single asset both the build menu and the furniture catalog read from, and the
    /// only thing a placement RPC needs to identify an item.
    ///
    /// Sending an index rather than a prefab reference means a client cannot ask the server
    /// to spawn something that is not in the catalog.
    /// </summary>
    [CreateAssetMenu(menuName = "House Flip/Placement Catalog", fileName = "PlacementCatalog")]
    public class PlacementCatalog : ScriptableObject
    {
        [SerializeField] private List<BuildingData> buildings = new List<BuildingData>();
        [SerializeField] private List<FurnitureData> furniture = new List<FurnitureData>();

        public IReadOnlyList<BuildingData> Buildings => buildings;
        public IReadOnlyList<FurnitureData> Furniture => furniture;

        public int CountOf(CatalogKind kind)
        {
            return kind == CatalogKind.Building ? buildings.Count : furniture.Count;
        }

        /// <summary>Returns null for an out-of-range index rather than throwing — the index arrives over the network.</summary>
        public PlaceableData Resolve(CatalogKind kind, int index)
        {
            if (kind == CatalogKind.Building)
            {
                return index >= 0 && index < buildings.Count ? buildings[index] : null;
            }

            return index >= 0 && index < furniture.Count ? furniture[index] : null;
        }

        public List<FurnitureData> ByCategory(FurnitureCategory category)
        {
            var result = new List<FurnitureData>();
            foreach (FurnitureData item in furniture)
            {
                if (item != null && item.category == category)
                {
                    result.Add(item);
                }
            }

            return result;
        }

#if UNITY_EDITOR
        public void EditorSetContents(List<BuildingData> buildingItems, List<FurnitureData> furnitureItems)
        {
            buildings = buildingItems;
            furniture = furnitureItems;
        }
#endif
    }
}
