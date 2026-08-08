using System.Collections.Generic;
using HouseFlip.Core;
using UnityEngine;

namespace HouseFlip.Balance
{
    /// <summary>The financial outcome of one modelled round.</summary>
    public struct RoundOutcome
    {
        public string Label;
        public float CleanlinessBonus;
        public float RepairBonus;
        public float FurnitureValue;
        public float DesignBonus;
        public float DamagePenalty;
        public float Spend;

        public float FinalValue => Mathf.Max(0f,
            GameConstants.HouseBaseValue + CleanlinessBonus + RepairBonus + FurnitureValue
            + DesignBonus - DamagePenalty);

        public float Investment => GameConstants.HousePurchasePrice + Spend;
        public float Profit => FinalValue - Investment;

        public override string ToString()
        {
            return $"{Label,-22} value {FinalValue,9:N0}  spend {Spend,7:N0}  profit {Profit,9:N0}";
        }
    }

    /// <summary>
    /// A solvable model of one round's economy, used to tune the numbers rather than guess
    /// at them (GDD 15, 16, 19).
    ///
    /// It reads the same <see cref="HouseDefinition"/> and <see cref="CatalogDefinition"/>
    /// data the scene is generated from, so the model cannot drift away from the game.
    ///
    /// Two simplifications, stated so the numbers are not over-trusted:
    ///  - Each catalog item is bought at most once. In game you can place several, which
    ///    is why the per-room value caps in <see cref="Economy.RoomController"/> exist.
    ///  - It assumes every action succeeds and nothing is wasted, so "best play" is an
    ///    upper bound no real team will quite reach.
    /// </summary>
    public static class BalanceModel
    {
        // --- Estimated seconds per action, including walking there and swapping tools ---
        public const float SecondsPerDirtSource = 7f;
        public const float SecondsPerRepair = 12f;
        public const float SecondsPerWallPaint = 7f;
        public const float SecondsPerPlacement = 18f;

        // ------------------------------------------------------------------
        // Fixed components
        // ------------------------------------------------------------------

        public static float FullCleanlinessBonus =>
            HouseDefinition.RoomCount * GameConstants.MaxCleanlinessValuePerRoom;

        /// <summary>Cleanliness the house starts with: only rooms that have no dirt at all.</summary>
        public static float InitialCleanlinessBonus
        {
            get
            {
                float bonus = 0f;
                foreach (RoomDefinition room in HouseDefinition.Rooms)
                {
                    if (room.DirtCount == 0)
                    {
                        bonus += GameConstants.MaxCleanlinessValuePerRoom;
                    }
                }

                return bonus;
            }
        }

        public static float InitialDirtPenalty =>
            HouseDefinition.TotalDirtSources * GameConstants.DirtPenaltyPerUnit;

        public static float InitialBrokenFixturePenalty =>
            HouseDefinition.Fixtures.Length * GameConstants.BrokenFixturePenalty;

        public static float TotalRepairCost
        {
            get
            {
                float total = 0f;
                foreach (FixtureDefinition fixture in HouseDefinition.Fixtures)
                {
                    total += fixture.Cost;
                }

                return total;
            }
        }

        public static float TotalRepairValue
        {
            get
            {
                float total = 0f;
                foreach (FixtureDefinition fixture in HouseDefinition.Fixtures)
                {
                    total += fixture.ValueBonus;
                }

                return total;
            }
        }

        public static float FullPaintCost =>
            HouseDefinition.TotalWallSegments * GameConstants.PaintCostPerWall;

        /// <summary>Design points from painting every wall in one coherent scheme (GDD 14).</summary>
        public static float FullPaintDesignPoints =>
            HouseDefinition.TotalWallSegments *
            (GameConstants.PaintDesignPoints + GameConstants.MatchingPaletteBonusPoints);

        // ------------------------------------------------------------------
        // Modelled rounds
        // ------------------------------------------------------------------

        /// <summary>The house as purchased: filthy, broken, and worth less than it cost.</summary>
        public static RoundOutcome AsPurchased()
        {
            return new RoundOutcome
            {
                Label = "Untouched",
                CleanlinessBonus = InitialCleanlinessBonus,
                DamagePenalty = InitialDirtPenalty + InitialBrokenFixturePenalty
            };
        }

