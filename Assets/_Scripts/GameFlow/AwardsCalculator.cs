using System.Collections.Generic;
using HouseFlip.Core;
using HouseFlip.Player;
using Unity.Collections;
using UnityEngine;

namespace HouseFlip.GameFlow
{
    /// <summary>
    /// Hands out the humorous end-of-round awards (GDD 20). Server-side only.
    ///
    /// Each award goes to at most one player, and every player gets exactly one, so the
    /// awards are assigned in priority order and a player is removed from the pool once
    /// they have won something. A four-player round therefore always fills four rows.
    /// </summary>
    public static class AwardsCalculator
    {
        private readonly struct AwardDefinition
        {
            public readonly string Name;
            public readonly string Icon;
            public readonly PlayerStat Stat;
            public readonly bool HighestWins;
            public readonly string DetailFormat;

            public AwardDefinition(string name, string icon, PlayerStat stat, bool highestWins, string detailFormat)
            {
                Name = name;
                Icon = icon;
                Stat = stat;
                HighestWins = highestWins;
                DetailFormat = detailFormat;
            }
        }

        private static readonly AwardDefinition[] Definitions =
        {
            new AwardDefinition("WRECKING BALL", "🔨", PlayerStat.ObjectsDestroyed, true, "{0:0} objects destroyed"),
            new AwardDefinition("DISASTER", "💀", PlayerStat.DamageCaused, true, "{0:0} structural disasters"),
            new AwardDefinition("MVP", "🏆", PlayerStat.HouseValueAdded, true, "${0:N0} of value added"),
            new AwardDefinition("INTERIOR DESIGNER", "🎨", PlayerStat.DesignPointsAdded, true, "{0:0} design points"),
            new AwardDefinition("CLEAN FREAK", "🧹", PlayerStat.DirtCleaned, true, "{0:0.0} dirt scrubbed"),
            new AwardDefinition("MONEY SAVER", "💰", PlayerStat.MoneySpent, false, "only ${0:N0} spent")
        };

        public static PlayerAward[] Build(IReadOnlyDictionary<ulong, PlayerController> players, bool teamMadeALoss)
        {
            var results = new List<PlayerAward>();

            // GDD 20: a negative profit brands the whole team, individual awards aside.
            if (teamMadeALoss)
            {
                results.Add(new PlayerAward
                {
                    ClientId = ulong.MaxValue,
                    PlayerName = new FixedString32Bytes("THE TEAM"),
                    AwardName = new FixedString32Bytes("TEAM DISASTER"),
                    Icon = new FixedString32Bytes("🤡"),
                    Detail = new FixedString64Bytes("Sold the house for less than you paid")
                });
            }

            var unawarded = new List<PlayerController>();
            foreach (KeyValuePair<ulong, PlayerController> entry in players)
            {
                if (entry.Value != null && entry.Value.Stats != null)
                {
                    unawarded.Add(entry.Value);
                }
            }

            foreach (AwardDefinition definition in Definitions)
            {
                if (unawarded.Count == 0)
                {
                    break;
                }

                PlayerController winner = PickWinner(unawarded, definition);
                if (winner == null)
                {
                    continue;
                }

                unawarded.Remove(winner);

                float value = winner.Stats.Get(definition.Stat);
                results.Add(new PlayerAward
                {
                    ClientId = winner.OwnerClientId,
                    PlayerName = winner.Stats.DisplayName.Value,
                    AwardName = new FixedString32Bytes(definition.Name),
                    Icon = new FixedString32Bytes(definition.Icon),
                    Detail = new FixedString64Bytes(string.Format(definition.DetailFormat, value))
                });
            }

            // Anyone left standing did a bit of everything and nothing in particular.
            foreach (PlayerController leftover in unawarded)
            {
                results.Add(new PlayerAward
                {
                    ClientId = leftover.OwnerClientId,
                    PlayerName = leftover.Stats.DisplayName.Value,
                    AwardName = new FixedString32Bytes("SAFE PAIR OF HANDS"),
                    Icon = new FixedString32Bytes("🧰"),
                    Detail = new FixedString64Bytes("Broke nothing. Suspicious.")
                });
            }

            return results.ToArray();
        }

        private static PlayerController PickWinner(List<PlayerController> candidates, AwardDefinition definition)
        {
            PlayerController best = null;
            float bestValue = definition.HighestWins ? float.MinValue : float.MaxValue;

            foreach (PlayerController candidate in candidates)
            {
                float value = candidate.Stats.Get(definition.Stat);

                // "Most objects destroyed" is not an award if nobody destroyed anything.
                if (definition.HighestWins && value <= 0f)
                {
                    continue;
                }

                bool better = definition.HighestWins ? value > bestValue : value < bestValue;
                if (better)
                {
                    bestValue = value;
                    best = candidate;
                }
            }

            return best;
        }

        public static string Describe(PlayerAward award)
        {
            return $"{award.Icon}  {award.AwardName} — {award.PlayerName} ({award.Detail})";
        }

        /// <summary>Debug helper: prints the awards table to the console on the host.</summary>
        public static void LogAwards(PlayerAward[] awards)
        {
            if (awards == null || awards.Length == 0)
            {
                return;
            }

            var builder = new System.Text.StringBuilder("PLAYER AWARDS\n");
            foreach (PlayerAward award in awards)
            {
                builder.AppendLine(Describe(award));
            }

            Debug.Log(builder.ToString());
        }
    }
}
