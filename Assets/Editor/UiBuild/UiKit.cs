using ItemCollector3D.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ItemCollector3D.EditorTools.UiPalette;

namespace ItemCollector3D.EditorTools
{
    /// <summary>Construction helpers for the UI design system: every screen is assembled from these pieces so all of it looks alike.</summary>
    public static class UiKit
    {
        public enum ButtonStyle { Gold, Wood, Green, Navy }

        public static Sprite S(string name) => UiSpriteFactory.Load(name);

        /// <summary>Share of the screen height kept free at the left, right and bottom edges for the Android system gestures (about 22 dp on a phone).</summary>
        public const float GestureMarginFraction = 0.055f;

        /// <summary>
        /// The one canvas setup of every UI canvas: reference 1920 x 1080 and screen match mode EXPAND. Expand scales by the SMALLER of width / height,
        /// so the whole 1080 unit high reference layout always fits on screen: a wider phone only adds width (it used to shrink the canvas height
        /// to about 890 units at 2.6:1 and cut the bottom of the menu). On every screen 16:9 or wider one unit is the same share of the screen height.
        /// </summary>
        public static void ConfigureScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        /// <summary>Keeps tappable UI clear of the Android gesture zones (see <see cref="SafeAreaFitter"/>).</summary>
        public static void ConfigureSafeArea(SafeAreaFitter fitter)
        {
            var so = new SerializedObject(fitter);
            so.FindProperty("gestureMarginFraction").floatValue = GestureMarginFraction;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ rects

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchors the rect to one point (anchor = pivot = normalized point) and sets its anchored position and size.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Center(RectTransform rt, Vector2 position, Vector2 size) => Place(rt, new Vector2(0.5f, 0.5f), position, size);

        public static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(t.GetChild(i).gameObject);
            }
        }

        // ------------------------------------------------------------------ images

        public static Image Img(Transform parent, string name, Sprite sprite, Color color, bool sliced = true, bool raycast = false)
        {
            RectTransform rt = Rect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite != null && sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = raycast;
            image.preserveAspect = sprite != null && !sliced;
            return image;
        }

        /// <summary>Full-bleed dim that also covers display cut-outs outside the safe area and blocks touches behind a panel.</summary>
        public static Image Dim(Transform parent, float alpha)
        {
            Image dim = Img(parent, "Dim", null, new Color(0.03f, 0.05f, 0.09f, alpha), false, true);
            Stretch(dim.rectTransform, -700, -700, -700, -700);
            return dim;
        }

        public static Image Shadow(Transform parent, Vector2 size, Vector2 offset, float alpha = 0.55f, float grow = 56f)
        {
            Image shadow = Img(parent, "Shadow", S("ui_shadow"), new Color(0f, 0f, 0f, alpha));
            Center(shadow.rectTransform, offset, size + Vector2.one * grow * 2f);
            return shadow;
        }

