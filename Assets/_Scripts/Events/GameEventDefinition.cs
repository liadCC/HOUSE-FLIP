using UnityEngine;
using UnityEngine.Events;

namespace HouseFlip.Events
{
    /// <summary>
    /// Data-driven random event (GDD 21). Adding a fourth event is a matter of authoring
    /// one of these assets and a handler component — no changes to the scheduler.
    /// </summary>
    [CreateAssetMenu(menuName = "House Flip/Game Event", fileName = "Event_")]
    public class GameEventDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string eventName = "Event";

        [TextArea]
        public string popupMessage = "Something happened!";

        [Header("Trigger window (seconds into the session)")]
        [Tooltip("Earliest point in the round this event may fire.")]
        public float earliestTime = 120f;

        [Tooltip("Latest point in the round this event may fire.")]
        public float latestTime = 1200f;

        [Tooltip("Relative likelihood when several events are eligible at once.")]
        public float weight = 1f;

        [Tooltip("Fire at most once per round.")]
        public bool oncePerRound = true;

        [Header("Hooks")]
        public UnityEvent onEventStart;
        public UnityEvent onEventResolved;

        public bool IsInWindow(float elapsedSeconds)
        {
            return elapsedSeconds >= earliestTime && elapsedSeconds <= latestTime;
        }
    }
}
