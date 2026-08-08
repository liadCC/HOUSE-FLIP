using HouseFlip.Core;
using HouseFlip.Economy;
using NUnit.Framework;

namespace HouseFlip.Tests
{
    /// <summary>
    /// Exercises the live economy systems rather than the value structs: the shared
    /// wallet's spending rules (GDD 15), how a room derives its four sub-scores
    /// (GDD 17), and how the house value is assembled from the rooms (GDD 16).
    ///
    /// These run the real components with IsServer forced on, which is the precondition
    /// for every authoritative path in them.
    /// </summary>
    [TestFixture]
    public class EconomySystemTests
    {
        [SetUp]
        public void Reset()
        {
            // Static registries and the event bus survive between tests otherwise.
            RoomRegistry.Clear();
            GameEvents.ResetAll();
        }

        [TearDown]
        public void Cleanup()
        {
            RoomRegistry.Clear();
            GameEvents.ResetAll();
        }

        private static BudgetManager ServerBudget()
        {
            var budget = new BudgetManager { IsServer = true };
            budget.ServerResetForNewRound();
            return budget;
        }

        // ==================================================================
        // Shared budget (GDD 15)
        // ==================================================================

        [Test]
        public void Budget_StartsAtTheGdgFigure()
        {
            Assert.That(ServerBudget().Current, Is.EqualTo(GameConstants.StartingBudget));
        }

        [Test]
        public void Spending_DeductsFromTheSharedWallet()
        {
            BudgetManager budget = ServerBudget();

            bool ok = budget.TrySpend(1500f, 0, SpendCategory.Furniture);

            Assert.That(ok, Is.True);
            Assert.That(budget.Current, Is.EqualTo(GameConstants.StartingBudget - 1500f));
        }

        [Test]
        public void Spending_MoreThanTheBudgetIsRefusedAndChangesNothing()
        {
            // GDD 15: purchases are blocked rather than pushing the budget negative.
            BudgetManager budget = ServerBudget();
            float before = budget.Current;

            bool ok = budget.TrySpend(GameConstants.StartingBudget + 1f, 0, SpendCategory.Renovation);

            Assert.That(ok, Is.False);
            Assert.That(budget.Current, Is.EqualTo(before), "A refused purchase must not move the budget.");
            Assert.That(budget.TotalSpent, Is.EqualTo(0f));
        }

        [Test]
        public void Spending_ExactlyTheRemainingBudgetIsAllowed()
        {
            // The boundary: spending the last cent should succeed, not be off-by-one.
            BudgetManager budget = ServerBudget();

            Assert.That(budget.TrySpend(GameConstants.StartingBudget, 0, SpendCategory.Renovation), Is.True);
            Assert.That(budget.Current, Is.EqualTo(0f));
        }

        [Test]
        public void Spending_IsBlockedOnceTheWalletIsEmpty()
        {
            BudgetManager budget = ServerBudget();
            budget.TrySpend(GameConstants.StartingBudget, 0, SpendCategory.Renovation);

            Assert.That(budget.TrySpend(1f, 0, SpendCategory.Furniture), Is.False);
        }

        [Test]
        public void Spending_RefusedPurchaseTellsOnlyTheBuyer()
        {
            BudgetManager budget = ServerBudget();
            string reason = null;
            GameEvents.PurchaseRejected += r => reason = r;

            budget.TrySpend(999999f, 3, SpendCategory.Furniture);

            Assert.That(reason, Is.EqualTo("No Budget!"), "GDD 15 names this warning explicitly.");
        }

        [Test]
        public void Spending_SplitsRenovationFromFurnitureForTheResultsScreen()
        {
            // GDD 19 prints these as separate lines, so they must be tracked separately.
            BudgetManager budget = ServerBudget();

            budget.TrySpend(3000f, 0, SpendCategory.Renovation);
            budget.TrySpend(1200f, 0, SpendCategory.Furniture);

            Assert.That(budget.SpentOnRenovation, Is.EqualTo(3000f));
            Assert.That(budget.SpentOnFurniture, Is.EqualTo(1200f));
            Assert.That(budget.TotalSpent, Is.EqualTo(4200f));
        }

        [Test]
        public void Refund_RestoresBudgetAndUnwindsTheSpendTotal()
        {
            BudgetManager budget = ServerBudget();
            budget.TrySpend(1000f, 0, SpendCategory.Furniture);

            budget.Refund(600f, SpendCategory.Furniture);

            Assert.That(budget.Current, Is.EqualTo(GameConstants.StartingBudget - 400f));
            Assert.That(budget.SpentOnFurniture, Is.EqualTo(400f));
        }

