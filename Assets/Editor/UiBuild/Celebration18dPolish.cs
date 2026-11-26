using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ItemCollector3D.EditorTools.UiKit;
using static ItemCollector3D.EditorTools.UiPalette;

namespace ItemCollector3D.EditorTools
{
    /// <summary>
    /// Stage 18D: the final layout of the four celebration screens (LEVEL COMPLETE of Green Mine and Rocky Canyon, GAME COMPLETE, 100% COLLECTION), applied to the EXISTING prefabs by object name, so every object keeps its identity
    /// (the scenes' prefab instances and the controllers' references stay valid and the scenes do not change). The builders call <see cref="ApplyLevelComplete"/> / <see cref="ApplyFinal"/> at the end of BuildPrefab, so a rebuild gives
    /// the same result. Only positions, sizes and text styles change: no component, event, text content or colour of the designs (wood + green / orange for the levels, royal violet + turquoise + gold for the finals) is touched.
    /// <list type="bullet">
    /// <item>One panel height (800) and one top: the banner overhangs 80 units, so the whole panel is 880 units high and is centred (window at y = -40). On an iPhone 15 Pro Max the safe-area root is 968 units high, so the title keeps
    /// 44 units to the top and the window 44 to the bottom (it was 10 / 78: the title nearly touched the edge).</item>
    /// <item>One rhythm: content block, 26 units, primary button (1000 x 150, the size of 17C / 17D), 26 units, two secondary buttons (500 x 126, 26 apart), 30+ units to the rim.</item>
    /// <item>Long lines keep at least 65 units from the rim; the three area rows of the collection are aligned in two columns; the player's gold sits on its own dark chip (LEVEL COMPLETE, 100% COLLECTION).</item>
    /// </list>
    /// </summary>
    public static class Celebration18dPolish
    {
        public const string LevelCompletePath = "Assets/Prefabs/UI/LevelComplete.prefab";
        public const string FinalCelebrationPath = "Assets/Prefabs/UI/FinalCelebration.prefab";

        public const float WindowHeight = 800f;
        public const float WindowY = -40f;
        public const float BannerHeight = 160f;
        public const float ButtonGap = 26f;

        private static readonly Vector2 PrimarySize = new(1000f, 150f);
        private static readonly Vector2 SecondarySize = new(500f, 126f);

