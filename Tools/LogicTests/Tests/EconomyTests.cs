using HouseFlip.Core;
using HouseFlip.Economy;
using HouseFlip.GameFlow;
using NUnit.Framework;

namespace HouseFlip.Tests
{
    /// <summary>
    /// Checks the economy maths against the worked examples printed in GDD 16, 17 and 19.
    /// If a balance number changes these should be updated deliberately, not silently.
    /// </summary>
    [TestFixture]
    public class EconomyTests
    {
        // ------------------------------------------------------------------
        // Room score (GDD 17)
        // ------------------------------------------------------------------

        [Test]
        public void RoomScore_MatchesWorkedExampleFromGdd()
        {
            // GDD 17 prints this exact room and calls the result 86 (rounded from 85.5).
            var score = new RoomScore
            {
                RoomName = "LIVING ROOM",
                Cleanliness = 90f,
                Furniture = 80f,
                Design = 70f,
                Condition = 100f
            };

            Assert.That(score.Total, Is.EqualTo(85.5f).Within(0.001f));
        }

        [Test]
        public void RoomScore_WeightsSumToOne()
        {
            // A perfect room must score exactly 100, which only holds if the four
            // weights add up. This is the invariant behind the formula in GDD 17.
            float sum = GameConstants.WeightCleanliness + GameConstants.WeightFurniture
                        + GameConstants.WeightDesign + GameConstants.WeightCondition;

            Assert.That(sum, Is.EqualTo(1f).Within(0.0001f));

            var perfect = new RoomScore
            {
                Cleanliness = 100f, Furniture = 100f, Design = 100f, Condition = 100f
            };

            Assert.That(perfect.Total, Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void RoomScore_IsClampedToRange()
        {
            var wrecked = new RoomScore
            {
                Cleanliness = 0f, Furniture = 0f, Design = -500f, Condition = 0f
            };

            Assert.That(wrecked.Total, Is.EqualTo(0f), "A room score must never go negative.");
        }

        // ------------------------------------------------------------------
        // House value (GDD 16, 19)
        // ------------------------------------------------------------------

        [Test]
        public void HouseValue_MatchesInspectionScreenExample()
        {
            // The inspection screen mocked up in GDD 19.
            var breakdown = new HouseValueBreakdown
            {
                BaseValue = 50000f,
                RenovationBonus = 18000f,
                FurnitureBonus = 0f,
                DesignBonus = 12000f,
                CleanlinessBonus = 5000f,
                DamagePenalty = 3000f
            };

            Assert.That(breakdown.FinalValue, Is.EqualTo(82000f).Within(0.01f));
        }

        [Test]
        public void HouseValue_NeverGoesNegative()
        {
            var catastrophe = new HouseValueBreakdown
            {
                BaseValue = 50000f,
                DamagePenalty = 999999f
            };

            Assert.That(catastrophe.FinalValue, Is.EqualTo(0f),
                "A thoroughly destroyed house is worth nothing, not a negative amount.");
        }

        // ------------------------------------------------------------------
        // Profit (GDD 19)
        // ------------------------------------------------------------------

        [Test]
        public void Profit_MatchesSellScreenExample()
        {
            // GDD 19: investment 75,000 against a final value of 82,000 is +7,000.
            var report = new InspectionReport
            {
                Value = new HouseValueBreakdown
                {
                    BaseValue = 50000f,
                    RenovationBonus = 18000f,
                    DesignBonus = 12000f,
                    CleanlinessBonus = 5000f,
                    DamagePenalty = 3000f
                },
                HousePurchasePrice = 50000f,
                RenovationSpent = 18000f,
                FurnitureSpent = 7000f
            };

            Assert.That(report.TotalInvestment, Is.EqualTo(75000f).Within(0.01f));
            Assert.That(report.FinalValue, Is.EqualTo(82000f).Within(0.01f));
            Assert.That(report.Profit, Is.EqualTo(7000f).Within(0.01f));
            Assert.That(report.IsProfitable, Is.True);
        }

        [Test]
        public void Profit_MatchesChaosScenarioLoss()
        {
            // The disaster ending in GDD 28: 43,000 value against 55,000 invested.
            var report = new InspectionReport
            {
                Value = new HouseValueBreakdown { BaseValue = 43000f },
                HousePurchasePrice = 50000f,
                RenovationSpent = 4000f,
                FurnitureSpent = 1000f
            };

            Assert.That(report.Profit, Is.EqualTo(-12000f).Within(0.01f));
            Assert.That(report.IsProfitable, Is.False);
        }

        [Test]
        public void Profit_OfExactlyZeroIsNotProfitable()
        {
            // Breaking even must trigger TEAM DISASTER's sibling path, not a celebration.
            var report = new InspectionReport
            {
                Value = new HouseValueBreakdown { BaseValue = 50000f },
                HousePurchasePrice = 50000f
            };

            Assert.That(report.Profit, Is.EqualTo(0f));
            Assert.That(report.IsProfitable, Is.False);
        }

        // ------------------------------------------------------------------
        // Formatting
        // ------------------------------------------------------------------

        [Test]
        public void Money_FormatsWithThousandsSeparators()
        {
            Assert.That(InspectionReport.Money(82000f), Is.EqualTo("$82,000"));
            Assert.That(InspectionReport.Money(-12000f), Is.EqualTo("-$12,000"));
        }

        [Test]
        public void SignedMoney_AlwaysShowsDirection()
        {
            Assert.That(InspectionReport.SignedMoney(7000f), Is.EqualTo("+$7,000"));
            Assert.That(InspectionReport.SignedMoney(-3000f), Is.EqualTo("-$3,000"));
            Assert.That(InspectionReport.SignedMoney(0f), Is.EqualTo("+$0"));
        }

        // ------------------------------------------------------------------
        // Session constants
        // ------------------------------------------------------------------

        [Test]
        public void Session_IsTwentyFiveMinutes()
        {
            Assert.That(GameConstants.SessionSeconds, Is.EqualTo(1500f), "GDD 18 fixes the round at 25:00.");
        }

        [Test]
        public void StartingBudgetAndHouseValue_MatchTheGdd()
        {
            Assert.That(GameConstants.StartingBudget, Is.EqualTo(20000f));
            Assert.That(GameConstants.HouseBaseValue, Is.EqualTo(50000f));
            Assert.That(GameConstants.HousePurchasePrice, Is.EqualTo(50000f));
        }

        [Test]
        public void Timer_FormatsAsMinutesAndSeconds()
        {
            Assert.That(TimerManager.Format(GameConstants.SessionSeconds), Is.EqualTo("25:00"));
            Assert.That(TimerManager.Format(0f), Is.EqualTo("00:00"));
            Assert.That(TimerManager.Format(59.9f), Is.EqualTo("00:59"));
            Assert.That(TimerManager.Format(61f), Is.EqualTo("01:01"));
        }

        [Test]
        public void Timer_ClampsNegativeTimeToZero()
        {
            // Floating point drift can push the countdown a hair below zero.
            Assert.That(TimerManager.Format(-5f), Is.EqualTo("00:00"));
        }
    }
}
