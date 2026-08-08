using System.Collections.Generic;
using System.Linq;
using HouseFlip.Core;
using HouseFlip.GameFlow;
using NUnit.Framework;

namespace HouseFlip.Tests
{
    /// <summary>
    /// The awards table in GDD 20, plus the two properties the screen depends on:
    /// every player gets exactly one award, and no award is handed out twice.
    /// </summary>
    [TestFixture]
    public class AwardsTests
    {
        private static AwardsCalculator.AwardCandidate Player(ulong id, string name,
            params (PlayerStat stat, float value)[] stats)
        {
            var values = new float[PlayerStatExtensions.Count];
            foreach ((PlayerStat stat, float value) in stats)
            {
                values[(int)stat] = value;
            }

            return new AwardsCalculator.AwardCandidate(id, name, values);
        }

        private static string AwardFor(PlayerAward[] awards, ulong clientId)
        {
            foreach (PlayerAward award in awards)
            {
                if (award.ClientId == clientId)
                {
                    return award.AwardName.ToString();
                }
            }

            return null;
        }

        [Test]
        public void WreckingBall_GoesToMostObjectsDestroyed()
        {
            var candidates = new List<AwardsCalculator.AwardCandidate>
            {
                Player(0, "Alex", (PlayerStat.ObjectsDestroyed, 3f)),
                Player(1, "Sam", (PlayerStat.ObjectsDestroyed, 11f)),
                Player(2, "Riley", (PlayerStat.ObjectsDestroyed, 0f))
            };

            PlayerAward[] awards = AwardsCalculator.Build(candidates, teamMadeALoss: false);

            Assert.That(AwardFor(awards, 1), Is.EqualTo("WRECKING BALL"));
        }

        [Test]
        public void MoneySaver_GoesToLeastSpent()
        {
            var candidates = new List<AwardsCalculator.AwardCandidate>
            {
                Player(0, "Alex", (PlayerStat.MoneySpent, 9000f)),
                Player(1, "Sam", (PlayerStat.MoneySpent, 120f))
            };

            PlayerAward[] awards = AwardsCalculator.Build(candidates, teamMadeALoss: false);

            // Sam spent least, but higher-priority awards are checked first — with no
            // other stats set, MONEY SAVER is the only one anyone qualifies for.
            Assert.That(AwardFor(awards, 1), Is.EqualTo("MONEY SAVER"));
        }

        [Test]
        public void EveryPlayerReceivesExactlyOneAward()
        {
            var candidates = new List<AwardsCalculator.AwardCandidate>
            {
                Player(0, "Alex", (PlayerStat.ObjectsDestroyed, 5f), (PlayerStat.MoneySpent, 1000f)),
                Player(1, "Sam", (PlayerStat.DirtCleaned, 8f), (PlayerStat.MoneySpent, 2000f)),
                Player(2, "Riley", (PlayerStat.HouseValueAdded, 9000f), (PlayerStat.MoneySpent, 5000f)),
                Player(3, "Jo", (PlayerStat.DesignPointsAdded, 60f), (PlayerStat.MoneySpent, 800f))
            };

            PlayerAward[] awards = AwardsCalculator.Build(candidates, teamMadeALoss: false);

            for (ulong id = 0; id < 4; id++)
            {
                ulong captured = id;
                Assert.That(awards.Count(a => a.ClientId == captured), Is.EqualTo(1),
                    $"Player {captured} should receive exactly one award.");
            }
        }

        [Test]
        public void NoAwardIsHandedOutTwice()
        {
            var candidates = new List<AwardsCalculator.AwardCandidate>
            {
                Player(0, "Alex", (PlayerStat.ObjectsDestroyed, 5f)),
                Player(1, "Sam", (PlayerStat.ObjectsDestroyed, 4f)),
                Player(2, "Riley", (PlayerStat.ObjectsDestroyed, 3f)),
                Player(3, "Jo", (PlayerStat.ObjectsDestroyed, 2f))
            };

            PlayerAward[] awards = AwardsCalculator.Build(candidates, teamMadeALoss: false);

            List<string> names = awards.Select(a => a.AwardName.ToString())
                                       .Where(n => n != "SAFE PAIR OF HANDS")
                                       .ToList();

            Assert.That(names.Distinct().Count(), Is.EqualTo(names.Count),
                "Each individual award may only be won once.");
        }

        [Test]
        public void TeamDisaster_IsAddedOnlyWhenTheRoundLosesMoney()
        {
            var candidates = new List<AwardsCalculator.AwardCandidate>
            {
                Player(0, "Alex", (PlayerStat.ObjectsDestroyed, 2f))
            };

            PlayerAward[] profitable = AwardsCalculator.Build(candidates, teamMadeALoss: false);
            PlayerAward[] loss = AwardsCalculator.Build(candidates, teamMadeALoss: true);

            Assert.That(profitable.Any(a => a.AwardName.ToString() == "TEAM DISASTER"), Is.False);
            Assert.That(loss.Any(a => a.AwardName.ToString() == "TEAM DISASTER"), Is.True);
        }

        [Test]
        public void PlayerWhoDidNothing_StillGetsAnAward()
        {
            // GDD 20 gives every player an award. Someone who touched nothing must not
            // fall through the priority list and end up with a blank row.
            var candidates = new List<AwardsCalculator.AwardCandidate>
            {
                Player(0, "Alex", (PlayerStat.ObjectsDestroyed, 4f), (PlayerStat.MoneySpent, 100f)),
                Player(1, "Idle")
            };

            PlayerAward[] awards = AwardsCalculator.Build(candidates, teamMadeALoss: false);

            Assert.That(AwardFor(awards, 1), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void EmptyLobby_ProducesNoAwardsButDoesNotThrow()
        {
            PlayerAward[] awards = AwardsCalculator.Build(
                new List<AwardsCalculator.AwardCandidate>(), teamMadeALoss: false);

            Assert.That(awards, Is.Empty);
        }

        [Test]
        public void OverlongPlayerName_IsTrimmedRatherThanThrowing()
        {
            // Names arrive from clients; FixedString32Bytes throws if handed too much.
            var candidates = new List<AwardsCalculator.AwardCandidate>
            {
                Player(0, new string('x', 200), (PlayerStat.ObjectsDestroyed, 1f))
            };

            Assert.DoesNotThrow(() => AwardsCalculator.Build(candidates, teamMadeALoss: false));
        }

        [Test]
        public void LargeStatValues_DoNotOverflowTheDetailString()
        {
            // "$1,234,567,890 of value added" must still fit FixedString64Bytes.
            var candidates = new List<AwardsCalculator.AwardCandidate>
            {
                Player(0, "Tycoon", (PlayerStat.HouseValueAdded, 1234567890f))
            };

            Assert.DoesNotThrow(() => AwardsCalculator.Build(candidates, teamMadeALoss: true));
        }
    }
}
