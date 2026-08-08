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
        //
        // Tuned against Balance/BalanceModel, not guessed. The originals made a profit
        // unavoidable: cleaning alone was worth $30,000 for no money at all, and design
        // points paid $40 each, so any team cleared +$70,000 without thinking. See
        // Tools/README.md for the outcome curve these produce.

        /// <summary>Cleaning is hygiene, not the main value driver — it mostly avoids the dirt penalty.</summary>
        public const float MaxCleanlinessValuePerRoom = 1200f;

        public const float DirtPenaltyPerUnit = 300f;

        /// <summary>Wrecking load-bearing structure should really hurt: "don't destroy it completely".</summary>
        public const float StructuralDamagePenalty = 2000f;

        public const float BrokenFixturePenalty = 900f;

        public const float DesignPointValue = 7f;

        /// <summary>
        /// Ceiling on how much furniture value one room can contribute.
        ///
        /// Without this, any item whose value exceeds its cost could be placed repeatedly
        /// for unbounded profit — cram twelve cabinets into the kitchen and the house is
        /// worth a fortune. The cap makes the first few pieces per room the ones that count.
        /// </summary>
        public const float MaxFurnitureValuePerRoom = 8000f;

        /// <summary>Same reasoning for design: stops wall-repainting and plant-spam farming.</summary>
        public const float MaxDesignPointsPerRoom = 150f;

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
