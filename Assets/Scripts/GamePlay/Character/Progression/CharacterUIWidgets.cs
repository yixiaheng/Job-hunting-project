using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Characters
{
    /// <summary>养成面板共用布局构造器，编辑器和运行时使用同一套布局。</summary>
    public static class CharacterUIWidgets
    {
        public static readonly Color Background = new Color(.055f, .075f, .12f, .97f);
        public static readonly Color Gold = new Color(.95f, .81f, .52f);
        public static TMP_FontAsset Font => PlayerRuntime.Instance != null ? PlayerRuntime.Instance.Config.uiFont : Resources.Load<PlayerRuntimeConfig>("Bootstrap/PlayerRuntimeConfig")?.uiFont;

        public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Image Image(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var rect = Rect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TMP_Text Label(Transform parent, string value, int size = 36)
        {
            var rect = Rect("Text", parent, Vector2.zero, Vector2.one);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font;
            text.fontSize = size;
            text.text = value;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = size * 1.6f;
            return text;
        }

        public static Button Button(Transform parent, string title, UnityEngine.Events.UnityAction action = null)
        {
            var rect = Rect("Button", parent, Vector2.zero, Vector2.one);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.15f, .20f, .29f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 78;
            layout.preferredHeight = 86;
            var label = Label(rect, title, 32);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 24;
            label.fontSizeMax = 32;
            var lr = (RectTransform)label.transform;
            lr.offsetMin = new Vector2(12, 6);
            lr.offsetMax = new Vector2(-12, -6);
            if (action != null)
                button.onClick.AddListener(action);
            return button;
        }

        public static RectTransform Scroll(Transform parent, Vector2 min, Vector2 max)
        {
            var viewport = Image("Scroll", parent, min, max, Background);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            var content = Rect("Content", viewport.transform, new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(.5f, 1);
            var group = content.gameObject.AddComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(24, 24, 20, 24);
            group.spacing = 16;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying)
                    Object.Destroy(child);
                else
                    Object.DestroyImmediate(child);
            }
        }
    }
}
