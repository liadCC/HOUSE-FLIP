namespace HouseFlip.Core
{
    /// <summary>
    /// Balance numbers pulled straight out of the GDD. Everything tunable in one place
    /// so designers do not have to hunt through systems.
    /// </summary>
    public static class GameConstants
    {
        // --- Economy (GDD 15, 16, 19) -------------------------------------
        public const float StartingBudget = 20000f;
        public const float HousePurchasePrice = 50000f;
        public const float HouseBaseValue = 50000f;

        // --- Session (GDD 18) --------------------------------------------
        public const float SessionSeconds = 25f * 60f;

        // --- House value weights (GDD 16) --------------------------------
        public const float MaxCleanlinessValuePerRoom = 5000f;
        public const float DirtPenaltyPerUnit = 250f;
        public const float StructuralDamagePenalty = 1200f;
        public const float BrokenFixturePenalty = 800f;
        public const float DesignPointValue = 40f;

        // --- Room score weights (GDD 17) ---------------------------------
        public const float WeightCleanliness = 0.25f;
        public const float WeightFurniture = 0.30f;
        public const float WeightDesign = 0.20f;
        public const float WeightCondition = 0.25f;

        /// <summary>Furniture count that scores a full 100 on a room's furniture sub-score.</summary>
        public const int FurnitureTargetPerRoom = 5;

        /// <summary>Design points that score a full 100 on a room's design sub-score.</summary>
        public const float DesignTargetPerRoom = 100f;

        // --- Painting (GDD 14) -------------------------------------------
        public const float PaintCostPerWall = 100f;
        public const float PaintDesignPoints = 8f;
        public const float MatchingPaletteBonusPoints = 15f;

        // --- Interaction / physics (GDD 7, 8) ----------------------------
        public const float InteractRange = 3.0f;
        public const float CarryDistance = 1.6f;
        public const float ThrowImpulse = 9f;
        public const float ThrowComedyMultiplier = 1.6f;
        public const float HeavyObjectCarriers = 2;

        // --- Grid (GDD 10) ------------------------------------------------
        public const float GridCellSize = 0.5f;

        // --- Random events (GDD 21) --------------------------------------
        public const float FloodCleanlinessDrainPerSecond = 0.02f;
        public const float UnresolvedLeakCleanlinessPenalty = 0.35f;
        public const float InspectorWarningLeadSeconds = 180f;
    }
}