        /// <summary>Applies the layout to the existing prefabs in place and saves them. Run via <c>script eval</c>.</summary>
        public static string PolishPrefabs()
        {
            string result = string.Empty;
            foreach ((string path, bool level) in new[] { (LevelCompletePath, true), (FinalCelebrationPath, false) })
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (level)
                    {
                        ApplyLevelComplete(root);
                    }
                    else
                    {
                        ApplyFinal(root);
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    result += path + " polished; ";
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return result;
        }

        // ================================================================ LEVEL COMPLETE

        public static void ApplyLevelComplete(GameObject root)
        {
            RectTransform window = Rt(root.transform, "Panel/Window");
            Window(window, new Vector2(1500f, WindowHeight), 44f, 0f);
            Banner(window, new Vector2(1060f, BannerHeight), 98f);

            // the picture (458 x 320, its top 19 below the banner) in its coloured frame
            var frameCentre = new Vector2(-420f, 141f);
            Put(Rt(window, "PictureOutline"), frameCentre.x, frameCentre.y, 470f, 332f);
            Put(Rt(window, "PictureFrame"), frameCentre.x, frameCentre.y, 458f, 320f);
            Put(Rt(window, "Picture"), frameCentre.x, frameCentre.y, 430f, 292f);
            Rt(Rt(window, "PictureOutline"), "Shadow").sizeDelta = new Vector2(458f + 44f, 320f + 44f); // the soft shadow under the frame follows its size

            // the right column: area, divider, treasures, area gold, then the player's own gold on a dark chip. The boxes stay a good 30 % taller than their text.
            const float x = 290f;
            At(Rt(window, "AreaName"), x, 230f);
            At(Rt(window, "Divider"), x, 182f);
            At(Rt(window, "Treasures"), x, 134f);
            At(Rt(window, "Gold"), x, 82f);
            RectTransform total = Rt(window, "TotalGold");
            Put(total, x, 32f, 560f, 58f);
            var totalText = total.GetComponent<TextMeshProUGUI>();
            totalText.font = UiFonts.Font;
            totalText.fontSharedMaterial = UiFonts.Load("UI_Title_Gold");
            totalText.fontSize = 40f;
            totalText.enableAutoSizing = true;
            totalText.fontSizeMin = 24f;
            totalText.fontSizeMax = 40f;
            Chip(window, total, new Vector2(x, 32f), new Vector2(600f, 62f));

            // the buttons: GO TO (primary), STAY HERE and AREA SELECTION (secondary), 26 units apart
            ResizeButton(Rt(window, "NextButton"), 0f, -126f, PrimarySize);
            ResizeButton(Rt(window, "StayButton"), -(SecondarySize.x + ButtonGap) * 0.5f, -290f, SecondarySize);
            ResizeButton(Rt(window, "AreaSelectionButton"), (SecondarySize.x + ButtonGap) * 0.5f, -290f, SecondarySize);

            // the trophy on the picture's corner, its halo, the burst start and the twinkles around the banner
            var trophy = new Vector2(-622f, 282f);
            At(Rt(window, "Halo"), trophy);
            At(Rt(window, "Trophy"), trophy);
            for (int i = 0; i < 10; i++)
            {
                At(Rt(window, "BurstStar" + i), trophy);
            }

            At(Rt(window, "TwinkleA"), -610f, 452f);
            At(Rt(window, "TwinkleB"), 610f, 452f);
            At(Rt(window, "TwinkleC"), -715f, 426f); // clear of the trophy's top (the trophy reaches y = 382)
            At(Rt(window, "TwinkleD"), 715f, 426f);
        }

        // ================================================================ GAME COMPLETE and 100% COLLECTION

        public static void ApplyFinal(GameObject root)
        {
            ApplyGameComplete(Rt(root.transform, "Panel/GameCompleteWindow"));
            ApplyCollection(Rt(root.transform, "Panel/CollectionWindow"));
        }

        private static void ApplyGameComplete(RectTransform window)
        {
            Window(window, new Vector2(1500f, WindowHeight), 50f, 1.28f);
            Banner(window, new Vector2(1120f, BannerHeight), 104f);
            Put(Rt(window, "Subtitle"), 0f, 284f, 1300f, 60f);
            Rt(window, "Subtitle").GetComponent<TMP_Text>().fontSizeMax = 50f;

            // the Ancient Mine picture (400 x 270) in its gold and turquoise frame
            var frameCentre = new Vector2(-430f, 103f);
            Put(Rt(window, "PictureGlow"), frameCentre.x, frameCentre.y, 600f, 405f);
            Put(Rt(window, "PictureOutline"), frameCentre.x, frameCentre.y, 416f, 286f);
            Put(Rt(window, "PictureFrame"), frameCentre.x, frameCentre.y, 400f, 270f);
            Put(Rt(window, "PictureInner"), frameCentre.x, frameCentre.y, 386f, 256f);
            Put(Rt(window, "Picture"), frameCentre.x, frameCentre.y, 372f, 242f);
            Rt(Rt(window, "PictureOutline"), "Shadow").sizeDelta = new Vector2(400f + 48f, 270f + 48f);

            // the right column (65 units to the rim): the gold in big letters, the goal under it, the rest in even steps; every box about 30 % taller than its text
            const float x = 295f;
            Put(Rt(window, "TotalGold"), x, 198f, 780f, 88f);
            Rt(window, "TotalGold").GetComponent<TMP_Text>().fontSizeMax = 68f;
            At(Rt(window, "Divider"), x, 150f);
            foreach ((string name, float y) in new[] { ("Goal", 112f), ("Treasures", 64f), ("Areas", 16f) })
            {
                Put(Rt(window, name), x, y, 780f, 60f);
                Rt(window, name).GetComponent<TMP_Text>().fontSizeMax = 44f;
            }

            RectTransform message = Rt(window, "Message");
            Put(message, x, -30f, 780f, 50f);
            var messageText = message.GetComponent<TextMeshProUGUI>();
            messageText.font = UiFonts.Font;
            messageText.fontSharedMaterial = UiFonts.Load("UI_Title_Gold");
            messageText.fontSize = 36f;
            messageText.enableAutoSizing = true;
            messageText.fontSizeMin = 26f;
            messageText.fontSizeMax = 36f;

            FinalButtons(window);

            var trophy = new Vector2(-612f, 214f);
            Put(Rt(window, "Halo"), trophy.x, trophy.y, 380f, 380f);
            Put(Rt(window, "Trophy"), trophy.x, trophy.y, 200f, 200f);
            for (int i = 0; i < 12; i++)
            {
                At(Rt(window, "BurstStar" + i), trophy);
            }

            At(Rt(window, "TwinkleA"), -640f, 452f);
            At(Rt(window, "TwinkleB"), 640f, 452f);
            At(Rt(window, "TwinkleC"), -705f, 372f);
            At(Rt(window, "TwinkleD"), 705f, 372f);
            At(Rt(window, "TwinkleE"), -300f, 478f);
            At(Rt(window, "TwinkleF"), 310f, 478f);
        }

        private static void ApplyCollection(RectTransform window)
        {
            Window(window, new Vector2(1360f, WindowHeight), 50f, 1.28f);
            Banner(window, new Vector2(1180f, BannerHeight), 90f);
            Put(Rt(window, "Subtitle"), 0f, 284f, 1200f, 60f);
            Rt(window, "Subtitle").GetComponent<TMP_Text>().fontSizeMax = 50f;

            // the badge: a coin (270) with 60 / 60 that still reads first, on the same centre as the picture of GAME COMPLETE
            var badgeCentre = new Vector2(-420f, 103f);
            Put(Rt(window, "Halo"), badgeCentre.x, badgeCentre.y, 420f, 420f);
            RectTransform group = Rt(window, "BadgeGroup");
            Put(group, badgeCentre.x, badgeCentre.y, 270f, 270f);
            Put(Rt(group, "BadgeRing"), 0f, 0f, 270f, 270f);
            Put(Rt(group, "Badge"), 0f, 0f, 246f, 246f);
            Put(Rt(group, "Count"), 0f, 16f, 210f, 100f);
            Rt(group, "Count").GetComponent<TMP_Text>().fontSizeMax = 76f;
            Put(Rt(group, "CountCaption"), 0f, -50f, 210f, 46f);
            Rt(group, "CountCaption").GetComponent<TMP_Text>().fontSizeMax = 34f;
            Put(Rt(group, "BadgeStar"), 0f, 90f, 46f, 46f);
            for (int i = 0; i < 10; i++)
            {
                At(Rt(window, "BurstStar" + i), badgeCentre);
            }

            // three rows: a check, the area name at the left, the count in a column of its own (the controller puts <pos=50%> between them)
            const float x = 290f;
            for (int i = 0; i < 3; i++)
            {
                float y = 190f - i * 58f;
                Put(Rt(window, "Check" + i), x - 380f, y, 50f, 50f);
                Put(Rt(window, "AreaRow" + i), x + 30f, y, 720f, 64f);
            }

            At(Rt(window, "Divider"), x, 36f);
            RectTransform total = Rt(window, "TotalGold");
            Put(total, x, -8f, 540f, 72f);
            total.GetComponent<TMP_Text>().fontSizeMax = 52f;
            Chip(window, total, new Vector2(x, -8f), new Vector2(580f, 64f));

            FinalButtons(window);

            At(Rt(window, "TwinkleA"), -610f, 452f);
            At(Rt(window, "TwinkleB"), 610f, 452f);
            At(Rt(window, "TwinkleC"), -655f, 372f);
            At(Rt(window, "TwinkleD"), 655f, 372f);
        }

        private static void FinalButtons(RectTransform window)
        {
            ResizeButton(Rt(window, "ContinueButton"), 0f, -141f, PrimarySize);
            ResizeButton(Rt(window, "AreaSelectionButton"), -(SecondarySize.x + ButtonGap) * 0.5f, -305f, SecondarySize);
            ResizeButton(Rt(window, "MainMenuButton"), (SecondarySize.x + ButtonGap) * 0.5f, -305f, SecondarySize);
        }

        // ================================================================ pieces

        /// <summary>The window and the pieces that depend on its size (shadow, aura, sheen, floor glow); every piece is found by name, the royal ones only when they exist.</summary>
        private static void Window(RectTransform window, Vector2 size, float shadowGrow, float auraScale)
        {
            window.anchoredPosition = new Vector2(0f, WindowY);
            window.sizeDelta = size;
            Rt(window, "Shadow").sizeDelta = size + Vector2.one * shadowGrow * 2f;
            if (auraScale > 0f)
            {
                Rt(window, "Aura").sizeDelta = size * auraScale;
                Rt(window, "Sheen").sizeDelta = new Vector2(size.x * 0.9f, size.y * 0.6f);
                Rt(window, "FloorGlow").sizeDelta = new Vector2(size.x * 0.9f, size.y * 0.4f);
            }
        }

        private static void Banner(RectTransform window, Vector2 size, float fontSize)
        {
            RectTransform banner = Rt(window, "TitleBanner");
            banner.sizeDelta = size;
            Rt(banner, "Shadow").sizeDelta = size + Vector2.one * 48f;
            Rt(banner, "Heading").GetComponent<TMP_Text>().fontSize = fontSize;
            if (Rt(banner, "Heading").GetComponent<TMP_Text>().enableAutoSizing)
            {
                Rt(banner, "Heading").GetComponent<TMP_Text>().fontSizeMax = fontSize;
            }
        }

        /// <summary>The same arithmetic as <c>UiKit.TextButton</c> for a new size: shadow, icon and label keep their place relative to the button.</summary>
        private static void ResizeButton(RectTransform button, float x, float y, Vector2 size)
        {
            Put(button, x, y, size.x, size.y);
            Rt(button, "Shadow").sizeDelta = size + Vector2.one * 64f;
            Transform iconTransform = button.Find("Icon");
            if (iconTransform != null)
            {
                float iconSize = Mathf.Min(size.y * 0.6f, 100f);
                var icon = (RectTransform)iconTransform;
                icon.anchoredPosition = new Vector2(size.y * 0.28f, 6f);
                icon.sizeDelta = new Vector2(iconSize, iconSize);
            }

            float iconInset = iconTransform != null ? size.y * 0.5f + 14f : 0f;
            var label = Rt(button, "Label");
            label.offsetMin = new Vector2(iconInset * 0.55f, 14f);
            label.offsetMax = Vector2.zero;
        }

        /// <summary>A dark rounded chip behind a line (the player's own gold), so it never reads as one more line of the statistics above it.</summary>
        private static void Chip(RectTransform window, RectTransform line, Vector2 centre, Vector2 size)
        {
            Transform old = window.Find("TotalChip");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            Image chip = Img(window, "TotalChip", S("ui_pill"), Alpha(NavyDeep, 0.62f));
            chip.raycastTarget = false;
            Center(chip.rectTransform, centre, size);
            chip.gameObject.layer = line.gameObject.layer;
            chip.transform.SetSiblingIndex(line.GetSiblingIndex());
        }

        private static RectTransform Rt(Transform parent, string path)
        {
            Transform t = parent.Find(path);
            if (t == null)
            {
                throw new System.InvalidOperationException($"Celebration18dPolish: '{path}' not found under '{parent.name}'");
            }

            return (RectTransform)t;
        }

        private static void At(RectTransform rt, Vector2 position) => rt.anchoredPosition = position;

        private static void At(RectTransform rt, float x, float y) => rt.anchoredPosition = new Vector2(x, y);

        private static void Put(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
