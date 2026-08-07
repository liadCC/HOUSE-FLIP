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

            profitLabel = UIFactory.CreateText(root.transform, "Profit", string.Empty, 44, TextAnchor.UpperLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-460f, -600f), new Vector2(720f, 80f));

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

            _instance?.Display(report, roomScores);
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
                foreach (RoomScore score in roomScores)
                {
                    _builder.AppendLine(score.RoomName.ToString().ToUpperInvariant());
                    _builder.AppendLine($"  Cleanliness: {score.Cleanliness,5:0}");
                    _builder.AppendLine($"  Furniture:   {score.Furniture,5:0}");
                    _builder.AppendLine($"  Design:      {score.Design,5:0}");
                    _builder.AppendLine($"  Condition:   {score.Condition,5:0}");
                    _builder.AppendLine($"  ─────────────────");
                    _builder.AppendLine($"  ROOM SCORE:  {score.Total,5:0}");
                    _builder.AppendLine();
                }
            }

            roomsLabel.text = _builder.ToString();

            profitLabel.text = $"PROFIT: {InspectionReport.SignedMoney(report.Profit)}";
            profitLabel.color = report.IsProfitable ? UIFactory.Good : UIFactory.Bad;
        }
    }
}
