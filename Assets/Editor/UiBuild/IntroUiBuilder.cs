using ItemCollector3D.Menus;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ItemCollector3D.EditorTools.UiKit;
using static ItemCollector3D.EditorTools.UiPalette;

namespace ItemCollector3D.EditorTools
{
    /// <summary>
    /// Builds the three-page introduction (stage 17A, shown after NEW GAME) into the Main Menu prefab: a full-screen overlay with a framed picture on the left (the approved area preview pictures,
    /// plus small signal / DIG / COLLECT chips on page 2 and three area cards on page 3), the page title and text on the right, page dots and the navigation buttons at the bottom. Everything comes
    /// from the existing design system (wood frame, gold / wood / navy buttons, Bangers + body fonts); no new art. <see cref="Rebuild"/> replaces only this panel in the prefab.
    /// </summary>
    public static class IntroUiBuilder
    {
        public const string PanelName = "IntroPanel";

        private static readonly string[] Bodies =
        {
            "Become a treasure hunter! Explore three mysterious mines, follow your detector and find every hidden treasure.",
            "Walk with the joystick and listen to the detector: SLOW, FAST, then RAPID. When DIG appears, dig it up and tap COLLECT to take your treasure.",
            "Gold opens new places. Start in Green Mine, unlock Rocky Canyon with 500 Gold and Ancient Mine with 1200 Gold. Can you find them all?",
        };

        /// <summary>Replaces only the introduction panel inside the Main Menu prefab (everything else stays as it is).</summary>
        public static void Rebuild()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(MenuUiBuilder.MainMenuPath);
            try
            {
                Transform old = root.transform.Find(PanelName);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }

                IntroSlidesController controller = BuildPanel(root.transform);
                SetRef(root.GetComponent<MainMenuController>(), "introPanel", controller);
                PrefabUtility.SaveAsPrefabAsset(root, MenuUiBuilder.MainMenuPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }

        public static IntroSlidesController BuildPanel(Transform menuRoot)
        {
            RectTransform panel = Rect(PanelName, menuRoot);
            Stretch(panel);
            var controller = panel.gameObject.AddComponent<IntroSlidesController>();

            // a solid backdrop far larger than any screen (display cut-outs, the bars above and below the safe area, tall phones): the Main Menu never shows through, and it catches every touch
            Image backdrop = Img(panel, "Backdrop", null, new Color(0.03f, 0.05f, 0.09f, 1f), false, true);
            Stretch(backdrop.rectTransform, -4000f, -4000f, -4000f, -4000f);
            Image vignette = Img(panel, "Vignette", S("ui_vignette"), new Color(1f, 1f, 1f, 0.9f), false);
            vignette.preserveAspect = false;
            Stretch(vignette.rectTransform, -300f, -300f, -300f, -300f);

            // ---- the frame of the picture (common to all pages), left
            var frameSize = new Vector2(940f, 650f);
            var pictureCentre = new Vector2(-430f, 70f);
            RectTransform frame = Rect("PictureFrame", panel);
            Center(frame, pictureCentre, frameSize);
            Shadow(frame, frameSize, new Vector2(0f, -14f), 0.4f, 44f);
            Image frameImage = Img(frame, "Frame", S("ui_panel_wood"), Color.white);
            Stretch(frameImage.rectTransform);
            var pictureSize = new Vector2(880f, 600f);

            var pages = new CanvasGroup[IntroSlidesController.PageCount];
            for (int i = 0; i < pages.Length; i++)
            {
                RectTransform page = Rect("Page_" + (i + 1), panel);
                Stretch(page);
                pages[i] = page.gameObject.AddComponent<CanvasGroup>();
                pages[i].blocksRaycasts = false;

                // picture (inside the frame)
                RectTransform picture = Rect("Picture", page);
                Center(picture, pictureCentre, pictureSize);
                switch (i)
                {
                    case 0:
                        Picture(picture, "area_green_mine", pictureSize);
                        break;
                    case 1:
                        Picture(picture, "area_rocky_canyon", pictureSize);
                        SignalStrip(picture, pictureSize);
                        break;
                    default:
                        AreaCards(picture, pictureSize);
                        break;
                }

                // title + text (right column)
                TextMeshProUGUI title = Text(page, "Heading", IntroSlidesController.Titles[i], 92f, "UI_Title_Gold", GoldLight, TextAlignmentOptions.TopLeft);
                title.textWrappingMode = TextWrappingModes.Normal;
                title.enableAutoSizing = true;
                title.fontSizeMin = 56f;
                title.fontSizeMax = 92f;
                title.lineSpacing = -12f;
                Center(title.rectTransform, new Vector2(520f, 235f), new Vector2(740f, 250f));

                TextMeshProUGUI body = BodyText(page, "Body", Bodies[i], 42f, Cream, TextAlignmentOptions.TopLeft);
                body.textWrappingMode = TextWrappingModes.Normal;
                body.lineSpacing = 6f;
                Center(body.rectTransform, new Vector2(520f, -85f), new Vector2(740f, 370f));
            }

            // ---- page dots, under the picture
            var dots = new Image[IntroSlidesController.PageCount];
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i] = Img(panel, "Dot_" + (i + 1), S("ui_dot"), Color.white, false);
                Center(dots[i].rectTransform, new Vector2(pictureCentre.x + (i - 1) * 56f, -300f), new Vector2(30f, 30f));
            }

