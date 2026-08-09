using System.Collections.Generic;
using HouseFlip.Art;
using HouseFlip.Core;
using HouseFlip.GameFlow;
using HouseFlip.Player;
using UnityEngine;
using UnityEngine.UI;

namespace HouseFlip.UI
{
    /// <summary>
    /// Minecraft-style tool hotbar along the bottom of the screen.
    ///
    /// Every tool is on screen at once, in a fixed slot, with its number on it. That
    /// matters more here than it looks: the game has five tools and a bare-hands state,
    /// and the interaction prompt refuses half the things you point at unless the right
    /// one is equipped. A single line of text naming the current tool told you what you
    /// were holding but not what else existed, or which key would get you there.
    ///
    /// Slot 0 is empty hands — that is the "tool" you need for grabbing and carrying, so
    /// it earns a slot of its own rather than being an invisible mode you reach with [0].
    /// </summary>
    public class ToolbarUI : MonoBehaviour
    {
        private const float SlotSize = 78f;
        private const float SlotSpacing = 8f;
        private const float BottomMargin = 26f;

        /// <summary>
        /// Slot order, left to right. Shared with <see cref="PlayerToolController"/> so the
        /// number drawn on a slot is the key that actually selects it, and so the wheel
        /// steps through the bar in the order it is drawn.
        /// </summary>
        private static ToolType[] Slots => ToolBelt.Slots;

        private readonly List<Image> _slotBackgrounds = new List<Image>();
        private readonly List<Image> _slotIcons = new List<Image>();
        private readonly List<RectTransform> _slotRects = new List<RectTransform>();

        private GameObject _root;
        private Text _nameLabel;
        private int _shown = -1;

        private static readonly Color SlotIdle = new Color(0.10f, 0.09f, 0.13f, 0.72f);
        private static readonly Color SlotSelected = new Color(0.98f, 0.94f, 0.86f, 0.95f);

        private void Awake()
        {
            if (_root == null)
            {
                Build();
            }
        }

        private void OnEnable() => GameEvents.GameStateChanged += OnGameStateChanged;

        private void OnDisable() => GameEvents.GameStateChanged -= OnGameStateChanged;

        private void OnGameStateChanged(GameState state)
        {
            // The hotbar is only meaningful mid-round; it would otherwise sit on top of
            // the lobby and the results screens.
            if (_root != null)
            {
                _root.SetActive(state == GameState.Renovating);
            }
        }

        private void Build()
        {
            Canvas canvas = UIFactory.EnsureCanvas("HUD_Canvas");
            transform.SetParent(canvas.transform, false);

            float totalWidth = Slots.Length * SlotSize + (Slots.Length - 1) * SlotSpacing;

            RectTransform bar = UIFactory.CreateRect(canvas.transform, "Toolbar",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, BottomMargin), new Vector2(totalWidth, SlotSize));
            _root = bar.gameObject;

            for (int i = 0; i < Slots.Length; i++)
            {
                float x = -totalWidth * 0.5f + SlotSize * 0.5f + i * (SlotSize + SlotSpacing);

                Image background = UIFactory.CreatePanel(bar, $"Slot_{i}",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, 0f), new Vector2(SlotSize, SlotSize), SlotIdle);

                // A coloured block stands in for an item sprite. It is the same colour as
                // the tool model in the character's hand, so the two read as the same thing.
                Image icon = UIFactory.CreatePanel(background.transform, "Icon",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 4f), new Vector2(SlotSize * 0.44f, SlotSize * 0.44f),
                    IconColor(Slots[i]));

                Text number = UIFactory.CreateText(background.transform, "Number", i.ToString(), 20,
                    TextAnchor.UpperLeft,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(7f, -4f), new Vector2(24f, 24f));
                number.color = new Color(1f, 1f, 1f, 0.75f);

                _slotBackgrounds.Add(background);
                _slotIcons.Add(icon);
                _slotRects.Add(background.rectTransform);
            }

            // Name of the selected tool, above the bar — Minecraft shows the held item's
            // name the same way, and it saves a legend for six colour swatches.
            _nameLabel = UIFactory.CreateText(bar, "SelectedName", string.Empty, 26,
                TextAnchor.LowerCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(420f, 34f));

            _root.SetActive(false);
        }

        /// <summary>Empty hands reads as a neutral swatch; the rest match their held model.</summary>
        private static Color IconColor(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.Hammer: return ArtPalette.ToolHandle;
                case ToolType.Screwdriver: return ArtPalette.ToolAccent;
                case ToolType.Wrench: return ArtPalette.ToolMetal;
                case ToolType.CleaningTool: return new Color(0.30f, 0.62f, 0.85f);
                case ToolType.PaintRoller: return ArtPalette.Skin;
                default: return new Color(0.55f, 0.55f, 0.58f, 0.5f);
            }
        }

        private void Update()
        {
            if (_root == null || !_root.activeSelf)
            {
                return;
            }

            PlayerController local = PlayerRegistry.LocalPlayer;
            if (local == null || local.Tools == null)
            {
                return;
            }

            int selected = ToolBelt.IndexOf(local.Tools.CurrentTool);
            if (selected != _shown)
            {
                _shown = selected;
                Refresh(selected);
            }
        }

        private void Refresh(int selected)
        {
            for (int i = 0; i < _slotBackgrounds.Count; i++)
            {
                bool isSelected = i == selected;

                _slotBackgrounds[i].color = isSelected ? SlotSelected : SlotIdle;

                // The selected slot grows slightly, which reads instantly even in
                // peripheral vision — colour alone is easy to miss mid-swing.
                float scale = isSelected ? 1.16f : 1f;
                _slotRects[i].localScale = new Vector3(scale, scale, 1f);

                Color icon = IconColor(Slots[i]);
                _slotIcons[i].color = isSelected ? icon : new Color(icon.r, icon.g, icon.b, 0.55f);
            }

            if (_nameLabel != null)
            {
                _nameLabel.text = Slots[selected].DisplayName();
            }
        }
    }
}
