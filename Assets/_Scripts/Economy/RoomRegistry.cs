using System.Collections.Generic;
using UnityEngine;

namespace HouseFlip.Economy
{
    /// <summary>
    /// Finds which room a world position belongs to. Objects self-register with their
    /// room on Start, so adding a room to the house needs no wiring anywhere else.
    /// </summary>
    public static class RoomRegistry
    {
        private static readonly List<RoomController> Rooms = new List<RoomController>();

        public static IReadOnlyList<RoomController> All => Rooms;

        public static void Register(RoomController room)
        {
            if (room != null && !Rooms.Contains(room))
            {
                Rooms.Add(room);
            }
        }

        public static void Unregister(RoomController room) => Rooms.Remove(room);

        public static void Clear() => Rooms.Clear();

        /// <summary>Room whose bounds contain the point, else the nearest room centre.</summary>
        public static RoomController FindRoom(Vector3 worldPosition)
        {
            foreach (RoomController room in Rooms)
            {
                if (room != null && room.Contains(worldPosition))
                {
                    return room;
                }
            }

            RoomController nearest = null;
            float best = float.MaxValue;

            foreach (RoomController room in Rooms)
            {
                if (room == null)
                {
                    continue;
                }

                float distance = room.WorldBounds.SqrDistance(worldPosition);
                if (distance < best)
                {
                    best = distance;
                    nearest = room;
                }
            }

            return nearest;
        }

        public static RoomController Random()
        {
            var live = new List<RoomController>();
            foreach (RoomController room in Rooms)
            {
                if (room != null)
                {
                    live.Add(room);
                }
            }

            return live.Count == 0 ? null : live[UnityEngine.Random.Range(0, live.Count)];
        }
    }
}