            // ---- navigation: BACK left, SKIP middle, NEXT / LET'S PLAY right
            const float navY = -405f, navHeight = 140f;
            Button back = TextButton(panel, "IntroBackButton", "BACK", ButtonStyle.Navy, new Vector2(340f, navHeight), 62f, "icon_chev_l");
            Center(back.GetComponent<RectTransform>(), new Vector2(-600f, navY), new Vector2(340f, navHeight));
            Button skip = TextButton(panel, "IntroSkipButton", "SKIP", ButtonStyle.Wood, new Vector2(340f, navHeight), 62f);
            Center(skip.GetComponent<RectTransform>(), new Vector2(0f, navY), new Vector2(340f, navHeight));
            Button next = TextButton(panel, "IntroNextButton", "NEXT", ButtonStyle.Gold, new Vector2(380f, navHeight), 70f, "icon_chev_r");
            Center(next.GetComponent<RectTransform>(), new Vector2(590f, navY), new Vector2(380f, navHeight));
            Button play = TextButton(panel, "IntroPlayButton", "LET'S PLAY", ButtonStyle.Gold, new Vector2(560f, navHeight), 70f, "icon_play", true);
            Center(play.GetComponent<RectTransform>(), new Vector2(500f, navY), new Vector2(560f, navHeight));

            var so = new SerializedObject(controller);
            SerializedProperty pageArray = so.FindProperty("pages");
            pageArray.arraySize = pages.Length;
            for (int i = 0; i < pages.Length; i++)
            {
                pageArray.GetArrayElementAtIndex(i).objectReferenceValue = pages[i];
            }

