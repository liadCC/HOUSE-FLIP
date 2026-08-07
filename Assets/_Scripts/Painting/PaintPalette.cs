using UnityEngine;

namespace HouseFlip.Painting
{
    /// <summary>The seven MVP paint colours (GDD 14).</summary>
    public static class PaintColors
    {
        public static readonly string[] Names =
        {
            "White", "Black", "Blue", "Red", "Green", "Yellow", "Pink"
        };

        public static readonly Color[] Values =
        {
            new Color(0.96f, 0.96f, 0.94f),
            new Color(0.14f, 0.14f, 0.16f),
            new Color(0.30f, 0.55f, 0.90f),
            new Color(0.86f, 0.28f, 0.26f),
            new Color(0.38f, 0.75f, 0.42f),
            new Color(0.96f, 0.83f, 0.32f),
            new Color(0.94f, 0.58f, 0.76f)
        };

        public const int Unpainted = -1;

        public static int Count => Values.Length;

        public static Color Get(int index)
        {
            if (index < 0 || index >= Values.Length)
            {
                return new Color(0.72f, 0.68f, 0.62f); // Grubby original plaster.
            }

            return Values[index];
        }

        public static string GetName(int index)
        {
            if (index < 0 || index >= Names.Length)
            {
                return "Bare";
            }

            return Names[index];
        }

        /// <summary>
        /// Colour pairs that read as a deliberate scheme rather than an accident.
        /// Painting a room in one of these earns the design bonus in GDD 14.
        /// </summary>
        public static bool IsHarmonious(int a, int b)
        {
            if (a < 0 || b < 0)
            {
                return false;
            }

            if (a == b)
            {
                return true;
            }

            // White pairs with anything; black pairs with white and yellow.
            const int white = 0, black = 1, yellow = 5;
            if (a == white || b == white)
            {
                return true;
            }

            return (a == black && b == yellow) || (b == black && a == yellow);
        }
    }

    /// <summary>
    /// Optional override asset so a future house theme can ship its own swatches
    /// without touching code (GDD 26 — architecture for later features).
    /// </summary>
    [CreateAssetMenu(menuName = "House Flip/Paint Palette", fileName = "PaintPalette")]
    public class PaintPalette : ScriptableObject
    {
        [SerializeField] private string[] colorNames;
        [SerializeField] private Color[] colors;

        public int Count => colors != null && colors.Length > 0 ? colors.Length : PaintColors.Count;

        public Color Get(int index)
        {
            if (colors == null || index < 0 || index >= colors.Length)
            {
                return PaintColors.Get(index);
            }

            return colors[index];
        }

        public string GetName(int index)
        {
            if (colorNames == null || index < 0 || index >= colorNames.Length)
            {
                return PaintColors.GetName(index);
            }

            return colorNames[index];
        }
    }
}
