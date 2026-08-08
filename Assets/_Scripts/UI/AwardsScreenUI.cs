using System.Text;
using HouseFlip.GameFlow;
using UnityEngine;
using UnityEngine.UI;

namespace HouseFlip.UI
{
    /// <summary>
    /// Sell result plus the humorous player awards (GDD 19, 20).
    /// Shown after the inspection screen is dismissed.
    /// </summary>
    public class AwardsScreenUI : MonoBehaviour
    {
        private static AwardsScreenUI _instance;

        [SerializeField] private GameObject root;
        [SerializeField] private Text headlineLabel;
        [SerializeField] private Text summaryLabel;
        [SerializeField] private Text awardsLabel;

        private readonly StringBuilder _builder = new StringBuilder();

        private void Awake()
        {
            _instance = this;

            if (root == null)
            {
                Build();
            }

            root.SetActive(false);
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
            Canvas canvas = UIFactory.EnsureCanvas("Screens_Canvas", 10);
            transform.SetParent(canvas.transform, false);

            Image panel = UIFactory.CreateFullScreenPanel(canvas.transform, "AwardsScreen", UIFactory.Shade);
            root = panel.gameObject;

            headlineLabel = UIFactory.CreateText(root.transform, "Headline", string.Empty, 76,
                TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -60f), new Vector2(1400f, 96f));

            summaryLabel = UIFactory.CreateText(root.transform, "Summary", string.Empty, 32,
                TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -170f), new Vector2(1400f, 140f));

            awardsLabel = UIFactory.CreateText(root.transform, "Awards", string.Empty, 34,
                TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -330f), new Vector2(1400f, 420f));

            UIFactory.CreateButton(root.transform, "BACK TO LOBBY",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 60f), new Vector2(420f, 72f),
                () => GameManager.Instance?.RequestAdvance());
        }

        public static void Show(InspectionReport report, PlayerAward[] awards)
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<AwardsScreenUI>();
            }

            if (_instance == null)
            {
                return;
            }

            // Take the inspection screen down only once we know something replaces it —
            // hiding first meant a missing instance left the player looking at the bare game
            // world with no button to advance the round.
            InspectionScreenUI.Hide();

            _instance.Display(report, awards);
        }

        public static void Hide()
        {
            if (_instance != null && _instance.root != null)
            {
                _instance.root.SetActive(false);
            }
        }

        private void Display(InspectionReport report, PlayerAward[] awards)
        {
            root.SetActive(true);

            headlineLabel.text = report.IsProfitable ? "SOLD!" : "SOLD AT A LOSS";
            headlineLabel.color = report.IsProfitable ? UIFactory.Good : UIFactory.Bad;

            _builder.Clear();
            _builder.AppendLine($"FINAL VALUE   {InspectionReport.Money(report.FinalValue)}");
            _builder.AppendLine($"INVESTMENT    {InspectionReport.Money(report.TotalInvestment)}");
            _builder.AppendLine($"PROFIT        {InspectionReport.SignedMoney(report.Profit)}");
            summaryLabel.text = _builder.ToString();

            _builder.Clear();
            if (awards != null)
            {
                foreach (PlayerAward award in awards)
                {
                    _builder.AppendLine($"{award.Icon}  {award.AwardName}  —  {award.PlayerName}");
                    _builder.AppendLine($"      {award.Detail}");
                }
            }

            awardsLabel.text = _builder.ToString();
        }
    }
}
