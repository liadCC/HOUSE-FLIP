using System.Collections.Generic;
using HouseFlip.Core;
using UnityEngine;

namespace HouseFlip.Balance
{
    /// <summary>One room's footprint and how much work it starts with.</summary>
    public readonly struct RoomDefinition
    {
        public readonly string Name;
        public readonly float MinX, MaxX, MinZ, MaxZ;
        public readonly bool IsYard;
        public readonly int DirtCount;
        public readonly float ScoreWeight;

        public RoomDefinition(string name, float minX, float maxX, float minZ, float maxZ,
            int dirtCount, float scoreWeight, bool isYard = false)
        {
            Name = name;
            MinX = minX; MaxX = maxX; MinZ = minZ; MaxZ = maxZ;
            DirtCount = dirtCount;
            ScoreWeight = scoreWeight;
            IsYard = isYard;
        }

        public Vector3 Center => new Vector3((MinX + MaxX) * 0.5f, 0f, (MinZ + MaxZ) * 0.5f);
        public float Width => MaxX - MinX;
        public float Depth => MaxZ - MinZ;
    }

    /// <summary>A broken fixture: which room, which tool, what it costs, what fixing it is worth.</summary>
    public readonly struct FixtureDefinition
    {
        public readonly string Room;
        public readonly string Label;
        public readonly ToolType Tool;
        public readonly float Cost;
        public readonly float ValueBonus;
        public readonly Vector3 Offset;

        public FixtureDefinition(string room, string label, ToolType tool, float cost, float valueBonus,
            Vector3 offset)
        {
            Room = room;
            Label = label;
            Tool = tool;
            Cost = cost;
            ValueBonus = valueBonus;
            Offset = offset;
        }
    }

    /// <summary>A straight run of wall, split into segments around its doorways.</summary>
    public readonly struct WallRunDefinition
    {
        public readonly string Name;
        public readonly Vector3 Start, End;
        public readonly float[] Doors;
        public readonly bool Structural;

        public WallRunDefinition(string name, Vector3 start, Vector3 end, float[] doors, bool structural)
        {
            Name = name;
            Start = start;
            End = end;
            Doors = doors;
            Structural = structural;
        }

        /// <summary>
        /// Number of wall pieces this run produces. Matters for balance: every segment is
        /// a paintable surface worth design points and a $100 paint job.
        /// </summary>
        public int SegmentCount(float doorWidth)
        {
            bool alongX = !Mathf.Approximately(Start.x, End.x);
            float from = alongX ? Start.x : Start.z;
            float to = alongX ? End.x : End.z;

            if (Doors == null || Doors.Length == 0)
            {
                return to - from > 0.05f ? 1 : 0;
            }

            var gaps = new List<Vector2>();
            foreach (float door in Doors)
            {
                gaps.Add(new Vector2(door - doorWidth * 0.5f, door + doorWidth * 0.5f));
            }

            gaps.Sort((a, b) => a.x.CompareTo(b.x));

            int segments = 0;
            float cursor = from;

            foreach (Vector2 gap in gaps)
            {
                if (gap.x - cursor > 0.05f)
                {
                    segments++;
                }

                cursor = Mathf.Max(cursor, gap.y);
            }

            if (to - cursor > 0.05f)
            {
                segments++;
            }

            return segments;
        }
    }

    /// <summary>
    /// The MVP house, as data (GDD 4): five rooms off a central hallway, plus a yard.
    ///
    /// This lives in the runtime assembly rather than inside the editor generator so the
    /// balance model can read the same numbers the scene is built from. When the two
    /// drifted apart, tuning was guesswork.
    /// </summary>
    public static class HouseDefinition
    {
        public const float WallHeight = 3f;
        public const float WallThickness = 0.2f;
        public const float DoorWidth = 1.6f;

        public static readonly RoomDefinition[] Rooms =
        {
            new RoomDefinition("Living Room", -9f, -1f, 0f, 7f, dirtCount: 8, scoreWeight: 1f),
            new RoomDefinition("Kitchen", -9f, -1f, 7f, 13f, dirtCount: 8, scoreWeight: 1f),
            new RoomDefinition("Hallway", -1f, 2f, 0f, 13f, dirtCount: 8, scoreWeight: 1f),
            new RoomDefinition("Bedroom", 2f, 10f, 0f, 7f, dirtCount: 8, scoreWeight: 1f),
            new RoomDefinition("Bathroom", 2f, 10f, 7f, 13f, dirtCount: 8, scoreWeight: 1f),

            // The yard is scored, but a scruffy garden should not sink the sale.
            new RoomDefinition("Yard", -9f, 10f, -9f, 0f, dirtCount: 0, scoreWeight: 0.4f, isYard: true)
        };

