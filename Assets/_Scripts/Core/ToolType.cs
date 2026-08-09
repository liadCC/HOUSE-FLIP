namespace HouseFlip.Core
{
    /// <summary>Tools a player can hold (GDD 6). Bare hands is the default state.</summary>
    public enum ToolType
    {
        None = 0,
        Hammer = 1,
        Screwdriver = 2,
        Wrench = 3,
        CleaningTool = 4,
        PaintRoller = 5
    }

    /// <summary>
    /// The hotbar, as an ordered list of slots. One definition shared by the UI that
    /// draws the bar and the input that cycles it, so the number key printed on a slot
    /// is guaranteed to be the key that selects it.
    ///
    /// Bare hands occupies slot 0 rather than being an invisible mode: it is the state
    /// you need for grabbing and carrying, so it deserves to be visible like any tool.
    /// </summary>
    public static class ToolBelt
    {
        public static readonly ToolType[] Slots =
        {
            ToolType.None,
            ToolType.Hammer,
            ToolType.Screwdriver,
            ToolType.Wrench,
            ToolType.CleaningTool,
            ToolType.PaintRoller
        };

        /// <summary>Slot holding this tool, or 0 for anything unrecognised.</summary>
        public static int IndexOf(ToolType tool)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i] == tool)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        /// Steps along the belt, wrapping at both ends. Negative steps walk left.
        /// </summary>
        public static ToolType Cycle(ToolType current, int steps)
        {
            int count = Slots.Length;

            // C# % keeps the sign of the dividend, so scrolling left off slot 0 would
            // land on a negative index without the second wrap.
            int index = (IndexOf(current) + steps) % count;
            if (index < 0)
            {
                index += count;
            }

            return Slots[index];
        }
    }

    public static class ToolTypeExtensions
    {
        public static string DisplayName(this ToolType tool)
        {
            switch (tool)
            {
                case ToolType.Hammer: return "Hammer";
                case ToolType.Screwdriver: return "Screwdriver";
                case ToolType.Wrench: return "Wrench";
                case ToolType.CleaningTool: return "Vacuum";
                case ToolType.PaintRoller: return "Paint Roller";
                default: return "Hands";
            }
        }
    }
}
