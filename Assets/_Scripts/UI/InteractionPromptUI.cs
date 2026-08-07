using UnityEngine;
using UnityEngine.UI;

namespace HouseFlip.UI
{
    /// <summary>
    /// The single centre-screen prompt (GDD 23). Static entry points because the
    /// Interactor, the carry system and the placement ghost all want to write to it and
    /// none of them should have to find it first.
    ///
    /// An override (placement mode, "Too Heavy") beats the aimed-at prompt.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        private static InteractionPromptUI _instance;
        private static string _prompt;
        private static string _override;

        [SerializeField] private Text label;

        private void Awake()
        {
            _instance = this;

            if (label == null)
            {
                Build();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Build()
        {
            Canvas canvas = UIFactory.EnsureCanvas("HUD_Canvas");
            transform.SetParent(canvas.transform, false);

            label = UIFactory.CreateText(canvas.transform, "InteractionPrompt", string.Empty, 34,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 150f), new Vector2(900f, 60f));

            label.color = UIFactory.Paper;
        }

        private void LateUpdate()
        {
            if (label == null)
            {
                return;
            }

            string active = !string.IsNullOrEmpty(_override) ? _override : _prompt;

            if (string.IsNullOrEmpty(active))
            {
                if (label.enabled)
                {
                    label.enabled = false;
                }

                return;
            }

            label.enabled = true;

            // Placement hints already carry their own key hints, so only decorate the
            // plain verb form.
            label.text = active.StartsWith("[") ? active : $"[E] {active}";
        }

        /// <summary>Set by the Interactor every frame; null clears it.</summary>
        public static void SetPrompt(string prompt) => _prompt = prompt;

        /// <summary>Takes priority over the aimed-at prompt. Remember to clear it.</summary>
        public static void SetOverride(string message) => _override = message;

        public static void Clear()
        {
            _prompt = null;
            _override = null;
        }
    }
}
