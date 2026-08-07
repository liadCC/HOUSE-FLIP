using HouseFlip.Core;
using UnityEngine;

namespace HouseFlip.Building
{
    /// <summary>
    /// The one implementation of "can this go here", shared by the client preview and the
    /// server's authoritative check. Keeping it in one place is what stops the green ghost
    /// from lying to the player.
    /// </summary>
    public static class PlacementValidator
    {
        private static readonly Collider[] OverlapBuffer = new Collider[16];

        public static bool IsBlocked(PlaceableData data, Vector3 position, Quaternion rotation, GameObject ignoreRoot = null)
        {
            if (data == null)
            {
                return true;
            }

            Vector3 size = GridUtility.FootprintToWorldSize(data.size, data.height);

            // Shrink slightly so pieces can sit flush against a wall or each other without
            // reporting a collision on the shared face.
            Vector3 halfExtents = size * 0.5f * 0.92f;
            Vector3 center = position + rotation * new Vector3(0f, halfExtents.y, 0f);

            int count = Physics.OverlapBoxNonAlloc(
                center, halfExtents, OverlapBuffer, rotation,
                GameLayers.PlacementBlockerMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider collider = OverlapBuffer[i];
                if (collider == null)
                {
                    continue;
                }

                if (ignoreRoot != null && collider.transform.IsChildOf(ignoreRoot.transform))
                {
                    continue;
                }

                // Standing on the floor you are placing onto is not an obstruction.
                if (IsGroundLike(collider))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool IsGroundLike(Collider collider)
        {
            // Large, flat, downward-facing surfaces are floors — placing on them is the point.
            Bounds bounds = collider.bounds;
            return bounds.size.y < 0.35f && bounds.size.x > 1.5f && bounds.size.z > 1.5f;
        }

        /// <summary>Server-side entry point: same test, no ghost to ignore.</summary>
        public static bool IsValidServerPlacement(PlaceableData data, Vector3 position, Quaternion rotation)
        {
            if (data == null || data.prefab == null)
            {
                return false;
            }

            return !IsBlocked(data, position, rotation);
        }
    }
}
