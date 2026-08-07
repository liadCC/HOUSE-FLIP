using HouseFlip.Core;
using HouseFlip.Player;
using UnityEngine;

namespace HouseFlip.Networking
{
    /// <summary>
    /// Server-side sanity checks shared by every action RPC in the game.
    ///
    /// Anything a client sends is a claim, not a fact: this re-derives the sender's
    /// position and equipped tool from server state before an action is allowed.
    /// </summary>
    public static class ActorValidator
    {
        /// <summary>Slack added to the range check so honest players are not punished for latency.</summary>
        public const float RangeTolerance = 1.75f;

        public static bool Validate(ulong clientId, Vector3 targetPosition, ToolType requiredTool,
            out PlayerController player)
        {
            player = PlayerRegistry.Get(clientId);
            if (player == null)
            {
                return false;
            }

            if (!PlayerController.InputEnabled)
            {
                return false;
            }

            if (Vector3.Distance(player.transform.position, targetPosition)
                > GameConstants.InteractRange + RangeTolerance)
            {
                return false;
            }

            if (player.Tools == null)
            {
                return requiredTool == ToolType.None;
            }

            return player.Tools.CurrentTool == requiredTool;
        }
    }
}
