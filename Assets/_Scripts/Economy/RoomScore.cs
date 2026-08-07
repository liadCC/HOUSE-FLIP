using HouseFlip.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.Economy
{
    /// <summary>
    /// A room's four sub-scores and the weighted total (GDD 17).
    /// Serializable so the host can ship the whole breakdown to the inspection screen.
    /// </summary>
    public struct RoomScore : INetworkSerializable
    {
        public FixedString32Bytes RoomName;
        public float Cleanliness;
        public float Furniture;
        public float Design;
        public float Condition;

        /// <summary>
        /// RoomScore = (Cleanliness * 0.25) + (Furniture * 0.30) + (Design * 0.20) + (Condition * 0.25)
        /// </summary>
        public float Total => Mathf.Clamp(
            Cleanliness * GameConstants.WeightCleanliness +
            Furniture * GameConstants.WeightFurniture +
            Design * GameConstants.WeightDesign +
            Condition * GameConstants.WeightCondition,
            0f, 100f);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref RoomName);
            serializer.SerializeValue(ref Cleanliness);
            serializer.SerializeValue(ref Furniture);
            serializer.SerializeValue(ref Design);
            serializer.SerializeValue(ref Condition);
        }

        public override string ToString()
        {
            return $"{RoomName}\n" +
                   $"  Cleanliness: {Cleanliness:0}\n" +
                   $"  Furniture:   {Furniture:0}\n" +
                   $"  Design:      {Design:0}\n" +
                   $"  Condition:   {Condition:0}\n" +
                   $"  ROOM SCORE:  {Total:0}";
        }
    }
}
