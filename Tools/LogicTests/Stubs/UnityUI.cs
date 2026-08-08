// Stubs for UnityEngine.UI (uGUI) members used by the HUD and screens.
using System;

namespace UnityEngine.UI
{
    public class Graphic : UnityEngine.Behaviour
    {
        public Color color { get; set; }
        public bool raycastTarget { get; set; }
        public RectTransform rectTransform => null;
    }

    public class MaskableGraphic : Graphic { }

    public class Image : MaskableGraphic
    {
        public Sprite sprite { get; set; }
    }

    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }

    public class Text : MaskableGraphic
    {
        public string text { get; set; } = "";
        public Font font { get; set; }
        public int fontSize { get; set; }
        public TextAnchor alignment { get; set; }
        public bool supportRichText { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
    }

    public class Selectable : UnityEngine.Behaviour
    {
        public bool interactable { get; set; } = true;
        public Graphic targetGraphic { get; set; }
    }

    public class ButtonClickedEvent : UnityEngine.Events.UnityEvent { }

    public class Button : Selectable
    {
        public ButtonClickedEvent onClick { get; } = new ButtonClickedEvent();
    }

    public class InputField : Selectable
    {
        public string text { get; set; } = "";
        public Text textComponent { get; set; }
    }

    public class Outline : UnityEngine.Behaviour
    {
        public Color effectColor { get; set; }
        public Vector2 effectDistance { get; set; }
    }

    public class CanvasScaler : UnityEngine.Behaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public float matchWidthOrHeight { get; set; }
    }

    public class GraphicRaycaster : UnityEngine.Behaviour { }

    public class LayoutGroup : UnityEngine.Behaviour
    {
        public RectOffset padding { get; set; }
    }

    public class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        public float spacing { get; set; }
        public bool childControlHeight { get; set; }
        public bool childControlWidth { get; set; }
        public bool childForceExpandHeight { get; set; }
        public bool childForceExpandWidth { get; set; }
    }

    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }

    public class LayoutElement : UnityEngine.Behaviour
    {
        public float preferredHeight { get; set; }
        public float preferredWidth { get; set; }
        public float minHeight { get; set; }
    }

}

namespace UnityEngine
{
    // CanvasGroup lives in UnityEngine proper in the real API.
    public class CanvasGroup : Behaviour
    {
        public float alpha { get; set; }
        public bool interactable { get; set; }
        public bool blocksRaycasts { get; set; }
    }
}
