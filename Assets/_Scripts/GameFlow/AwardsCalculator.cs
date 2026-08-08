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

        /// <summary>
        /// A player's end-of-round numbers, decoupled from the Unity component that
        /// produced them. This is what makes the award logic testable without a running
        /// scene — <see cref="Build(IReadOnlyList{AwardCandidate}, bool)"/> is a pure
        /// function over these.
        /// </summary>
        public readonly struct AwardCandidate
        {
            public readonly ulong ClientId;
            public readonly string Name;
            private readonly float[] _stats;

            public AwardCandidate(ulong clientId, string name, float[] stats)
            {
                ClientId = clientId;
                Name = string.IsNullOrEmpty(name) ? $"Player {clientId + 1}" : name;
                _stats = stats;
            }

            public float Get(PlayerStat stat)
            {
                int index = (int)stat;
                return _stats != null && index >= 0 && index < _stats.Length ? _stats[index] : 0f;
            }
        }

        /// <summary>Adapts live players into candidates, then defers to the pure core.</summary>
        public static PlayerAward[] Build(IReadOnlyDictionary<ulong, PlayerController> players, bool teamMadeALoss)
        {
            var candidates = new List<AwardCandidate>();

            foreach (KeyValuePair<ulong, PlayerController> entry in players)
            {
                PlayerController player = entry.Value;
                if (player == null || player.Stats == null)
                {
                    continue;
                }

                var stats = new float[PlayerStatExtensions.Count];
                for (int i = 0; i < stats.Length; i++)
                {
                    stats[i] = player.Stats.Get((PlayerStat)i);
                }

                candidates.Add(new AwardCandidate(
                    player.OwnerClientId, player.Stats.DisplayName.Value.ToString(), stats));
            }

            return Build(candidates, teamMadeALoss);
        }

        /// <summary>
        /// The award allocation itself (GDD 20). Each award goes to at most one player and
        /// each player receives exactly one, so awards are assigned in priority order and a
        /// winner drops out of the pool. A four-player round therefore always fills four rows.
        /// </summary>
        public static PlayerAward[] Build(IReadOnlyList<AwardCandidate> candidates, bool teamMadeALoss)
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

            var unawarded = new List<AwardCandidate>(candidates ?? new List<AwardCandidate>());

            foreach (AwardDefinition definition in Definitions)
            {
                if (unawarded.Count == 0)
                {
                    break;
                }

                int winner = PickWinner(unawarded, definition);
                if (winner < 0)
                {
                    continue;
                }

                AwardCandidate candidate = unawarded[winner];
                unawarded.RemoveAt(winner);

                float value = candidate.Get(definition.Stat);
                results.Add(new PlayerAward
                {
                    ClientId = candidate.ClientId,
                    PlayerName = new FixedString32Bytes(Trim(candidate.Name, 24)),
                    AwardName = new FixedString32Bytes(definition.Name),
                    Icon = new FixedString32Bytes(definition.Icon),
                    Detail = new FixedString64Bytes(
                        Trim(string.Format(definition.DetailFormat, value), 56))
                });
            }

            // Anyone left standing did a bit of everything and nothing in particular.
            foreach (AwardCandidate leftover in unawarded)
            {
                results.Add(new PlayerAward
                {
                    ClientId = leftover.ClientId,
                    PlayerName = new FixedString32Bytes(Trim(leftover.Name, 24)),
                    AwardName = new FixedString32Bytes("SAFE PAIR OF HANDS"),
                    Icon = new FixedString32Bytes("🧰"),
                    Detail = new FixedString64Bytes("Broke nothing. Suspicious.")
                });
            }

            return results.ToArray();
        }

        /// <summary>Index of the winning candidate, or -1 if the award goes unclaimed.</summary>
        private static int PickWinner(List<AwardCandidate> candidates, AwardDefinition definition)
        {
            int best = -1;
            float bestValue = definition.HighestWins ? float.MinValue : float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                float value = candidates[i].Get(definition.Stat);

                // "Most objects destroyed" is not an award if nobody destroyed anything.
                if (definition.HighestWins && value <= 0f)
                {
                    continue;
                }

                bool better = definition.HighestWins ? value > bestValue : value < bestValue;
                if (better)
                {
                    bestValue = value;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// FixedString32Bytes/FixedString64Bytes throw when handed more than they can
        /// hold. Player names come from clients, so they get clipped rather than trusted.
        /// </summary>
        private static string Trim(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Player";
            }

            return value.Length <= maxChars ? value : value.Substring(0, maxChars);
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
