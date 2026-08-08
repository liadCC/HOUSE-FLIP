using HouseFlip.Player;
using UnityEngine;

namespace HouseFlip.Polish
{
    /// <summary>
    /// One entry point for "something happened over there" feedback (GDD 18 — Polish).
    ///
    /// Everything here is local and cosmetic. Gameplay code calls it from inside a
    /// ClientRpc, so all four players get the same shake and the same shower of rubble
    /// without any of it being simulated on the wire.
    ///
    /// Shake falls off with distance: a wall coming down on the far side of the house
    /// should register as a rumble, not the same jolt as one swung next to your head.
    /// </summary>
    public static class ImpactFeedback
    {
        /// <summary>Beyond this many metres an impact produces no shake at all.</summary>
        private const float FalloffRange = 18f;

        public static void Play(Vector3 position, float trauma, Color debrisColor,
            int debrisPieces = 0, float debrisForce = 3.5f)
        {
            CameraShake.Shake(ScaleByDistance(position, trauma));

            if (debrisPieces > 0)
            {
                DebrisBurst.Spawn(position, debrisColor, debrisPieces, debrisForce);
            }
        }

        /// <summary>Shake with no debris — throws, placements, UI-adjacent thumps.</summary>
        public static void Shake(Vector3 position, float trauma)
        {
            CameraShake.Shake(ScaleByDistance(position, trauma));
        }

        /// <summary>Shake that ignores distance, for things that happen to *you*.</summary>
        public static void ShakeLocal(float trauma)
        {
            CameraShake.Shake(trauma);
        }

        private static float ScaleByDistance(Vector3 position, float trauma)
        {
            PlayerController local = PlayerRegistry.LocalPlayer;
            if (local == null)
            {
                return trauma;
            }

            float distance = Vector3.Distance(local.transform.position, position);
            if (distance >= FalloffRange)
            {
                return 0f;
            }

            // Quadratic falloff: close hits stay punchy, distant ones fade off quickly
            // rather than trailing a long tail of low-grade wobble.
            float t = 1f - (distance / FalloffRange);
            return trauma * t * t;
        }
    }
}