            SerializedProperty dotArray = so.FindProperty("dots");
            dotArray.arraySize = dots.Length;
            for (int i = 0; i < dots.Length; i++)
            {
                dotArray.GetArrayElementAtIndex(i).objectReferenceValue = dots[i];
            }

            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("skipButton").objectReferenceValue = skip;
            so.FindProperty("nextButton").objectReferenceValue = next;
            so.FindProperty("playButton").objectReferenceValue = play;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.SetAsLastSibling();
            panel.gameObject.SetActive(false);
            return controller;
        }

        private static Sprite Area(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{AreaPreviewArt.Folder}/{name}.png");

        private static void Picture(RectTransform parent, string spriteName, Vector2 size)
        {
            Image image = Img(parent, "Art", Area(spriteName), Color.white, false);
            image.preserveAspect = true;
            Stretch(image.rectTransform);
        }

        /// <summary>Page 2: the detector's three levels and the two action buttons, as little chips over the bottom of the picture.</summary>
        private static void SignalStrip(RectTransform picture, Vector2 size)
        {
            RectTransform strip = Rect("SignalStrip", picture);
            Place(strip, new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(size.x - 60f, 112f));
            Image bg = Img(strip, "Bg", S("ui_pill"), new Color(NavyDeep.r, NavyDeep.g, NavyDeep.b, 0.9f));
            Stretch(bg.rectTransform);
            Chip(strip, "SLOW", GreenLight, new Vector2(-330f, 0f), new Vector2(130f, 64f), 36f);
            Chip(strip, "FAST", GoldLight, new Vector2(-185f, 0f), new Vector2(130f, 64f), 36f);
            Chip(strip, "RAPID", Hex("#F0773F"), new Vector2(-30f, 0f), new Vector2(140f, 64f), 36f);
            Image dig = Img(strip, "DigChip", S("ui_btn_gold"), Color.white);
            Center(dig.rectTransform, new Vector2(110f, 4f), new Vector2(120f, 74f));
            TextMeshProUGUI digLabel = Text(dig.rectTransform, "Label", "DIG", 42f, "UI_Text_GoldDeep", Cream);
            Stretch(digLabel.rectTransform, 0f, 8f, 0f, 0f);
            Image collect = Img(strip, "CollectChip", S("ui_btn_green"), Color.white);
            Center(collect.rectTransform, new Vector2(290f, 4f), new Vector2(180f, 74f));
            TextMeshProUGUI collectLabel = Text(collect.rectTransform, "Label", "COLLECT", 36f, "UI_Text_Green", Cream);
            Stretch(collectLabel.rectTransform, 0f, 8f, 0f, 0f);
        }

        private static void Chip(Transform parent, string label, Color colour, Vector2 position, Vector2 size, float fontSize)
        {
            Image chip = Img(parent, label + "Chip", S("ui_pill"), colour);
            Center(chip.rectTransform, position, size);
            TextMeshProUGUI text = Text(chip.rectTransform, "Label", label, fontSize, "UI_Title_Gold", Cream);
            Stretch(text.rectTransform, 0f, 4f, 0f, 0f);
        }

        /// <summary>Page 3: the three areas in order, each with what it takes to get in.</summary>
        private static void AreaCards(RectTransform picture, Vector2 size)
        {
            Image inset = Img(picture, "Backdrop", S("ui_row_inset"), new Color(NavyDeep.r, NavyDeep.g, NavyDeep.b, 0.96f));
            Stretch(inset.rectTransform);
            string[] sprites = { "area_green_mine", "area_rocky_canyon", "area_ancient_mine" };
            string[] names = { "GREEN MINE", "ROCKY CANYON", "ANCIENT MINE" };
            string[] needs = { "START HERE", "500 GOLD", "1200 GOLD" };
            const float cardWidth = 250f, cardHeight = 171f, gap = 50f;
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * (cardWidth + gap);
                RectTransform card = Rect("Card_" + (i + 1), picture);
                Center(card, new Vector2(x, 70f), new Vector2(cardWidth, cardHeight));
                Shadow(card, new Vector2(cardWidth, cardHeight), new Vector2(0f, -8f), 0.4f, 18f);
                Image art = Img(card, "Art", Area(sprites[i]), Color.white, false);
                Stretch(art.rectTransform);
                TextMeshProUGUI label = Text(card, "Name", names[i], 40f, "UI_Title_Gold", GoldLight);
                Center(label.rectTransform, new Vector2(0f, -cardHeight * 0.5f - 44f), new Vector2(cardWidth + 40f, 56f));
                Chip(card, needs[i], i == 0 ? GreenLight : GoldLight, new Vector2(0f, -cardHeight * 0.5f - 112f), new Vector2(cardWidth - 20f, 56f), 34f);
                if (i < 2)
                {
                    Image arrow = Img(card, "Arrow", S("icon_chev_r"), Color.white, false);
                    Center(arrow.rectTransform, new Vector2(cardWidth * 0.5f + gap * 0.5f, 0f), new Vector2(54f, 54f));
                }
            }
        }
    }
}