        /// <summary>Everything done, budget ignored. The absolute ceiling.</summary>
        public static RoundOutcome UnboundedCeiling()
        {
            return new RoundOutcome
            {
                Label = "Ceiling (no budget)",
                CleanlinessBonus = FullCleanlinessBonus,
                RepairBonus = TotalRepairValue,
                FurnitureValue = CatalogDefinition.TotalFurnitureValue,
                DesignBonus = (CatalogDefinition.TotalFurnitureDesignPoints + FullPaintDesignPoints)
                              * GameConstants.DesignPointValue,
                Spend = TotalRepairCost + CatalogDefinition.TotalFurnitureCost + FullPaintCost
            };
        }

        /// <summary>
        /// The best a team can actually do: clean and repair everything, paint everything,
        /// then spend what is left on the highest-value shopping basket that fits.
        /// </summary>
        public static RoundOutcome BestPlay()
        {
            float committed = TotalRepairCost + FullPaintCost;
            float shoppingBudget = GameConstants.StartingBudget - committed;

            var items = new List<PlaceableDefinition>(CatalogDefinition.Furniture);
            items.AddRange(CatalogDefinition.Buildings);

            Knapsack(items, shoppingBudget, out float basketCost, out float basketValue,
                out float basketDesign);

            return new RoundOutcome
            {
                Label = "Best play",
                CleanlinessBonus = FullCleanlinessBonus,
                RepairBonus = TotalRepairValue,
                FurnitureValue = basketValue,
                DesignBonus = (basketDesign + FullPaintDesignPoints) * GameConstants.DesignPointValue,
                Spend = committed + basketCost
            };
        }

        /// <summary>
        /// A team that shops enthusiastically and does none of the boring work, smashing a
        /// few load-bearing walls on the way through. This is the round that must lose money.
        /// </summary>
        public static RoundOutcome NegligentPlay(int structuralWallsSmashed = 4)
        {
            var items = new List<PlaceableDefinition>(CatalogDefinition.Furniture);
            Knapsack(items, GameConstants.StartingBudget, out float basketCost, out float basketValue,
                out float basketDesign);

            return new RoundOutcome
            {
                Label = "Negligent play",
                CleanlinessBonus = InitialCleanlinessBonus,
                RepairBonus = 0f,
                FurnitureValue = basketValue,
                DesignBonus = basketDesign * GameConstants.DesignPointValue,
                DamagePenalty = InitialDirtPenalty + InitialBrokenFixturePenalty
                                + structuralWallsSmashed * GameConstants.StructuralDamagePenalty,
                Spend = basketCost
            };
        }

        /// <summary>
        /// Cleaning only, ignoring the broken fixtures. This one is meant to LOSE money:
        /// tidying a house whose plumbing and wiring are still broken does not sell it.
        /// It is the check that scrubbing floors alone cannot carry a round.
        /// </summary>
        public static RoundOutcome CleanOnlyPlay()
        {
            return new RoundOutcome
            {
                Label = "Clean only",
                CleanlinessBonus = FullCleanlinessBonus,
                DamagePenalty = InitialBrokenFixturePenalty
            };
        }

        /// <summary>Cleaning and repairing only — no shopping at all. The frugal route.</summary>
        public static RoundOutcome FrugalPlay()
        {
            return new RoundOutcome
            {
                Label = "Clean + repair only",
                CleanlinessBonus = FullCleanlinessBonus,
                RepairBonus = TotalRepairValue,
                Spend = TotalRepairCost
            };
        }

        // ------------------------------------------------------------------
        // Shopping optimiser
        // ------------------------------------------------------------------

        /// <summary>
        /// 0/1 knapsack over the catalog, maximising effective value within the budget.
        /// Costs are whole dollars, so an exact integer DP is cheap and avoids the
        /// rounding fuzz a greedy ratio sort would introduce.
        /// </summary>
        public static void Knapsack(List<PlaceableDefinition> items, float budget,
            out float chosenCost, out float chosenValue, out float chosenDesign)
        {
            chosenCost = 0f;
            chosenValue = 0f;
            chosenDesign = 0f;

            int capacity = Mathf.Max(0, Mathf.FloorToInt(budget));
            if (capacity <= 0 || items.Count == 0)
            {
                return;
            }

            int n = items.Count;
            var best = new float[n + 1, capacity + 1];

            for (int i = 1; i <= n; i++)
            {
                int cost = Mathf.CeilToInt(items[i - 1].Cost);
                float value = items[i - 1].EffectiveValue(GameConstants.DesignPointValue);

                for (int c = 0; c <= capacity; c++)
                {
                    float skip = best[i - 1, c];
                    best[i, c] = skip;

                    if (cost <= c)
                    {
                        float take = best[i - 1, c - cost] + value;
                        if (take > skip)
                        {
                            best[i, c] = take;
                        }
                    }
                }
            }

            // Walk the table back to recover which items were taken.
            int remaining = capacity;
            for (int i = n; i >= 1; i--)
            {
                if (Mathf.Approximately(best[i, remaining], best[i - 1, remaining]))
                {
                    continue;
                }

                PlaceableDefinition item = items[i - 1];
                chosenCost += item.Cost;
                chosenValue += item.ValueContribution;
                chosenDesign += item.DesignPoints;

                remaining -= Mathf.CeilToInt(item.Cost);
            }
        }

