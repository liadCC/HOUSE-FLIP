using HouseFlip.Furniture;
using HouseFlip.PhysicsGrab;
using UnityEngine;

namespace HouseFlip.Balance
{
    /// <summary>One catalog entry, as plain data.</summary>
    public readonly struct PlaceableDefinition
    {
        public readonly string AssetName;
        public readonly string DisplayName;
        public readonly bool IsFurniture;
        public readonly FurnitureCategory Category;
        public readonly float Cost;
        public readonly float ValueContribution;
        public readonly float DesignPoints;
        public readonly Vector2Int Size;
        public readonly float Height;
        public readonly Vector3 PrefabSize;
        public readonly Color Color;
        public readonly MassCategory Mass;

        /// <summary>Structural pieces must sit on the floor grid; decor can go on a shelf.</summary>
        public readonly bool RequiresFloorContact;

        public PlaceableDefinition(string assetName, string displayName, bool isFurniture,
            FurnitureCategory category, float cost, float valueContribution, float designPoints,
            Vector2Int size, float height, Vector3 prefabSize, Color color, MassCategory mass,
            bool requiresFloorContact = true)
        {
            AssetName = assetName;
            DisplayName = displayName;
            IsFurniture = isFurniture;
            Category = category;
            Cost = cost;
            ValueContribution = valueContribution;
            DesignPoints = designPoints;
            Size = size;
            Height = height;
            PrefabSize = prefabSize;
            Color = color;
            Mass = mass;
            RequiresFloorContact = requiresFloorContact;
        }

        /// <summary>
        /// What this item is actually worth to the sale: its own value plus the cash value
        /// of the design points it brings. This is the number that decides whether it is
        /// worth buying, so it is what the balance model optimises on.
        /// </summary>
        public float EffectiveValue(float designPointValue) => ValueContribution + DesignPoints * designPointValue;
    }

    /// <summary>
    /// The MVP catalog from GDD 10 and 11, as data.
    ///
    /// Lives in the runtime assembly so the balance model reads exactly the numbers the
    /// editor generator turns into ScriptableObjects. Value-to-cost ratios are
    /// deliberately uneven — between about 1.2x and 1.6x — because a catalog where
    /// everything has the same ratio makes the shopping decision meaningless.
    ///
    /// The full list costs more than the budget on purpose (see BalanceModel), so a round
    /// is about choosing well, not buying everything.
    /// </summary>
    public static class CatalogDefinition
    {
        private static Color Rgb(float r, float g, float b) => new Color(r, g, b);

        public static readonly PlaceableDefinition[] Buildings =
        {
            new PlaceableDefinition("Build_WallSegment", "Wall Segment", false, default,
                cost: 400f, valueContribution: 460f, designPoints: 3f,
                new Vector2Int(4, 1), 3f, new Vector3(2f, 3f, 0.2f),
                Rgb(0.90f, 0.88f, 0.83f), MassCategory.Heavy),

            new PlaceableDefinition("Build_Door", "Door", false, default,
                cost: 500f, valueContribution: 560f, designPoints: 5f,
                new Vector2Int(2, 1), 2.4f, new Vector3(1f, 2.4f, 0.14f),
                Rgb(0.55f, 0.36f, 0.22f), MassCategory.Medium),

            new PlaceableDefinition("Build_Window", "Window", false, default,
                cost: 550f, valueContribution: 700f, designPoints: 7f,
                new Vector2Int(2, 1), 1.4f, new Vector3(1.2f, 1.2f, 0.12f),
                Rgb(0.62f, 0.82f, 0.92f), MassCategory.Medium),

            new PlaceableDefinition("Build_FloorTile", "Floor Tile", false, default,
                cost: 110f, valueContribution: 130f, designPoints: 2f,
                new Vector2Int(2, 2), 0.12f, new Vector3(1f, 0.1f, 1f),
                Rgb(0.82f, 0.79f, 0.72f), MassCategory.Light),

            new PlaceableDefinition("Build_Cabinet", "Cabinet", false, default,
                cost: 700f, valueContribution: 820f, designPoints: 7f,
                new Vector2Int(2, 1), 1.9f, new Vector3(1f, 1.9f, 0.55f),
                Rgb(0.72f, 0.55f, 0.35f), MassCategory.Heavy),

            new PlaceableDefinition("Build_Sink", "Sink", false, default,
                cost: 620f, valueContribution: 720f, designPoints: 6f,
                new Vector2Int(2, 1), 0.95f, new Vector3(0.9f, 0.9f, 0.5f),
                Rgb(0.92f, 0.94f, 0.95f), MassCategory.Medium)
        };