        [Test]
        public void Refund_NeverDrivesTheSpendTotalNegative()
        {
            BudgetManager budget = ServerBudget();

            budget.Refund(5000f, SpendCategory.Renovation);

            Assert.That(budget.SpentOnRenovation, Is.EqualTo(0f));
        }

        [Test]
        public void Spending_NegativeAmountsAreRejected()
        {
            // A negative "cost" would otherwise be free money.
            BudgetManager budget = ServerBudget();

            Assert.That(budget.TrySpend(-500f, 0, SpendCategory.Furniture), Is.False);
            Assert.That(budget.Current, Is.EqualTo(GameConstants.StartingBudget));
        }

        [Test]
        public void Clients_CannotSpendTheSharedWallet()
        {
            // GDD 22: exactly one source of truth. A non-server caller must be refused.
            var clientSide = new BudgetManager { IsServer = false };

            Assert.That(clientSide.TrySpend(10f, 0, SpendCategory.Furniture), Is.False);
        }

        // ==================================================================
        // Room sub-scores (GDD 17)
        // ==================================================================

        private static RoomController Room()
        {
            // Awake is skipped deliberately: it wires a BoxCollider this harness has no
            // way to provide, and none of the scoring depends on the room's bounds.
            return new RoomController { IsServer = true };
        }

        [Test]
        public void RoomCleanliness_MapsDirectlyOntoTheSubScore()
        {
            RoomController room = Room();
            room.Cleanliness.Value = 0.9f;

            Assert.That(room.BuildScore().Cleanliness, Is.EqualTo(90f).Within(0.01f));
        }

