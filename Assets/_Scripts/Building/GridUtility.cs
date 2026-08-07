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

        /// <summary>Yaw snapped to 90 degree steps, which is all the MVP placement needs.</summary>
        public static Quaternion SnapRotation(float yawDegrees)
        {
            return Quaternion.Euler(0f, Mathf.Round(yawDegrees / 90f) * 90f, 0f);
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
