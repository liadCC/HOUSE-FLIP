namespace HouseFlip.Core
{
    /// <summary>
    /// Per-player counters tracked across a session and used to hand out the
    /// end-of-round awards (GDD 20).
    /// </summary>
    public enum PlayerStat
    {
        ObjectsDestroyed = 0,
        MoneySpent = 1,
        DirtCleaned = 2,
        RepairsCompleted = 3,
        FurniturePlaced = 4,
        WallsPainted = 5,
        DesignPointsAdded = 6,
        HouseValueAdded = 7,
        DamageCaused = 8,
        EventsResolved = 9
    }

    public static class PlayerStatExtensions
    {
        public const int Count = 10;
    }
}
