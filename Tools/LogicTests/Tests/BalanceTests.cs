using HouseFlip.Balance;
using HouseFlip.Core;
using NUnit.Framework;
using UnityEngine;

namespace HouseFlip.Tests
{
    /// <summary>
    /// Locks in the shape of the difficulty curve.
    ///
    /// These are design assertions, not implementation details: if a balance number
    /// changes and one of these fails, the change altered how the game plays and wants a
    /// deliberate decision, not a silent edit. Run
    /// <c>BalanceModel.BuildReport()</c> to see the full picture.
    /// </summary>
    [TestFixture]
    public class BalanceTests
    {
        // ==================================================================
        // The shape of the curve (GDD 1: "try to sell it for a profit")
        // ==================================================================

        [Test]
        public void AnUntouchedHouseIsWorthLessThanYouPaidForIt()
        {
            // It is a wreck. Walking in and doing nothing must be a heavy loss, or the
            // premise of the game does not hold.
            RoundOutcome outcome = BalanceModel.AsPurchased();

            Assert.That(outcome.Profit, Is.LessThan(-10000f),
                "Doing nothing should be a serious loss.");
        }

        [Test]
        public void ShoppingWithoutDoingTheWorkLosesMoney()
        {
            // The failure case the GDD's chaos scenario describes: spend the budget, wreck
            // some structure, skip the boring jobs.
            Assert.That(BalanceModel.NegligentPlay().Profit, Is.LessThan(0f),
                "Buying furniture while ignoring the repairs must not be profitable.");
        }

        [Test]
        public void CleaningAloneIsNotEnoughToTurnAProfit()
        {
            // Cleaning is the easiest work in the game. If it alone carried a round there
            // would be no reason to touch anything else.
            Assert.That(BalanceModel.CleanOnlyPlay().Profit, Is.LessThan(0f),
                "A spotless house with broken plumbing should still sell at a loss.");
        }

        [Test]
        public void RepairsAreWhatTipTheRoundIntoProfit()
        {
            // The intended lesson of a first round: fix the broken things.
            Assert.That(BalanceModel.CleanOnlyPlay().Profit, Is.LessThan(0f));
            Assert.That(BalanceModel.FrugalPlay().Profit, Is.GreaterThan(0f),
                "Cleaning plus repairs, with no shopping at all, should be a viable route.");
        }

        [Test]
        public void ShoppingWellBeatsNotShoppingAtAll()
        {
            // Furniture has to be worth buying, or the catalog is decoration on a spreadsheet.
            Assert.That(BalanceModel.BestPlay().Profit,
                Is.GreaterThan(BalanceModel.FrugalPlay().Profit),
                "A well-chosen basket should beat spending nothing.");
        }

        [Test]
        public void TheCurveSpansLossToProfitInTheIntendedOrder()
        {
            float untouched = BalanceModel.AsPurchased().Profit;
            float negligent = BalanceModel.NegligentPlay().Profit;
            float cleanOnly = BalanceModel.CleanOnlyPlay().Profit;
            float frugal = BalanceModel.FrugalPlay().Profit;
            float best = BalanceModel.BestPlay().Profit;

            Assert.That(untouched, Is.LessThan(cleanOnly), "Cleaning should help, even if not enough.");
            Assert.That(negligent, Is.LessThan(cleanOnly), "Wasting the budget should be worse than not spending it.");
            Assert.That(cleanOnly, Is.LessThan(frugal));
            Assert.That(frugal, Is.LessThan(best));
        }

        [Test]
        public void BestPlayIsRewardingButNotAbsurd()
        {
            // A perfect round should feel like a win without making the sale trivial.
            // The original numbers put this at +$70,000 because cleaning alone paid
            // $30,000 and design points paid $40 each.
            float profit = BalanceModel.BestPlay().Profit;

            Assert.That(profit, Is.GreaterThan(8000f), "A perfect round should pay well.");
            Assert.That(profit, Is.LessThan(30000f), "…but not so well that the round plays itself.");
        }

        // ==================================================================
        // The shopping decision (GDD 11, 15)
        // ==================================================================

        [Test]
        public void DoingEverythingCostsMoreThanTheBudgetAllows()
        {
            // This is what makes the catalog a decision. If the whole list fitted inside
            // the budget the optimal play would simply be "buy one of everything".
            float everything = BalanceModel.TotalRepairCost
                               + BalanceModel.FullPaintCost
                               + CatalogDefinition.TotalFurnitureCost;

            Assert.That(everything, Is.GreaterThan(GameConstants.StartingBudget),
                "The full shopping list must not fit in the budget.");
        }

        [Test]
        public void BestPlaySpendsNearlyTheWholeBudget()
        {
            // If the optimum leaves a lot unspent, the budget is not really a constraint.
            float spend = BalanceModel.BestPlay().Spend;

            Assert.That(spend, Is.GreaterThan(GameConstants.StartingBudget * 0.9f));
            Assert.That(spend, Is.LessThanOrEqualTo(GameConstants.StartingBudget));
        }

