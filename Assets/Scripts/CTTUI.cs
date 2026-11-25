using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CatchTheTroublemaker
{
    /// <summary>Small uGUI factory used to build the whole interface at runtime.</summary>
    public static class CttUi
    {
        private static Font cachedFont;

        public static Color Background => CTTConfig.Parse("#10152f");
        public static Color Panel => CTTConfig.Parse("#1b2350");
        public static Color Accent => CTTConfig.Parse("#ffd166");
        public static Color Accent2 => CTTConfig.Parse("#4cc9f0");
        public static Color Muted => CTTConfig.Parse("#a9b4e0");
        public static Color Danger => CTTConfig.Parse("#ef476f");

        public static Font Font
        {
            get
            {
                if (cachedFont != null) return cachedFont;
                foreach (string name in new[] { "LegacyRuntime.ttf", "Inter-Regular.ttf", "Arial.ttf" })
                {
                    try
                    {
                        var font = Resources.GetBuiltinResource<Font>(name);
                        if (font != null) { cachedFont = font; return font; }
                    }
                    catch { /* try the next built-in name */ }
                }
                return null;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform PanelAt(string name, Transform parent, Color color)
        {
            var rt = Rect(name, parent);
            Stretch(rt);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            return rt;
        }

        public static void Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color,
            TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = Rect(name, parent);
            Stretch(rt);
            var label = rt.gameObject.AddComponent<Text>();
            if (Font != null) label.font = Font;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        public static Text LabelAt(Transform parent, string name, string text, int size, Color color,
            Vector2 anchor, Vector2 dimensions, Vector2 position, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var label = Label(parent, name, text, size, color, align);
            Place(label.rectTransform, anchor, dimensions, position);
            return label;
        }

        public static Button Button(Transform parent, string name, string text, Color color, Action onClick,
            Vector2 anchor, Vector2 size, Vector2 position, int fontSize = 30)
        {
            var rt = Rect(name, parent);
            Place(rt, anchor, size, position);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rt.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.25f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
            colors.disabledColor = new Color(0.35f, 0.38f, 0.5f, 0.8f);
            button.colors = colors;

            // Shared UI feedback for every button built by the UI factory.
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (button.interactable) CttSfx.Instance?.PlayHover(); });
            var trigger = rt.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Add(enter);

            button.onClick.AddListener(() => CttSfx.Instance?.PlayPress());
            if (onClick != null) button.onClick.AddListener(() => onClick());
            Label(rt, "Caption", text, fontSize, Color.white);
            return button;
        }

        public static InputField Field(Transform parent, string name, string placeholder, string value,
            Vector2 anchor, Vector2 size, Vector2 position)
        {
            var rt = Rect(name, parent);
            Place(rt, anchor, size, position);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = CTTConfig.Parse("#0d1230");
            var field = rt.gameObject.AddComponent<InputField>();
            field.lineType = InputField.LineType.SingleLine;
            field.caretColor = Accent;
            field.selectionColor = new Color(0.3f, 0.5f, 1f, 0.5f);

            var text = Label(rt, "Text", value, 28, Color.white, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform, 16, 16, 6, 6);
            var hint = Label(rt, "Placeholder", placeholder, 26, Muted, TextAnchor.MiddleLeft);
            Stretch(hint.rectTransform, 16, 16, 6, 6);

            field.textComponent = text;
            field.placeholder = hint;
            field.text = value;
            return field;
        }

        /// <summary>Flat coloured square used everywhere as a character icon.</summary>
        public static Image Icon(Transform parent, string name, Color color, Vector2 size,
            Vector2 anchor, Vector2 position)
        {
            var rt = Rect(name, parent);
            Place(rt, anchor, size, position);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Character sprite used on the family selection cards.</summary>
        public static Image SpriteImage(Transform parent, string name, Sprite sprite,
            Vector2 size, Vector2 anchor, Vector2 position)
        {
            var rt = Rect(name, parent);
            Place(rt, anchor, size, position);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Coloured square with a light frame, used to mark the local player.</summary>
        public static Image FramedIcon(Transform parent, string name, Color color, Vector2 size,
            Vector2 anchor, Vector2 position, float frame = 4f)
        {
            Icon(parent, name + "Frame", Color.white,
                new Vector2(size.x + frame * 2f, size.y + frame * 2f), anchor, position);
            return Icon(parent, name, color, size, anchor, position);
        }

        /// <summary>Push a button caption aside so a square icon can sit on its left.</summary>
        public static void InsetCaption(Button button, float left)
        {
            var caption = button.GetComponentInChildren<Text>();
            if (caption == null) return;
            caption.rectTransform.anchorMin = Vector2.zero;
            caption.rectTransform.anchorMax = Vector2.one;
            caption.rectTransform.offsetMin = new Vector2(left, 0f);
            caption.rectTransform.offsetMax = new Vector2(-8f, 0f);
            caption.alignment = TextAnchor.MiddleLeft;
        }

        public static RawImage BoardImage(Transform parent)
        {
            var rt = Rect("Board", parent);
            Stretch(rt);
            var image = rt.gameObject.AddComponent<RawImage>();
            image.color = Color.white;
            return image;
        }
    }
}
