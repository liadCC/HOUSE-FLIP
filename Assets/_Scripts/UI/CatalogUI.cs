using System.Collections.Generic;
using HouseFlip.Building;
using HouseFlip.Core;
using HouseFlip.Furniture;
using HouseFlip.GameFlow;
using HouseFlip.Networking;
using HouseFlip.Painting;
using HouseFlip.Player;
using UnityEngine;
using UnityEngine.UI;

namespace HouseFlip.UI
{
    /// <summary>
    /// The shop: build pieces on [B], furniture on [F], paint swatches on [C] (GDD 10, 11, 14).
    ///
    /// Picking an item hands off to the local <see cref="PlacementController"/>; this panel
    /// never spends money or spawns anything itself.
    /// </summary>
    public class CatalogUI : MonoBehaviour
    {
        [SerializeField] private PlacementCatalog catalog;
        [SerializeField] private KeyCode furnitureKey = KeyCode.F;
        [SerializeField] private KeyCode buildKey = KeyCode.B;
        [SerializeField] private KeyCode paintKey = KeyCode.C;

        private GameObject _root;
        private RectTransform _listRoot;
        private Text _titleLabel;

        private readonly List<GameObject> _entries = new List<GameObject>();
        private CatalogKind _kind = CatalogKind.Furniture;
        private bool _paintMode;
        private GameState _state = GameState.Lobby;

        private bool IsOpen => _root != null && _root.activeSelf;

        private void Awake()
        {
            if (_root == null)
            {
                Build();
            }

            _root.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.GameStateChanged += OnGameStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.GameStateChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(GameState state)
        {
            _state = state;

            // Close in the same frame the round ends rather than waiting for Update, so the
            // shop cannot be drawn over the inspection screen that is about to appear.
            if (_state != GameState.Renovating && IsOpen)
            {
                Close();
            }
        }

        private void Build()
        {
            Canvas canvas = UIFactory.EnsureCanvas("HUD_Canvas");
            transform.SetParent(canvas.transform, false);

            Image panel = UIFactory.CreatePanel(canvas.transform, "CatalogPanel",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-40f, 0f), new Vector2(460f, 760f), UIFactory.Shade);
            _root = panel.gameObject;

            _titleLabel = UIFactory.CreateText(_root.transform, "Title", "CATALOG", 36, TextAnchor.UpperCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -16f), new Vector2(0f, 46f));
            _titleLabel.color = UIFactory.Accent;

            _listRoot = UIFactory.CreateRect(_root.transform, "List",
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero);
            _listRoot.offsetMin = new Vector2(14f, 60f);
            _listRoot.offsetMax = new Vector2(-14f, -70f);

            UIFactory.MakeVerticalList(_listRoot, 8f, new RectOffset(6, 6, 6, 6));