        [Test]
        public void CatalogValueRatiosVary()
        {
            // A catalog where every item has the same value-per-dollar makes the choice
            // arbitrary. There has to be a spread for shopping to be a skill.
            float min = float.MaxValue, max = float.MinValue;

            foreach (PlaceableDefinition item in CatalogDefinition.Furniture)
            {
                float ratio = item.EffectiveValue(GameConstants.DesignPointValue) / item.Cost;
                min = Mathf.Min(min, ratio);
                max = Mathf.Max(max, ratio);
            }

            Assert.That(max - min, Is.GreaterThan(0.25f),
                "Catalog value ratios should differ enough to make choosing matter.");
        }

        [Test]
        public void EveryCatalogItemIsWorthMoreThanItCosts()
        {
            // An item that loses money on purchase is a trap, not a decision — nobody
            // would ever buy it and it would just clutter the menu.
            foreach (PlaceableDefinition item in CatalogDefinition.Furniture)
            {
                Assert.That(item.EffectiveValue(GameConstants.DesignPointValue),
                    Is.GreaterThan(item.Cost), $"{item.DisplayName} costs more than it is worth.");
            }
        }

        [Test]
        public void BuildingPiecesDoNotOutEarnFurniture()
        {
            // Build pieces exist to repair your own demolition. If they returned more per
            // dollar than furniture, optimal play would be to spam wall segments — which
            // is exactly what an earlier pass of these numbers produced.
            float bestBuilding = 0f;
            foreach (PlaceableDefinition item in CatalogDefinition.Buildings)
            {
                bestBuilding = Mathf.Max(bestBuilding,
                    item.EffectiveValue(GameConstants.DesignPointValue) / item.Cost);
            }

            float bestFurniture = 0f;
            foreach (PlaceableDefinition item in CatalogDefinition.Furniture)
            {
                bestFurniture = Mathf.Max(bestFurniture,
                    item.EffectiveValue(GameConstants.DesignPointValue) / item.Cost);
            }

            Assert.That(bestBuilding, Is.LessThan(bestFurniture),
                "No building piece should be the single most profitable thing to buy.");
        }

        // ==================================================================
        // Value cannot be farmed (GDD 16)
        // ==================================================================

        [Test]
        public void PerRoomCapsExistToStopValueFarming()
        {
            // Every catalog item is worth more than it costs, which means without a cap
            // you could place the same item forever for unbounded profit.
            Assert.That(GameConstants.MaxFurnitureValuePerRoom, Is.GreaterThan(0f));
            Assert.That(GameConstants.MaxDesignPointsPerRoom, Is.GreaterThan(0f));
        }

        [Test]
        public void TheFurnitureCapIsGenerousEnoughForAnHonestRoom()
        {
            // The cap must not bite during normal play, or furnishing a room properly
            // would feel arbitrarily punished. A full room of its own category should fit.
            float livingRoom = 0f;
            foreach (PlaceableDefinition item in CatalogDefinition.Furniture)
            {
                if (item.Category == Furniture.FurnitureCategory.LivingRoom)
                {
                    livingRoom += item.ValueContribution;
                }
            }

            Assert.That(livingRoom, Is.LessThanOrEqualTo(GameConstants.MaxFurnitureValuePerRoom),
                "Furnishing one room fully should not hit the anti-farming cap.");
        }

        // ==================================================================
        // Time budget (GDD 3)
        // ==================================================================

        [Test]
        public void TheHouseFitsInsideTheRoundForASoloPlayer()
        {
            // A solo player must be able to finish. If the content overran the clock, the
            // single-player experience would be unwinnable rather than merely hard.
            Assert.That(BalanceModel.WorkSecondsFor(1),
                Is.LessThan(GameConstants.SessionSeconds),
                "One player must be able to complete the house inside the round.");
        }

        [Test]
        public void MorePlayersMeansFasterWork()
        {
            // GDD 2: "More players = faster work AND more chaos".
            Assert.That(BalanceModel.WorkSecondsFor(4), Is.LessThan(BalanceModel.WorkSecondsFor(1)));
        }

        /// <summary>
        /// Documents a known shortfall rather than asserting it away.
        ///
        /// The GDD fixes the round at 25 minutes, but one house of this size is only about
        /// four minutes of work for four players. The content is thin for a full team, and
        /// the honest fixes are a shorter round or a bigger house — both design calls, so
        /// this test records the fact instead of hiding it.
        /// </summary>
        [Test]
        public void FourPlayersFinishWellInsideTheRound_KnownContentShortfall()
        {
            float utilisation = BalanceModel.RoundUtilisation(4);

            Assert.That(utilisation, Is.LessThan(0.5f),
                "If this starts failing the house grew — good, update the note in Tools/README.md.");
        }
    }
}
