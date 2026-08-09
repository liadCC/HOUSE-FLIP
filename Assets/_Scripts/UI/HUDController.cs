using System.Text;
using HouseFlip.Core;
using HouseFlip.GameFlow;
using HouseFlip.Player;
using HouseFlip.Polish;
using UnityEngine;
using UnityEngine.UI;

namespace HouseFlip.UI
{
    /// <summary>
    /// The in-game HUD (GDD 23): players and budget top-left, timer top-centre,
    /// house value top-right.
    ///
    /// Every number here comes from a replicated NetworkVariable via the event bus, so
    /// the HUD is a pure view — it never computes a value of its own.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [SerializeField] private Text budgetLabel;
        [SerializeField] private Text playersLabel;
        [SerializeField] private Text timerLabel;
        [SerializeField] private Text houseValueLabel;
        [SerializeField] private Text toolLabel;
        [SerializeField] private Text warningLabel;

        private float _warningTimer;
        private readonly StringBuilder _builder = new StringBuilder();

        private bool _budgetInitialised;
        private bool _houseValueInitialised;
        private float _lastHouseValue;
        private float _valueColorTimer;

        private void Awake()
        {
            if (budgetLabel == null)
            {
                Build();
            }
        }

        private void OnEnable()
        {
            GameEvents.BudgetChanged += OnBudgetChanged;
            GameEvents.HouseValueChanged += OnHouseValueChanged;
            GameEvents.TimerChanged += OnTimerChanged;
            GameEvents.PurchaseRejected += OnPurchaseRejected;
            GameEvents.GameStateChanged += OnGameStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.BudgetChanged -= OnBudgetChanged;
            GameEvents.HouseValueChanged -= OnHouseValueChanged;
            GameEvents.TimerChanged -= OnTimerChanged;
            GameEvents.PurchaseRejected -= OnPurchaseRejected;
            GameEvents.GameStateChanged -= OnGameStateChanged;
        }

        private void Build()
        {
            Canvas canvas = UIFactory.EnsureCanvas("HUD_Canvas");
            transform.SetParent(canvas.transform, false);
            Transform root = canvas.transform;

            budgetLabel = UIFactory.CreateText(root, "Budget", "$20,000", 40, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -28f), new Vector2(520f, 52f));
            budgetLabel.color = UIFactory.Accent;

            playersLabel = UIFactory.CreateText(root, "Players", string.Empty, 24, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -84f), new Vector2(520f, 140f));

            timerLabel = UIFactory.CreateText(root, "Timer", "25:00", 72, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(400f, 90f));

            houseValueLabel = UIFactory.CreateText(root, "HouseValue", "$50,000", 40, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-32f, -28f), new Vector2(520f, 52f));
            houseValueLabel.color = UIFactory.Good;

            // Key hints only. The equipped tool used to be named here as well, which was
            // the only way to know what you were holding; the hotbar shows that — and the
            // five you are not holding — along the bottom of the screen now.
            toolLabel = UIFactory.CreateText(root, "Tool", "[F] shop    [B] build", 26, TextAnchor.LowerRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-32f, 32f), new Vector2(520f, 40f));

            warningLabel = UIFactory.CreateText(root, "Warning", string.Empty, 44, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 120f), new Vector2(900f, 60f));
            warningLabel.color = UIFactory.Bad;
            warningLabel.enabled = false;
        }

        private void Update()
        {
            UpdatePlayerList();

            if (_warningTimer > 0f)
            {
                _warningTimer -= Time.deltaTime;
                if (_warningTimer <= 0f && warningLabel != null)
                {
                    warningLabel.enabled = false;
                }
            }

            // Let the gain/loss tint fade back to the neutral colour.
            if (_valueColorTimer > 0f)
            {
                _valueColorTimer -= Time.deltaTime;
                if (_valueColorTimer <= 0f && houseValueLabel != null)
                {
                    houseValueLabel.color = UIFactory.Good;
                }
            }
        }

        private void UpdatePlayerList()
        {
            if (playersLabel == null)
            {
                return;
            }

            _builder.Clear();

            foreach (var entry in PlayerRegistry.All)
            {
                PlayerController player = entry.Value;
                if (player == null)
                {
                    continue;
                }

                Color color = player.PlayerColor.Value;
                string hex = ColorUtility.ToHtmlStringRGB(color);
                string name = player.Stats != null ? player.Stats.DisplayName.Value.ToString() : $"Player {entry.Key}";
                string you = player.IsOwner ? "  (you)" : string.Empty;

                _builder.AppendLine($"<color=#{hex}>●</color> {name}{you}");
            }

            playersLabel.supportRichText = true;
            playersLabel.text = _builder.ToString();
        }

        private void OnBudgetChanged(float value)
        {
            if (budgetLabel == null)
            {
                return;
            }

            budgetLabel.text = $"${value:N0}";
            budgetLabel.color = value <= 0f ? UIFactory.Bad : UIFactory.Accent;

            // Skip the punch on the initial population, or the HUD pops on spawn.
            if (_budgetInitialised)
            {
                ScalePunch.PunchOn(budgetLabel.gameObject, 0.18f);
            }

            _budgetInitialised = true;
        }

        private void OnHouseValueChanged(float value)
        {
            if (houseValueLabel == null)
            {
                return;
            }

            houseValueLabel.text = $"${value:N0}";

            if (_houseValueInitialised)
            {
                // Green when the house gained value, red when the team just cost themselves
                // money — the direction is more useful at a glance than the number.
                bool gained = value >= _lastHouseValue;
                houseValueLabel.color = gained ? UIFactory.Good : UIFactory.Bad;
                _valueColorTimer = 1.2f;

                ScalePunch.PunchOn(houseValueLabel.gameObject, gained ? 0.2f : 0.28f);
            }

            _lastHouseValue = value;
            _houseValueInitialised = true;
        }

        private void OnTimerChanged(float secondsRemaining)
        {
            if (timerLabel == null)
            {
                return;
            }

            timerLabel.text = TimerManager.Format(secondsRemaining);

            // Last two minutes go red so nobody is surprised by the buzzer.
            timerLabel.color = secondsRemaining <= 120f ? UIFactory.Bad : UIFactory.Paper;
        }

        private void OnPurchaseRejected(string reason)
        {
            if (warningLabel == null)
            {
                return;
            }

            warningLabel.text = reason;
            warningLabel.enabled = true;
            _warningTimer = 2.5f;
        }

        private void OnGameStateChanged(GameState state)
        {
            bool inGame = state == GameState.Renovating;

            SetVisible(budgetLabel, inGame);
            SetVisible(playersLabel, inGame);
            SetVisible(timerLabel, inGame);
            SetVisible(houseValueLabel, inGame);
            SetVisible(toolLabel, inGame);

            if (!inGame && warningLabel != null)
            {
                warningLabel.enabled = false;
            }
        }

        private static void SetVisible(Text text, bool visible)
        {
            if (text != null)
            {
                text.enabled = visible;
            }
        }
    }
}
