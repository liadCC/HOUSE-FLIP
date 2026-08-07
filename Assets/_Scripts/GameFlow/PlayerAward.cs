using Unity.Collections;
using Unity.Netcode;

namespace HouseFlip.GameFlow
{
    /// <summary>One end-of-round award handed to one player (GDD 20).</summary>
    public struct PlayerAward : INetworkSerializable
    {
        public ulong ClientId;
        public FixedString32Bytes PlayerName;
        public FixedString32Bytes AwardName;
        public FixedString32Bytes Icon;
        public FixedString64Bytes Detail;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref AwardName);
            serializer.SerializeValue(ref Icon);
            serializer.SerializeValue(ref Detail);
        }
    }
}
