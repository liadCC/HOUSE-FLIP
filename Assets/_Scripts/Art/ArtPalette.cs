using UnityEngine;

namespace HouseFlip.Art
{
    /// <summary>
    /// The house style, in one place (GDD 1: cartoon / stylized / low-poly).
    ///
    /// The scheme is warm-lit with cool shadows. That single decision does most of the
    /// work: a warm key light against a blue-violet shade reads as sunlight and gives
    /// flat-shaded geometry depth without any texture detail. Everything else is chosen
    /// to sit inside that, with saturation kept low enough that the paint colours players
    /// choose (GDD 14) stay the loudest thing in the room.
    /// </summary>
    public static class ArtPalette
    {
        private static Color Hex(int rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f);
        }

        // --- Light and shade ---------------------------------------------
        public static readonly Color SunLight = Hex(0xFFF2DC);
        public static readonly Color ShadowTint = Hex(0x8C96D1);
        public static readonly Color RimLight = Hex(0xFFFAF0);

        public static readonly Color AmbientSky = Hex(0x9DB4D6);
        public static readonly Color AmbientEquator = Hex(0x8A8C93);
        public static readonly Color AmbientGround = Hex(0x574F47);

        // --- Sky ----------------------------------------------------------
        public static readonly Color SkyTop = Hex(0x5C93DB);
        public static readonly Color SkyHorizon = Hex(0xDEEAF6);
        public static readonly Color SkyBottom = Hex(0x6B6259);

        public static readonly Color Fog = Hex(0xCBDCEC);

        // --- Structure ----------------------------------------------------
        public static readonly Color Plaster = Hex(0xE4DACA);
        public static readonly Color FloorWood = Hex(0xB2855A);
        public static readonly Color Grass = Hex(0x74A356);
        public static readonly Color Pipe = Hex(0x93A0AD);
        public static readonly Color FuseBox = Hex(0x5A5F66);

        // --- State ---------------------------------------------------------
        // Broken and fixed need to be readable at a glance across a room, so they are
        // the two most saturated colours in the palette.
        public static readonly Color Broken = Hex(0xC94F3D);
        public static readonly Color Repaired = Hex(0x5FAE55);
        public static readonly Color Dirt = Hex(0x5C4A33);
        public static readonly Color Water = new Color(0.42f, 0.66f, 0.86f, 0.55f);

        // --- Characters -----------------------------------------------------
        // Kept far apart in hue so four players are instantly distinguishable, and
        // matched in value so nobody's character reads as "the dark one".
        public static readonly Color[] PlayerColors =
        {
            Hex(0xE4553F),
            Hex(0x3D8CD6),
            Hex(0x5CB255),
            Hex(0xEBB43C)
        };

        public static readonly Color Skin = Hex(0xE8C6A0);
        public static readonly Color Overalls = Hex(0x4A5568);

        // --- Tools ----------------------------------------------------------
        public static readonly Color ToolHandle = Hex(0x8A5A38);
        public static readonly Color ToolMetal = Hex(0xA8B0B8);
        public static readonly Color ToolAccent = Hex(0xE0A030);

        /// <summary>Slight per-instance variation so a row of identical props does not look stamped.</summary>
        public static Color Vary(Color color, float amount = 0.05f)
        {
            float t = 1f + Random.Range(-amount, amount);
            return new Color(
                Mathf.Clamp01(color.r * t),
                Mathf.Clamp01(color.g * t),
                Mathf.Clamp01(color.b * t),
                color.a);
        }
    }
}
