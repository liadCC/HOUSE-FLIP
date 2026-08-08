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
        /// <summary>
        /// How far from the player a placement may land. Lives here rather than on the
        /// controller because the server enforces the same number: an inspector value that
        /// drifted above it would only ever paint a green ghost the server then refuses.
        /// </summary>
        public const float MaxPlacementDistance = 6f;

        private const float SupportProbeHeight = 0.25f;
        private const float SupportProbeDistance = 0.5f;

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
                if (IsGroundLike(collider, position.y))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool IsGroundLike(Collider collider, float placementBaseY)
        {
            // Large, flat, downward-facing surfaces are floors — placing on them is the point.
            Bounds bounds = collider.bounds;

            // The height test is what makes this a floor test rather than a shape test.
            // Shape alone exempts anything flat and wide *wherever it is*, so a rug, a low
            // table or a bed frame at waist height reads as "floor" and walls, cabinets and
            // furniture can be built straight through it. Only geometry that stops at or
            // below the base of the new object is something the object rests on.
            return bounds.size.y < 0.35f
                   && bounds.size.x > 1.5f && bounds.size.z > 1.5f
                   && bounds.max.y <= placementBaseY + 0.06f;
        }

        /// <summary>True when this item is only allowed to sit on the floor (GDD 10).</summary>
        public static bool RequiresFloorContact(PlaceableData data)
        {
            return data is BuildingData building && building.requiresFloorContact;
        }

        /// <summary>
        /// Probes straight down from just above the pivot for an upward-facing surface.
        ///
        /// The floor rule has to be re-derivable from the position alone, because that is
        /// all the placement RPC carries — the client's aim raycast does not survive the
        /// wire. Deriving it the same way on both sides is what stops "Needs Floor" from
        /// being a client-side suggestion the server never enforces.
        /// </summary>
        public static bool HasFloorSupport(Vector3 position)
        {
            var probe = new Ray(position + Vector3.up * SupportProbeHeight, -Vector3.up);

            return Physics.Raycast(probe, out RaycastHit hit, SupportProbeDistance,
                       GameLayers.PlacementSurfaceMask, QueryTriggerInteraction.Ignore)
                   && Vector3.Dot(hit.normal, Vector3.up) >= 0.5f;
        }

        /// <summary>Server-side entry point: same test, no ghost to ignore.</summary>
        public static bool IsValidServerPlacement(PlaceableData data, Vector3 position, Quaternion rotation)
        {
            if (data == null || data.prefab == null)
            {
                return false;
            }

            // Without this the server accepted anything the overlap test let through, so a
            // client that ignored its own red "Needs Floor" ghost could hang walls, doors
            // and cabinets in mid-air or off a wall face anywhere in range.
            if (RequiresFloorContact(data) && !HasFloorSupport(position))
            {
                return false;
            }

            return !IsBlocked(data, position, rotation);
        }
    }
}
