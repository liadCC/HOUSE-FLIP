using HouseFlip.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HouseFlip.UI
{
    /// <summary>
    /// Banner shown when a random event fires or resolves (GDD 21).
    /// Listens to the event bus, so it never needs a reference to the event system.
    /// </summary>
    public class EventPopupUI : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text messageLabel;
        [SerializeField] private float displaySeconds = 5f;
        [SerializeField] private float fadeSeconds = 0.6f;

        private float _remaining;
        private CanvasGroup _group;

        private void Awake()
        {
            if (background == null)
            {
                Build();
            }

            _group = background.GetComponent<CanvasGroup>();
            if (_group == null)
            {
                _group = background.gameObject.AddComponent<CanvasGroup>();
            }

            _group.alpha = 0f;
        }

        private void OnEnable()
        {
            GameEvents.RandomEventStarted += OnEventStarted;
            GameEvents.RandomEventResolved += OnEventResolved;
        }

        private void OnDisable()
        {
            GameEvents.RandomEventStarted -= OnEventStarted;
            GameEvents.RandomEventResolved -= OnEventResolved;
        }

        private void Build()
        {
            Canvas canvas = UIFactory.EnsureCanvas("HUD_Canvas");
            transform.SetParent(canvas.transform, false);

            background = UIFactory.CreatePanel(canvas.transform, "EventPopup",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -140f), new Vector2(880f, 130f), UIFactory.Shade);

            titleLabel = UIFactory.CreateText(background.transform, "Title", string.Empty, 34,
                TextAnchor.UpperCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -12f), new Vector2(0f, 40f));
            titleLabel.color = UIFactory.Accent;

            messageLabel = UIFactory.CreateText(background.transform, "Message", string.Empty, 26,
                TextAnchor.UpperCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -60f), new Vector2(0f, 60f));
        }

        private void Update()
        {
            if (_group == null)
            {
                return;
            }

            if (_remaining > 0f)
            {
                _remaining -= Time.deltaTime;
                _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, Time.deltaTime / fadeSeconds);
            }
            else
            {
                _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.deltaTime / fadeSeconds);
            }
        }

        private void OnEventStarted(string eventName, string message)
        {
            Show($"⚠️  {eventName}", message);
        }

        private void OnEventResolved(string eventName)
        {
            Show("✅  RESOLVED", $"{eventName} is under control.");
        }

        private void Show(string title, string message)
        {
            if (titleLabel != null)
            {
                titleLabel.text = title;
            }

            if (messageLabel != null)
            {
                messageLabel.text = message;
            }

            _remaining = displaySeconds;
        }
    }
}
