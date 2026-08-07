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