        // ------------------------------------------------------------------
        // Time budget (GDD 3: a round should be completable in ~25 minutes)
        // ------------------------------------------------------------------

        /// <summary>Total single-player seconds of work the house contains.</summary>
        public static float TotalWorkSeconds =>
            HouseDefinition.TotalDirtSources * SecondsPerDirtSource
            + HouseDefinition.Fixtures.Length * SecondsPerRepair
            + HouseDefinition.TotalWallSegments * SecondsPerWallPaint
            + CatalogDefinition.Furniture.Length * SecondsPerPlacement;

        public static float WorkSecondsFor(int players) => TotalWorkSeconds / Mathf.Max(1, players);

        /// <summary>Fraction of the round a team of this size spends actually working.</summary>
        public static float RoundUtilisation(int players) =>
            WorkSecondsFor(players) / GameConstants.SessionSeconds;

        // ------------------------------------------------------------------
        // Report
        // ------------------------------------------------------------------

        public static string BuildReport()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("HOUSE FLIP — BALANCE MODEL");
            sb.AppendLine("==========================================================");
            sb.AppendLine($"Budget             ${GameConstants.StartingBudget:N0}");
            sb.AppendLine($"House price        ${GameConstants.HousePurchasePrice:N0}");
            sb.AppendLine($"Rooms              {HouseDefinition.RoomCount}");
            sb.AppendLine($"Dirt sources       {HouseDefinition.TotalDirtSources}");
            sb.AppendLine($"Broken fixtures    {HouseDefinition.Fixtures.Length}");
            sb.AppendLine($"Wall segments      {HouseDefinition.TotalWallSegments}");
            sb.AppendLine($"Catalog items      {CatalogDefinition.Furniture.Length} furniture, " +
                          $"{CatalogDefinition.Buildings.Length} building");
            sb.AppendLine();

            sb.AppendLine("COST OF DOING EVERYTHING");
            sb.AppendLine($"  Repairs          ${TotalRepairCost:N0}");
            sb.AppendLine($"  Paint all walls  ${FullPaintCost:N0}");
            sb.AppendLine($"  Whole catalog    ${CatalogDefinition.TotalFurnitureCost:N0}");
            float everything = TotalRepairCost + FullPaintCost + CatalogDefinition.TotalFurnitureCost;
            sb.AppendLine($"  TOTAL            ${everything:N0}" +
                          (everything > GameConstants.StartingBudget
                              ? $"   (over budget by ${everything - GameConstants.StartingBudget:N0} — forces choices)"
                              : "   (WITHIN BUDGET — no shopping decision to make)"));
            sb.AppendLine();

            sb.AppendLine("OUTCOMES");
            sb.AppendLine($"  {AsPurchased()}");
            sb.AppendLine($"  {NegligentPlay()}");
            sb.AppendLine($"  {CleanOnlyPlay()}");
            sb.AppendLine($"  {FrugalPlay()}");
            sb.AppendLine($"  {BestPlay()}");
            sb.AppendLine($"  {UnboundedCeiling()}");
            sb.AppendLine();

            sb.AppendLine("TIME BUDGET");
            sb.AppendLine($"  Total work       {TotalWorkSeconds:N0}s of single-player effort");
            for (int players = 1; players <= 4; players++)
            {
                sb.AppendLine($"  {players} player(s)      {WorkSecondsFor(players) / 60f,5:0.0} min" +
                              $"   ({RoundUtilisation(players) * 100f,5:0.0}% of the 25 min round)");
            }

            return sb.ToString();
        }
    }
}
