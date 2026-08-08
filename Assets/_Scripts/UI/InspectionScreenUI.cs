using System.Text;
using HouseFlip.Economy;
using HouseFlip.GameFlow;
using UnityEngine;
using UnityEngine.UI;

namespace HouseFlip.UI
{
    /// <summary>
    /// The HOUSE INSPECTION screen (GDD 19). Renders the itemised value breakdown, the
    /// per-room scores, and the profit calculation exactly in the order the GDD lists them.
    /// </summary>
    public class InspectionScreenUI : MonoBehaviour
    {
        private static InspectionScreenUI _instance;

        [SerializeField] private GameObject root;
        [SerializeField] private Text bodyLabel;
        [SerializeField] private Text roomsLabel;
        [SerializeField] private Text profitLabel;

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

            Image panel = UIFactory.CreateFullScreenPanel(canvas.transform, "InspectionScreen", UIFactory.Shade);
            root = panel.gameObject;

            UIFactory.CreateText(root.transform, "Title", "HOUSE INSPECTION", 64, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -50f), new Vector2(1200f, 80f)).color = UIFactory.Accent;

            bodyLabel = UIFactory.CreateText(root.transform, "Body", string.Empty, 30, TextAnchor.UpperLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-460f, -160f), new Vector2(720f, 420f));

            roomsLabel = UIFactory.CreateText(root.transform, "Rooms", string.Empty, 22, TextAnchor.UpperLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(420f, -160f), new Vector2(640f, 640f));

            // 600 sat inside the body block: 16 lines of 30pt text reach ~700px below the
            // canvas top, so the profit line was drawn straight over TOTAL INVESTMENT.
            profitLabel = UIFactory.CreateText(root.transform, "Profit", string.Empty, 44, TextAnchor.UpperLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-460f, -740f), new Vector2(720f, 80f));

            UIFactory.CreateButton(root.transform, "SELL THE HOUSE",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 60f), new Vector2(420f, 72f),
                () => GameManager.Instance?.RequestAdvance());
        }

        public static void Show(InspectionReport report, RoomScore[] roomScores)
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<InspectionScreenUI>();
            }

            if (_instance == null)
            {
                return;
            }

            // Both end-of-round screens are full-screen panels on Screens_Canvas and the
            // awards panel is created later, so it is the later sibling and would draw on
            // top of this one if a previous round had left it active.
            AwardsScreenUI.Hide();

            _instance.Display(report, roomScores);
        }

        public static void Hide()
        {
            if (_instance != null && _instance.root != null)
            {
                _instance.root.SetActive(false);
            }
        }

        private void Display(InspectionReport report, RoomScore[] roomScores)
        {
            root.SetActive(true);

            _builder.Clear();
            _builder.AppendLine("═══════════════════════════════");
            _builder.AppendLine($"  Original Value:    {InspectionReport.Money(report.Value.BaseValue)}");
            _builder.AppendLine($"  Renovation Bonus:  {InspectionReport.SignedMoney(report.Value.RenovationBonus)}");
            _builder.AppendLine($"  Furniture Bonus:   {InspectionReport.SignedMoney(report.Value.FurnitureBonus)}");
            _builder.AppendLine($"  Design Bonus:      {InspectionReport.SignedMoney(report.Value.DesignBonus)}");
            _builder.AppendLine($"  Cleanliness Bonus: {InspectionReport.SignedMoney(report.Value.CleanlinessBonus)}");
            _builder.AppendLine($"  Damage Penalty:    {InspectionReport.SignedMoney(-report.Value.DamagePenalty)}");
            _builder.AppendLine("  ───────────────────────────");
            _builder.AppendLine($"  FINAL VALUE:       {InspectionReport.Money(report.FinalValue)}");
            _builder.AppendLine("═══════════════════════════════");
            _builder.AppendLine();
            _builder.AppendLine($"  House Purchase:    {InspectionReport.Money(report.HousePurchasePrice)}");
            _builder.AppendLine($"  Renovation Spent:  {InspectionReport.Money(report.RenovationSpent)}");
            _builder.AppendLine($"  Furniture Spent:   {InspectionReport.Money(report.FurnitureSpent)}");
            _builder.AppendLine("  ───────────────────────────");
            _builder.AppendLine($"  TOTAL INVESTMENT:  {InspectionReport.Money(report.TotalInvestment)}");

            bodyLabel.text = _builder.ToString();

            _builder.Clear();
            _builder.AppendLine($"HOUSE SCORE: {report.FinalHouseScore:0} / 100");
            _builder.AppendLine();

            if (roomScores != null)
            {
                // One row per room, not seven. The house has six scored rooms (GDD 17), and at
                // seven lines each the block ran ~1100px from a start 160px down a 1080px
                // canvas — the last rooms were drawn off the bottom of the screen entirely.
                foreach (RoomScore score in roomScores)
                {
                    _builder.AppendLine($"{score.RoomName.ToString().ToUpperInvariant(),-14}{score.Total,5:0}");
                    _builder.AppendLine(
                        $"   Clean {score.Cleanliness,3:0}   Furn {score.Furniture,3:0}" +
                        $"   Design {score.Design,3:0}   Cond {score.Condition,3:0}");
                    _builder.AppendLine();
                }
            }

            roomsLabel.text = _builder.ToString();

            profitLabel.text = $"PROFIT: {InspectionReport.SignedMoney(report.Profit)}";
            profitLabel.color = report.IsProfitable ? UIFactory.Good : UIFactory.Bad;
        }
    }
}
