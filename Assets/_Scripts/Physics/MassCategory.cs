namespace HouseFlip.PhysicsGrab
{
    /// <summary>Carry weight classes from GDD 8.</summary>
    public enum MassCategory
    {
        /// <summary>Chair, lamp, box — one player.</summary>
        Light = 0,

        /// <summary>Table, shelf, toilet — one player, but slower.</summary>
        Medium = 1,

        /// <summary>Sofa, fridge, wardrobe — needs two players.</summary>
        Heavy = 2
    }

    public static class MassCategoryExtensions
    {
        public static int RequiredCarriers(this MassCategory category)
        {
            return category == MassCategory.Heavy ? 2 : 1;
        }

        public static float RigidbodyMass(this MassCategory category)
        {
            switch (category)
            {
                case MassCategory.Light: return 4f;
                case MassCategory.Medium: return 18f;
                case MassCategory.Heavy: return 60f;
                default: return 1f;
            }
        }
    }
}