        // Repair costs are fixed by the table in GDD 13. The value bonuses are tuned at
        // roughly 2.8x cost: fixing a wreck is the highest-return work in the game, which
        // is what makes ignoring the repairs an expensive mistake.
        public static readonly FixtureDefinition[] Fixtures =
        {
            new FixtureDefinition("Living Room", "Broken Light", ToolType.Screwdriver, 150f, 420f,
                new Vector3(0f, 2.6f, 0f)),
            new FixtureDefinition("Living Room", "Faulty Outlet", ToolType.Screwdriver, 250f, 700f,
                new Vector3(2.6f, 0.4f, -2f)),

            new FixtureDefinition("Kitchen", "Leaking Faucet", ToolType.Wrench, 200f, 560f,
                new Vector3(-2.5f, 1f, 2f)),
            new FixtureDefinition("Kitchen", "Faulty Outlet", ToolType.Screwdriver, 250f, 700f,
                new Vector3(2f, 0.4f, -2f)),
            new FixtureDefinition("Kitchen", "Broken Light", ToolType.Screwdriver, 150f, 420f,
                new Vector3(0f, 2.6f, 0f)),

            new FixtureDefinition("Bedroom", "Broken Window", ToolType.Hammer, 400f, 1120f,
                new Vector3(3.6f, 1.4f, 0f)),
            new FixtureDefinition("Bedroom", "Broken Light", ToolType.Screwdriver, 150f, 420f,
                new Vector3(0f, 2.6f, 0f)),

            new FixtureDefinition("Bathroom", "Broken Toilet", ToolType.Wrench, 300f, 840f,
                new Vector3(-2f, 0.4f, -1.5f)),
            new FixtureDefinition("Bathroom", "Leaking Faucet", ToolType.Wrench, 200f, 560f,
                new Vector3(2f, 1f, 1.5f)),
            new FixtureDefinition("Bathroom", "Broken Light", ToolType.Screwdriver, 150f, 420f,
                new Vector3(0f, 2.6f, 0f)),

            new FixtureDefinition("Hallway", "Faulty Outlet", ToolType.Screwdriver, 250f, 700f,
                new Vector3(0f, 0.4f, -4f)),
            new FixtureDefinition("Hallway", "Broken Light", ToolType.Screwdriver, 150f, 420f,
                new Vector3(0f, 2.6f, 2f)),

            new FixtureDefinition("Living Room", "Broken Window", ToolType.Hammer, 400f, 1120f,
                new Vector3(-3.6f, 1.4f, 0f)),
            new FixtureDefinition("Kitchen", "Broken Window", ToolType.Hammer, 400f, 1120f,
                new Vector3(-3.6f, 1.4f, 1f)),
            new FixtureDefinition("Bathroom", "Faulty Outlet", ToolType.Screwdriver, 250f, 700f,
                new Vector3(2.5f, 0.4f, -2f)),
            new FixtureDefinition("Bedroom", "Leaking Faucet", ToolType.Wrench, 200f, 560f,
                new Vector3(-2.8f, 1f, 2f))
        };

        public static readonly WallRunDefinition[] WallRuns =
        {
            // Perimeter — structural, so knocking one down is a real penalty (GDD 16).
            new WallRunDefinition("Wall_South", new Vector3(-9f, 0f, 0f), new Vector3(10f, 0f, 0f),
                new[] { 0.5f }, structural: true),
            new WallRunDefinition("Wall_North", new Vector3(-9f, 0f, 13f), new Vector3(10f, 0f, 13f),
                null, structural: true),
            new WallRunDefinition("Wall_West", new Vector3(-9f, 0f, 0f), new Vector3(-9f, 0f, 13f),
                null, structural: true),
            new WallRunDefinition("Wall_East", new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 13f),
                null, structural: true),

            // Interior — non-structural, which is what the hammer is for (GDD 9).
            new WallRunDefinition("Wall_HallWest", new Vector3(-1f, 0f, 0f), new Vector3(-1f, 0f, 13f),
                new[] { 3.5f, 10f }, structural: false),
            new WallRunDefinition("Wall_HallEast", new Vector3(2f, 0f, 0f), new Vector3(2f, 0f, 13f),
                new[] { 3.5f, 10f }, structural: false),
            new WallRunDefinition("Wall_LivingKitchen", new Vector3(-9f, 0f, 7f), new Vector3(-1f, 0f, 7f),
                null, structural: false),
            new WallRunDefinition("Wall_BedBath", new Vector3(2f, 0f, 7f), new Vector3(10f, 0f, 7f),
                null, structural: false)
        };

        public static int RoomCount => Rooms.Length;

        public static int TotalDirtSources
        {
            get
            {
                int total = 0;
                foreach (RoomDefinition room in Rooms)
                {
                    total += room.DirtCount;
                }

                return total;
            }
        }

        public static int TotalWallSegments
        {
            get
            {
                int total = 0;
                foreach (WallRunDefinition run in WallRuns)
                {
                    total += run.SegmentCount(DoorWidth);
                }

                return total;
            }
        }

        public static RoomDefinition Room(string name)
        {
            foreach (RoomDefinition room in Rooms)
            {
                if (room.Name == name)
                {
                    return room;
                }
            }

            return Rooms[0];
        }
    }
}