        [Test]
        public void RoomFurniture_ReachesFullScoreAtTheTargetCount()
        {
            RoomController room = Room();

            room.FurnitureCount.Value = GameConstants.FurnitureTargetPerRoom;
            Assert.That(room.BuildScore().Furniture, Is.EqualTo(100f).Within(0.01f));

            room.FurnitureCount.Value = 0;
            Assert.That(room.BuildScore().Furniture, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void RoomFurniture_OverfillingDoesNotScorePastFull()
        {
            // Cramming twenty sofas into the bathroom should not beat a tasteful five.
            RoomController room = Room();
            room.FurnitureCount.Value = GameConstants.FurnitureTargetPerRoom * 4;

            Assert.That(room.BuildScore().Furniture, Is.EqualTo(100f).Within(0.01f));
        }

        [Test]
        public void RoomDesign_ReachesFullScoreAtTheTargetPoints()
        {
            RoomController room = Room();
            room.DesignPoints.Value = GameConstants.DesignTargetPerRoom;

            Assert.That(room.BuildScore().Design, Is.EqualTo(100f).Within(0.01f));
        }

        [Test]
        public void RoomCondition_IsPerfectWhenNothingIsBroken()
        {
            // An untouched room has no fixtures registered and nothing smashed.
            Assert.That(Room().BuildScore().Condition, Is.EqualTo(100f).Within(0.01f));
        }

        [Test]
        public void RoomScore_CombinesSubScoresUsingTheGddWeights()
        {
            RoomController room = Room();
            room.Cleanliness.Value = 1f;
            room.FurnitureCount.Value = GameConstants.FurnitureTargetPerRoom;
            room.DesignPoints.Value = GameConstants.DesignTargetPerRoom;

            Assert.That(room.BuildScore().Total, Is.EqualTo(100f).Within(0.01f),
                "A spotless, fully furnished, well designed, undamaged room scores 100.");
        }

        [Test]
        public void UnresolvedLeakPenalty_DragsCleanlinessDown()
        {
            // GDD 21: an unfixed water leak leaves a permanent cleanliness penalty.
            RoomController room = Room();
            room.Cleanliness.Value = 1f;

            room.ApplyCleanlinessPenalty(GameConstants.UnresolvedLeakCleanlinessPenalty);

            Assert.That(room.Cleanliness.Value, Is.LessThan(1f));
            Assert.That(room.CleanlinessPenalty.Value,
                Is.EqualTo(GameConstants.UnresolvedLeakCleanlinessPenalty).Within(0.001f));
        }

        [Test]
        public void CleanlinessPenalty_CannotPushTheRoomBelowZero()
        {
            RoomController room = Room();

            room.ApplyCleanlinessPenalty(5f);

            Assert.That(room.Cleanliness.Value, Is.EqualTo(0f));
        }

        // ==================================================================
        // House value assembly (GDD 16)
        // ==================================================================

        private static HouseValueManager ServerHouseValue()
        {
            var manager = new HouseValueManager { IsServer = true };
            manager.ServerResetForNewRound();
            return manager;
        }

        [Test]
        public void HouseValue_StartsAtTheBaseValueWithNoRooms()
        {
            Assert.That(ServerHouseValue().BuildBreakdown().FinalValue,
                Is.EqualTo(GameConstants.HouseBaseValue).Within(0.01f));
        }

        [Test]
        public void HouseValue_AddsUpToFiveThousandPerSpotlessRoom()
        {
            // GDD 16: room cleanliness is worth up to $5,000 per room.
            RoomController room = Room();
            room.Cleanliness.Value = 1f;
            RoomRegistry.Register(room);

            HouseValueBreakdown breakdown = ServerHouseValue().BuildBreakdown();

            Assert.That(breakdown.CleanlinessBonus,
                Is.EqualTo(GameConstants.MaxCleanlinessValuePerRoom).Within(0.01f));
        }

        [Test]
        public void HouseValue_ScalesCleanlinessBonusAcrossRooms()
        {
            for (int i = 0; i < 3; i++)
            {
                RoomController room = Room();
                room.Cleanliness.Value = 1f;
                RoomRegistry.Register(room);
            }

            Assert.That(ServerHouseValue().BuildBreakdown().CleanlinessBonus,
                Is.EqualTo(GameConstants.MaxCleanlinessValuePerRoom * 3f).Within(0.01f));
        }

        [Test]
        public void HouseValue_CountsCompletedRepairs()
        {
            HouseValueManager manager = ServerHouseValue();

            manager.OnRepairComplete(900f);
            manager.OnRepairComplete(1300f);

            Assert.That(manager.BuildBreakdown().RenovationBonus, Is.EqualTo(2200f).Within(0.01f));
        }

        [Test]
        public void HouseValue_ClawsBackTheBonusWhenFurnitureIsRemoved()
        {
            // Selling a sofa back must not leave its value baked into the house.
            RoomController room = Room();
            RoomRegistry.Register(room);

            room.AddFurnitureValue(2600f);
            room.AddFurnitureValue(-2600f);

            Assert.That(ServerHouseValue().BuildBreakdown().FurnitureBonus, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void HouseValue_FurnitureBonusNeverGoesNegative()
        {
            RoomController room = Room();
            RoomRegistry.Register(room);

            room.AddFurnitureValue(-5000f);

            Assert.That(room.FurnitureValue.Value, Is.EqualTo(0f));
        }

        [Test]
        public void HouseValue_FurnitureIsCappedPerRoomToStopFarming()
        {
            // Every catalog item is worth more than it costs, so without this cap a player
            // could place the same cabinet forever and print money.
            RoomController room = Room();
            RoomRegistry.Register(room);

            for (int i = 0; i < 50; i++)
            {
                room.AddFurnitureValue(1000f);
            }

            Assert.That(room.FurnitureValue.Value,
                Is.EqualTo(GameConstants.MaxFurnitureValuePerRoom).Within(0.01f));
        }

        [Test]
        public void DesignPointsAreCappedPerRoomToStopRepaintFarming()
        {
            RoomController room = Room();

            for (int i = 0; i < 100; i++)
            {
                room.AddDesignPoints(25f);
            }

            Assert.That(room.DesignPoints.Value,
                Is.EqualTo(GameConstants.MaxDesignPointsPerRoom).Within(0.01f));
        }

        [Test]
        public void HouseValue_ConvertsDesignPointsAtTheConfiguredRate()
        {
            RoomController room = Room();
            room.DesignPoints.Value = 50f;
            RoomRegistry.Register(room);

            Assert.That(ServerHouseValue().BuildBreakdown().DesignBonus,
                Is.EqualTo(50f * GameConstants.DesignPointValue).Within(0.01f));
        }

        [Test]
        public void HouseValue_ResetForNewRoundDropsAccumulatedBonuses()
        {
            // Starting a second round must not inherit the first round's renovation.
            HouseValueManager manager = ServerHouseValue();
            manager.OnRepairComplete(5000f);

            manager.ServerResetForNewRound();

            Assert.That(manager.BuildBreakdown().RenovationBonus, Is.EqualTo(0f));
        }

        [Test]
        public void Clients_CannotMoveTheHouseValue()
        {
            var clientSide = new HouseValueManager { IsServer = false };

            clientSide.OnRepairComplete(9999f);

            Assert.That(clientSide.BuildBreakdown().RenovationBonus, Is.EqualTo(0f),
                "Only the server accumulates value (GDD 22).");
        }
    }
}