        public static readonly PlaceableDefinition[] Furniture =
        {
            // --- Living Room -------------------------------------------------
            new PlaceableDefinition("Furniture_Sofa", "Sofa", true, FurnitureCategory.LivingRoom,
                1900f, 2700f, 22f, new Vector2Int(4, 2), 0.85f, new Vector3(2f, 0.85f, 1f),
                Rgb(0.36f, 0.52f, 0.78f), MassCategory.Heavy),
            new PlaceableDefinition("Furniture_TV", "TV", true, FurnitureCategory.LivingRoom,
                1250f, 1600f, 16f, new Vector2Int(3, 1), 0.7f, new Vector3(1.4f, 0.7f, 0.12f),
                Rgb(0.14f, 0.14f, 0.17f), MassCategory.Medium),
            new PlaceableDefinition("Furniture_CoffeeTable", "Coffee Table", true, FurnitureCategory.LivingRoom,
                520f, 700f, 10f, new Vector2Int(2, 2), 0.45f, new Vector3(1f, 0.45f, 0.6f),
                Rgb(0.62f, 0.42f, 0.26f), MassCategory.Medium),
            new PlaceableDefinition("Furniture_Chair", "Chair", true, FurnitureCategory.LivingRoom,
                300f, 380f, 7f, new Vector2Int(1, 1), 0.9f, new Vector3(0.5f, 0.9f, 0.5f),
                Rgb(0.78f, 0.42f, 0.34f), MassCategory.Light),
            new PlaceableDefinition("Furniture_FloorLamp", "Floor Lamp", true, FurnitureCategory.LivingRoom,
                350f, 470f, 12f, new Vector2Int(1, 1), 1.6f, new Vector3(0.28f, 1.6f, 0.28f),
                Rgb(0.95f, 0.88f, 0.62f), MassCategory.Light),

            // --- Bedroom -----------------------------------------------------
            new PlaceableDefinition("Furniture_Bed", "Bed", true, FurnitureCategory.Bedroom,
                2200f, 3100f, 24f, new Vector2Int(4, 5), 0.6f, new Vector3(2f, 0.6f, 2.4f),
                Rgb(0.85f, 0.80f, 0.72f), MassCategory.Heavy),
            new PlaceableDefinition("Furniture_Wardrobe", "Wardrobe", true, FurnitureCategory.Bedroom,
                1500f, 1950f, 18f, new Vector2Int(3, 2), 2.1f, new Vector3(1.4f, 2.1f, 0.7f),
                Rgb(0.58f, 0.40f, 0.26f), MassCategory.Heavy),
            new PlaceableDefinition("Furniture_Desk", "Desk", true, FurnitureCategory.Bedroom,
                700f, 900f, 11f, new Vector2Int(3, 2), 0.78f, new Vector3(1.4f, 0.78f, 0.7f),
                Rgb(0.68f, 0.50f, 0.32f), MassCategory.Medium),
            new PlaceableDefinition("Furniture_BedsideLamp", "Bedside Lamp", true, FurnitureCategory.Bedroom,
                240f, 330f, 9f, new Vector2Int(1, 1), 0.5f, new Vector3(0.3f, 0.5f, 0.3f),
                Rgb(0.96f, 0.86f, 0.55f), MassCategory.Light),

            // --- Kitchen -----------------------------------------------------
            new PlaceableDefinition("Furniture_Fridge", "Fridge", true, FurnitureCategory.Kitchen,
                2050f, 2800f, 20f, new Vector2Int(2, 2), 1.9f, new Vector3(0.9f, 1.9f, 0.8f),
                Rgb(0.90f, 0.92f, 0.94f), MassCategory.Heavy),
            new PlaceableDefinition("Furniture_Oven", "Oven", true, FurnitureCategory.Kitchen,
                1550f, 2000f, 17f, new Vector2Int(2, 2), 0.95f, new Vector3(0.85f, 0.95f, 0.7f),
                Rgb(0.28f, 0.28f, 0.32f), MassCategory.Heavy),
            new PlaceableDefinition("Furniture_Counter", "Counter", true, FurnitureCategory.Kitchen,
                950f, 1300f, 13f, new Vector2Int(4, 2), 0.95f, new Vector3(2f, 0.95f, 0.7f),
                Rgb(0.80f, 0.74f, 0.62f), MassCategory.Heavy),
            new PlaceableDefinition("Furniture_KitchenSink", "Kitchen Sink", true, FurnitureCategory.Kitchen,
                850f, 1150f, 12f, new Vector2Int(2, 2), 0.95f, new Vector3(0.9f, 0.95f, 0.65f),
                Rgb(0.88f, 0.90f, 0.92f), MassCategory.Medium),

            // --- Bathroom ----------------------------------------------------
            new PlaceableDefinition("Furniture_Toilet", "Toilet", true, FurnitureCategory.Bathroom,
                650f, 900f, 10f, new Vector2Int(2, 2), 0.8f, new Vector3(0.6f, 0.8f, 0.75f),
                Rgb(0.95f, 0.96f, 0.97f), MassCategory.Medium),
            new PlaceableDefinition("Furniture_Shower", "Shower", true, FurnitureCategory.Bathroom,
                1700f, 2300f, 19f, new Vector2Int(3, 3), 2.1f, new Vector3(1.2f, 2.1f, 1.2f),
                Rgb(0.72f, 0.86f, 0.90f), MassCategory.Heavy),
            new PlaceableDefinition("Furniture_BathroomSink", "Bathroom Sink", true, FurnitureCategory.Bathroom,
                570f, 780f, 10f, new Vector2Int(2, 1), 0.9f, new Vector3(0.8f, 0.9f, 0.5f),
                Rgb(0.93f, 0.95f, 0.96f), MassCategory.Medium),
            new PlaceableDefinition("Furniture_Mirror", "Mirror", true, FurnitureCategory.Bathroom,
                320f, 500f, 11f, new Vector2Int(2, 1), 0.9f, new Vector3(0.8f, 0.9f, 0.08f),
                Rgb(0.80f, 0.88f, 0.92f), MassCategory.Light),

            // --- Decoration --------------------------------------------------
            // Poor value-per-dollar but excellent design-per-dollar, and no preferred
            // room. This is the "taste" play: cheap points if you have design headroom.
            new PlaceableDefinition("Furniture_Plant", "Plant", true, FurnitureCategory.Decoration,
                200f, 230f, 12f, new Vector2Int(1, 1), 1.1f, new Vector3(0.45f, 1.1f, 0.45f),
                Rgb(0.34f, 0.68f, 0.36f), MassCategory.Light),
            new PlaceableDefinition("Furniture_Painting", "Painting", true, FurnitureCategory.Decoration,
                430f, 560f, 15f, new Vector2Int(2, 1), 0.8f, new Vector3(0.9f, 0.8f, 0.07f),
                Rgb(0.85f, 0.62f, 0.30f), MassCategory.Light),
            new PlaceableDefinition("Furniture_Rug", "Rug", true, FurnitureCategory.Decoration,
                380f, 480f, 13f, new Vector2Int(4, 3), 0.06f, new Vector3(2f, 0.06f, 1.5f),
                Rgb(0.72f, 0.30f, 0.32f), MassCategory.Light),
            new PlaceableDefinition("Furniture_Clock", "Clock", true, FurnitureCategory.Decoration,
                175f, 210f, 8f, new Vector2Int(1, 1), 0.4f, new Vector3(0.4f, 0.4f, 0.08f),
                Rgb(0.94f, 0.90f, 0.80f), MassCategory.Light)
        };

        public static float TotalFurnitureCost => Sum(Furniture, d => d.Cost);
        public static float TotalFurnitureValue => Sum(Furniture, d => d.ValueContribution);
        public static float TotalFurnitureDesignPoints => Sum(Furniture, d => d.DesignPoints);

        private static float Sum(PlaceableDefinition[] items, System.Func<PlaceableDefinition, float> select)
        {
            float total = 0f;
            foreach (PlaceableDefinition item in items)
            {
                total += select(item);
            }

            return total;
        }
    }
}
