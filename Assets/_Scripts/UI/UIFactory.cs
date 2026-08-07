using UnityEngine;
using UnityEngine.UI;

namespace HouseFlip.UI
{
    /// <summary>
    /// Builds the HUD widgets in code.
    ///
    /// The MVP has no authored UI prefabs, so every screen constructs itself from these
    /// helpers on Awake. When art replaces the placeholder look, each component can take
    /// serialized references instead and these calls simply stop firing — the fields are
    /// only populated when they are still null.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color Ink = new Color(0.12f, 0.11f, 0.16f);
        public static readonly Color Paper = new Color(1f, 0.98f, 0.94f, 0.92f);
        public static readonly Color Accent = new Color(0.98f, 0.74f, 0.24f);
        public static readonly Color Good = new Color(0.30f, 0.78f, 0.42f);
        public static readonly Color Bad = new Color(0.90f, 0.32f, 0.28f);
        public static readonly Color Shade = new Color(0.08f, 0.07f, 0.11f, 0.86f);

        private static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    // Renamed in newer Unity versions; fall back for older projects.
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                }

                return _font;
            }
        }

        public static Canvas EnsureCanvas(string name, int sortOrder = 0)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                Canvas found = existing.GetComponent<Canvas>();
                if (found != null)
                {
                    return found;
                }
            }

            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static RectTransform CreateRect(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image CreatePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offset, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect(parent, name, anchorMin, anchorMax, pivot, offset, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Image CreateFullScreenPanel(Transform parent, string name, Color color)
        {
            RectTransform rect = CreateRect(parent, name,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text CreateText(Transform parent, string name, string content, int fontSize,
            TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            RectTransform rect = CreateRect(parent, name, anchorMin, anchorMax, pivot, offset, size);

            var text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Paper;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            AddOutline(text.gameObject);
            return text;
        }

        /// <summary>Chunky outline: the cartoon look in GDD 23, and it keeps text readable over any wall colour.</summary>
        public static void AddOutline(GameObject target)
        {
            var outline = target.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        public static Button CreateButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 offset, Vector2 size, System.Action onClick)
        {
            RectTransform rect = CreateRect(parent, $"Button_{label}", anchorMin, anchorMax, pivot, offset, size);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = Accent;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText(rect, "Label", label, 28, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            text.color = Ink;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            if (onClick != null)
            {
                button.onClick.AddListener(() =>
                {
                    Core.GameEvents.RaiseSfx(Core.SfxId.UIClick);
                    onClick();
                });
            }

            return button;
        }

        public static InputField CreateInputField(Transform parent, string name, string initial,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            RectTransform rect = CreateRect(parent, name, anchorMin, anchorMax, pivot, offset, size);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.9f);

            var field = rect.gameObject.AddComponent<InputField>();

            Text text = CreateText(rect, "Text", initial, 24, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            text.color = Ink;
            text.supportRichText = false;
            text.raycastTarget = true;
            text.rectTransform.offsetMin = new Vector2(10f, 4f);
            text.rectTransform.offsetMax = new Vector2(-10f, -4f);

            field.textComponent = text;
            field.targetGraphic = image;
            field.text = initial;

            return field;
        }

        public static VerticalLayoutGroup MakeVerticalList(RectTransform rect, float spacing, RectOffset padding)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(12, 12, 12, 12);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            return layout;
        }
    }
}
