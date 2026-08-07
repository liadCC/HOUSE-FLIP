using HouseFlip.Core;
using HouseFlip.Player;

namespace HouseFlip.Interaction
{
    /// <summary>
    /// The single interaction contract (GDD 7). Every object the player can press [E]
    /// on implements this — destructibles, grabbables, dirt, broken fixtures, walls,
    /// the fuse box, the burst pipe, everything.
    ///
    /// A blocked interaction is expressed through the prompt text rather than a
    /// separate "can interact" flag, which is what lets a heavy sofa read
    /// "[E] Too Heavy — Need Help" without any special-casing in the Interactor.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Verb shown after the key hint, e.g. "Smash", "Grab", "Paint", "Fix".
        /// Return null or empty to suppress the prompt entirely.
        /// </summary>
        string GetPromptText();

        /// <summary>Fired on the local player that pressed [E]. Implementations forward to the server.</summary>
        void OnInteract(PlayerController player);
    }

    /// <summary>
    /// Declares which tool an interaction belongs to.
    ///
    /// A single object often carries several interactions — a wall is both paintable and
    /// smashable, a fridge is both grabbable and sellable — and without this the
    /// <see cref="Interactor"/> would have to guess which one [E] meant. With it, the
    /// equipped tool disambiguates: hammer means smash, roller means paint.
    /// </summary>
    public interface IToolGated
    {
        ToolType RequiredTool { get; }
    }
}
