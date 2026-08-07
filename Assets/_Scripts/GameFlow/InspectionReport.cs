using HouseFlip.Economy;
using Unity.Netcode;
using UnityEngine;

namespace HouseFlip.GameFlow
{
    /// <summary>
    /// The final numbers printed on the inspection and sell screens (GDD 19).
    /// Built once on the server and shipped to every client so nobody sees a different
    /// profit figure than their friends.
    /// </summary>
    public struct InspectionReport : INetworkSerializable
    {
        public HouseValueBreakdown Value;

        public float HousePurchasePrice;
        public float RenovationSpent;
        public float FurnitureSpent;

        /// <summary>Weighted average of every room score (GDD 17).</summary>
        public float FinalHouseScore;

        public float TotalInvestment => HousePurchasePrice + RenovationSpent + FurnitureSpent;
        public float FinalValue => Value.FinalValue;
        public float Profit => FinalValue - TotalInvestment;
        public bool IsProfitable => Profit > 0f;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            Value.NetworkSerialize(serializer);
            serializer.SerializeValue(ref HousePurchasePrice);
            serializer.SerializeValue(ref RenovationSpent);
            serializer.SerializeValue(ref FurnitureSpent);
            serializer.SerializeValue(ref FinalHouseScore);
        }

        public static string Money(float amount)
        {
            string sign = amount < 0f ? "-" : string.Empty;
            return $"{sign}${Mathf.Abs(amount):N0}";
        }

        public static string SignedMoney(float amount)
        {
            string sign = amount < 0f ? "-" : "+";
            return $"{sign}${Mathf.Abs(amount):N0}";
        }
    }
}