            UIFactory.CreateText(_root.transform, "Hint",
                "[R] rotate   [Shift+Click] place many   [Esc] cancel", 20, TextAnchor.LowerCenter,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 14f), new Vector2(0f, 34f));
        }

        private void Update()
        {
            // Gate on the session state, not PlayerController.InputEnabled: that flag is
            // deliberately true again in the lobby (ResetInputGate), so keying it would let
            // the shop open on top of the lobby menu — and closing it there re-grabs the
            // cursor, leaving HOST GAME unclickable with nothing left to release it.
            if (_state != GameState.Renovating)
            {
                if (IsOpen)
                {
                    Close();
                }

                return;
            }

            if (Input.GetKeyDown(furnitureKey))
            {
                Toggle(CatalogKind.Furniture, false);
            }
            else if (Input.GetKeyDown(buildKey))
            {
                Toggle(CatalogKind.Building, false);
            }
            else if (Input.GetKeyDown(paintKey))
            {
                Toggle(CatalogKind.Furniture, true);
            }
            else if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private void Toggle(CatalogKind kind, bool paintMode)
        {
            if (IsOpen && _kind == kind && _paintMode == paintMode)
            {
                Close();
                return;
            }

            _kind = kind;
            _paintMode = paintMode;
            Open();
        }

        private void Open()
        {
            ResolveCatalog();

            // Drop any ghost first. PlacementController reads the raw mouse button and knows
            // nothing about uGUI, so with the cursor freed the very click that picks the next
            // item would also confirm — and pay for — the item already being placed.
            CancelLocalPlacement();

            _root.SetActive(true);
            PlayerCameraRig.SetCursorLocked(false);

            if (_paintMode)
            {
                PopulatePaint();
            }
            else
            {
                PopulateItems();
            }
        }

        private void Close()
        {
            _root.SetActive(false);

            // Only hand the cursor back to the camera while a round is actually running.
            // Closing because the timer hit zero would otherwise lock the cursor the instant
            // the inspection screen opens, so SELL THE HOUSE could never be clicked and
            // nothing would ever release the cursor again.
            if (_state == GameState.Renovating)
            {
                PlayerCameraRig.SetCursorLocked(true);
            }
        }

        private static void CancelLocalPlacement()
        {
            PlayerController local = PlayerRegistry.LocalPlayer;
            PlacementController placer = local != null ? local.GetComponent<PlacementController>() : null;

            if (placer != null && placer.IsPlacing)
            {
                placer.Cancel();
            }
        }

        private void ResolveCatalog()
        {
            if (catalog == null && RenovationService.Instance != null)
            {
                catalog = RenovationService.Instance.Catalog;
            }
        }

        private void ClearEntries()
        {
            foreach (GameObject entry in _entries)
            {
                if (entry != null)
                {
                    Destroy(entry);
                }
            }

            _entries.Clear();
        }

        private void PopulateItems()
        {
            ClearEntries();

            _titleLabel.text = _kind == CatalogKind.Building ? "BUILD" : "FURNITURE";

            if (catalog == null)
            {
                AddLabel("No catalog assigned");
                return;
            }

            int count = catalog.CountOf(_kind);
            if (count == 0)
            {
                AddLabel("Catalog is empty");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                PlaceableData data = catalog.Resolve(_kind, i);
                if (data == null)
                {
                    continue;
                }

                int index = i;
                CatalogKind kind = _kind;

                Button button = UIFactory.CreateButton(_listRoot, $"{data.itemName}   ${data.cost:N0}",
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    Vector2.zero, new Vector2(0f, 54f),
                    () => Select(kind, index));

                var element = button.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 54f;

                _entries.Add(button.gameObject);
            }
        }

        private void PopulatePaint()
        {
            ClearEntries();
            _titleLabel.text = "PAINT";

            for (int i = 0; i < PaintColors.Count; i++)
            {
                int index = i;

                Button button = UIFactory.CreateButton(_listRoot, PaintColors.GetName(i),
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    Vector2.zero, new Vector2(0f, 54f),
                    () => SelectPaint(index));

                Image image = button.GetComponent<Image>();
                image.color = PaintColors.Get(i);

                var element = button.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 54f;

                _entries.Add(button.gameObject);
            }
        }

        private void AddLabel(string message)
        {
            Text text = UIFactory.CreateText(_listRoot, "Empty", message, 24, TextAnchor.MiddleCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 40f));

            _entries.Add(text.gameObject);
        }

        private void Select(CatalogKind kind, int index)
        {
            PlayerController local = PlayerRegistry.LocalPlayer;
            PlacementController placer = local != null ? local.GetComponent<PlacementController>() : null;

            if (placer != null)
            {
                placer.BeginPlacement(kind, index);
            }

            Close();
        }

        private void SelectPaint(int colorIndex)
        {
            PlayerController local = PlayerRegistry.LocalPlayer;

            // Close on every path, exactly like Select does. Bailing out early left the panel
            // open with the cursor free whenever the local player had not spawned yet, and
            // the player had no way to tell the click had been swallowed.
            if (local != null && local.Tools != null)
            {
                local.Tools.SetPaintColorIndex(colorIndex);

                // Picking a colour implies you want the roller in your hand.
                if (local.Tools.CurrentTool != ToolType.PaintRoller)
                {
                    local.Tools.Equip(ToolType.PaintRoller);
                }
            }

            Close();
        }
    }
}