        // ------------------------------------------------------------------ text

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, string materialName, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            RectTransform rt = Rect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = UiFonts.Font;
            tmp.fontSharedMaterial = UiFonts.Load(materialName);
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            tmp.margin = Vector4.zero;
            return tmp;
        }

        /// <summary>Small readable text (row labels, values, taglines): the body font, not the slanted display font.</summary>
        public static TextMeshProUGUI BodyText(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, string materialName = "UI_Body_Cream")
        {
            TextMeshProUGUI tmp = Text(parent, name, text, size, materialName, color, alignment);
            tmp.font = UiFonts.BodyFont;
            tmp.fontSharedMaterial = UiFonts.Load(materialName);
            return tmp;
        }

        // ------------------------------------------------------------------ buttons

        public static string SpriteFor(ButtonStyle style) => style switch
        {
            ButtonStyle.Gold => "ui_btn_gold",
            ButtonStyle.Wood => "ui_btn_wood",
            ButtonStyle.Green => "ui_btn_green",
            _ => "ui_btn_navy",
        };

        public static string TextMaterialFor(ButtonStyle style) => style switch
        {
            ButtonStyle.Gold => "UI_Text_GoldDeep",
            ButtonStyle.Wood => "UI_Text_Brown",
            ButtonStyle.Green => "UI_Text_Green",
            _ => "UI_Text_Navy",
        };

        /// <summary>
        /// A chunky button: container (the Button, feedback, entrance) -> Shadow, Face (the touch target, tinted on press), Icon, Label.
        /// The container size is the touch size.
        /// </summary>
        public static Button TextButton(Transform parent, string name, string label, ButtonStyle style, Vector2 size, float fontSize, string icon = null,
            bool pulse = false)
        {
            RectTransform root = Rect(name, parent);
            root.sizeDelta = size;
            Shadow(root, size, new Vector2(0f, -10f), 0.38f, 32f);

            Image face = Img(root, "Face", S(SpriteFor(style)), Color.white, true, true);
            Stretch(face.rectTransform);

            RectTransform labelRect = null;
            float iconSize = Mathf.Min(size.y * 0.6f, 100f);
            float iconInset = icon != null ? size.y * 0.5f + 14f : 0f;
            if (icon != null)
            {
                Image iconImage = Img(root, "Icon", S(icon), Color.white, false);
                Place(iconImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(size.y * 0.28f, 6f), new Vector2(iconSize, iconSize));
            }

            TextMeshProUGUI text = Text(root, "Label", label, fontSize, TextMaterialFor(style), Cream);
            labelRect = text.rectTransform;
            Stretch(labelRect, iconInset * 0.55f, 14f, 0f, 0f); // lip of the sprite sits at the bottom: lift the text a little
            labelRect.offsetMax = new Vector2(0f, 0f);
            text.verticalAlignment = VerticalAlignmentOptions.Middle;

            return FinishButton(root, face, labelRect, pulse);
        }

        public static Button RoundButton(Transform parent, string name, string spriteName, string iconName, float size, float iconSize)
        {
            RectTransform root = Rect(name, parent);
            root.sizeDelta = new Vector2(size, size);
            Shadow(root, new Vector2(size, size), new Vector2(0f, -8f), 0.38f, 24f);
            Image face = Img(root, "Face", S(spriteName), Color.white, false, true);
            Stretch(face.rectTransform);
            face.preserveAspect = true;
            Image icon = Img(root, "Icon", S(iconName), Color.white, false);
            Center(icon.rectTransform, new Vector2(0f, 4f), new Vector2(iconSize, iconSize));
            return FinishButton(root, face, icon.rectTransform, false);
        }

        private static Button FinishButton(RectTransform root, Image face, RectTransform label, bool pulse)
        {
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
            colors.pressedColor = new Color(0.8f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var feedback = root.gameObject.AddComponent<UiButtonFeedback>();
            var so = new SerializedObject(feedback);
            so.FindProperty("target").objectReferenceValue = root;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("idlePulse").boolValue = pulse;
            so.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        public static UiEntrance Entrance(GameObject go, UiEntrance.Style style, float delay = 0f, float duration = 0.34f, float distance = 160f)
        {
            var entrance = go.AddComponent<UiEntrance>();
            var so = new SerializedObject(entrance);
            so.FindProperty("style").enumValueIndex = (int)style;
            so.FindProperty("delay").floatValue = delay;
            so.FindProperty("duration").floatValue = duration;
            so.FindProperty("distance").floatValue = distance;
            so.ApplyModifiedPropertiesWithoutUndo();
            return entrance;
        }

        // ------------------------------------------------------------------ panels

        /// <summary>The wood window: soft shadow, frame, grain overlay. Returns the window rect (children go inside).</summary>
        public static RectTransform WoodWindow(Transform parent, Vector2 size, string name = "Window")
        {
            RectTransform window = Rect(name, parent);
            Center(window, Vector2.zero, size);
            Shadow(window, size, new Vector2(0f, -14f), 0.34f, 44f); // lighter than stage 8 (0.6 / 64)
            Image frame = Img(window, "Frame", S("ui_panel_wood"), Color.white, true, true);
            Stretch(frame.rectTransform);
            Image grain = Img(window, "Grain", S("ui_wood_grain"), new Color(1f, 1f, 1f, 0.55f), false);
            grain.type = Image.Type.Tiled;
            Stretch(grain.rectTransform, 30f, 30f, 30f, 30f);
            return window;
        }

        public static RectTransform TitleBanner(Transform parent, string text, Vector2 size, float fontSize, Vector2 position, string name = "TitleBanner")
        {
            RectTransform banner = Rect(name, parent);
            Place(banner, new Vector2(0.5f, 1f), position, size);
            banner.pivot = new Vector2(0.5f, 0.5f);
            Shadow(banner, size, new Vector2(0f, -8f), 0.32f, 24f);
            Image image = Img(banner, "Bg", S("ui_banner"), Color.white);
            Stretch(image.rectTransform);
            TextMeshProUGUI title = Text(banner, "Heading", text, fontSize, "UI_Title_Gold", Gold); // not "Title": that name belongs to the game title
            Stretch(title.rectTransform, 0, 6, 0, 0);
            title.verticalAlignment = VerticalAlignmentOptions.Middle;
            return banner;
        }

        public static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
