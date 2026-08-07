namespace HouseFlip.Core
{
    /// <summary>
    /// Layer indices declared in ProjectSettings/TagManager.asset.
    /// Keep this in sync with the project's layer list.
    /// </summary>
    public static class GameLayers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int Water = 4;
        public const int UI = 5;
        public const int Player = 8;
        public const int Interactable = 9;
        public const int Grabbable = 10;
        public const int Carried = 11;
        public const int PlacementGhost = 12;
        public const int HouseStructure = 13;

        public static readonly int InteractableMask =
            (1 << Default) | (1 << Interactable) | (1 << Grabbable) | (1 << HouseStructure);

        /// <summary>Surfaces a build/furniture ghost is allowed to snap onto.</summary>
        public static readonly int PlacementSurfaceMask =
            (1 << Default) | (1 << HouseStructure);

        /// <summary>Everything a ghost treats as an obstruction when testing overlap.</summary>
        public static readonly int PlacementBlockerMask =
            (1 << Default) | (1 << Player) | (1 << Interactable) | (1 << Grabbable) | (1 << HouseStructure);

        /// <summary>
        /// What the third-person camera pulls in for. Deliberately excludes Player and
        /// Carried: the camera should never shove itself forward because of the character
        /// it is following or the wardrobe they are holding.
        /// </summary>
        public static readonly int CameraCollisionMask = (1 << Default) | (1 << HouseStructure);
    }
}
