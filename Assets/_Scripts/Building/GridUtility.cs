using HouseFlip.Core;
using UnityEngine;

namespace HouseFlip.Building
{
    /// <summary>Grid snapping shared by the building and furniture placers (GDD 10, 11).</summary>
    public static class GridUtility
    {
        public static float CellSize => GameConstants.GridCellSize;

        /// <summary>Snaps X/Z to the grid and leaves Y untouched (the floor decides height).</summary>
        public static Vector3 Snap(Vector3 world)
        {
            return Snap(world, CellSize);
        }

        public static Vector3 Snap(Vector3 world, float cellSize)
        {
            if (cellSize <= 0.0001f)
            {
                return world;
            }

            return new Vector3(
                Mathf.Round(world.x / cellSize) * cellSize,
                world.y,
                Mathf.Round(world.z / cellSize) * cellSize);
        }

        /// <summary>
        /// Snaps a footprint so even-sized objects land on cell edges and odd-sized ones
        /// land on cell centres. Without this a 2x1 sofa always sits half a cell off.
        /// </summary>
        public static Vector3 SnapFootprint(Vector3 world, Vector2Int footprint)
        {
            float cell = CellSize;
            float offsetX = footprint.x % 2 == 0 ? 0f : cell * 0.5f;
            float offsetZ = footprint.y % 2 == 0 ? 0f : cell * 0.5f;

            return new Vector3(
                Mathf.Round((world.x - offsetX) / cell) * cell + offsetX,
                world.y,
                Mathf.Round((world.z - offsetZ) / cell) * cell + offsetZ);
        }

        /// <summary>
        /// Footprint snap for a piece that has been turned on the grid.
        ///
        /// A 3x2 desk rotated 90 degrees occupies 2x3 cells, so which axis is the odd one
        /// swaps with it. Snapping a rotated piece against its unrotated footprint applies
        /// the half-cell offset to the wrong axis and parks it half a cell off the grid —
        /// which is every wall segment, door, window, cabinet, wardrobe, desk, bed and rug
        /// in the catalog, none of which then line up with the ones already placed.
        /// </summary>
        public static Vector3 SnapFootprint(Vector3 world, Vector2Int footprint, float yawDegrees)
        {
            return SnapFootprint(world, RotateFootprint(footprint, yawDegrees));
        }

        /// <summary>Footprint as it lies on the world grid after a 90 degree step rotation.</summary>
        public static Vector2Int RotateFootprint(Vector2Int footprint, float yawDegrees)
        {
            int quarterTurns = Mathf.Abs(Mathf.RoundToInt(SnapYaw(yawDegrees) / 90f)) % 2;
            return quarterTurns == 0 ? footprint : new Vector2Int(footprint.y, footprint.x);
        }

        /// <summary>
        /// Yaw snapped to 90 degree steps, which is all the MVP placement needs.
        /// Kept separate from <see cref="SnapRotation"/> so the arithmetic can be checked
        /// without going through a quaternion.
        /// </summary>
        public static float SnapYaw(float yawDegrees)
        {
            return Mathf.Round(yawDegrees / 90f) * 90f;
        }

        /// <summary>Yaw snapped to 90 degree steps, as a rotation.</summary>
        public static Quaternion SnapRotation(float yawDegrees)
        {
            return Quaternion.Euler(0f, SnapYaw(yawDegrees), 0f);
        }

        public static Vector3 FootprintToWorldSize(Vector2Int footprint, float height)
        {
            return new Vector3(
                Mathf.Max(1, footprint.x) * CellSize,
                Mathf.Max(0.05f, height),
                Mathf.Max(1, footprint.y) * CellSize);
        }
    }
}
