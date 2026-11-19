using System.IO;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ItemCollector3D.EditorTools.UiKit;
using static ItemCollector3D.EditorTools.UiPalette;

namespace ItemCollector3D.EditorTools
{
    /// <summary>Rebuilds the menu prefabs (Settings, Pause, Main Menu + Character Selection) in place with the new design system.</summary>
    public static class MenuUiBuilder
    {
        public const string SettingsPath = "Assets/Prefabs/Menus/SettingsPanel.prefab";
        public const string PausePath = "Assets/Prefabs/Menus/PauseMenu.prefab";
        public const string MainMenuPath = "Assets/Prefabs/Menus/MainMenuUI.prefab";
        public const string CatalogPath = CharacterBuilder.CatalogPath;

        // ---- stage 10 layout (1920x1080 reference units; the canvas scaler EXPANDS, so the height is never less than 1080)
        /// <summary>A phone's 48 dp touch target in reference units (126 on a 1080 px high, 2.625x screen; 128 keeps a margin).</summary>
        public const float TouchUnits = 128f;
        private const float MenuColumnX = 110f;        // distance of the button column from the (gesture-safe) left edge
        private const float MenuColumnBottom = 96f;    // distance of EXIT from the (gesture-safe) bottom edge: anchored to the BOTTOM so EXIT can never be cut
        private const float MenuColumnWidth = 520f;
        private const float PlayHeight = 176f;
        private const float MenuButtonHeight = TouchUnits;
        private const float MenuGap = 22f;

        public static void BuildAll()
        {
            BuildSettings();
            BuildPause();
            BuildMainMenu();
            AssetDatabase.SaveAssets();
        }

        // ================================================================ Settings

        public static void BuildSettings()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SettingsPath);
            try
            {
                ClearChildren(root.transform);
                var rootRect = (RectTransform)root.transform;
                Stretch(rootRect);

                Dim(root.transform, 0.6f);
                RectTransform window = WoodWindow(root.transform, new Vector2(1300f, 740f));
                window.anchoredPosition = new Vector2(0f, -30f);
                Entrance(window.gameObject, UiEntrance.Style.Pop, 0f, 0.24f);

                TitleBanner(window, "SETTINGS", new Vector2(660f, 150f), 84f, new Vector2(0f, -4f));

                Button close = RoundButton(window, "CloseButton", "ui_round_navy", "icon_close", TouchUnits, 66f);
                Place(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-34f, -34f), new Vector2(TouchUnits, TouchUnits));
                close.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                close.GetComponent<RectTransform>().anchoredPosition = new Vector2(-30f, -22f);

                // three equal rows: 128 high with an equal 18 unit gap (pitch 146)
                Slider music = SettingRow(window, "MusicRow", "Music Volume", "icon_music", 215f, out TMP_Text musicValue);
                Slider sfx = SettingRow(window, "SfxRow", "Sound Effects", "icon_sfx", 69f, out TMP_Text sfxValue);
                Slider camera = SettingRow(window, "CameraRow", "Camera Sensitivity", "icon_camera", -77f, out TMP_Text cameraValue);

                Button back = TextButton(window, "BackButton", "BACK", ButtonStyle.Green, new Vector2(520f, 132f), 74f);
                Center(back.GetComponent<RectTransform>(), new Vector2(0f, -240f), new Vector2(520f, 132f));

                var controller = root.GetComponent<SettingsPanelController>();
                SetRef(controller, "musicSlider", music);
                SetRef(controller, "sfxSlider", sfx);
                SetRef(controller, "cameraSlider", camera);
                SetRef(controller, "musicValueLabel", musicValue);
                SetRef(controller, "sfxValueLabel", sfxValue);
                SetRef(controller, "cameraValueLabel", cameraValue);
                SetRef(controller, "backButton", back);
                SetRef(controller, "closeButton", close);
                PrefabUtility.SaveAsPrefabAsset(root, SettingsPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Slider SettingRow(Transform window, string name, string label, string icon, float y, out TMP_Text valueLabel)
        {
            RectTransform row = Rect(name, window);
            Center(row, new Vector2(0f, y), new Vector2(1120f, TouchUnits));
            Image bg = Img(row, "Bg", S("ui_row_inset"), new Color(1f, 1f, 1f, 0.95f));
            Stretch(bg.rectTransform);

            Image iconImage = Img(row, "Icon", S(icon), Color.white, false);
            Place(iconImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(80f, 80f));

            // small text: the upright body font (the slanted display font is for titles and big buttons)
            TextMeshProUGUI text = BodyText(row, "Label", label, 40f, Cream);
            Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(124f, 0f), new Vector2(392f, 96f));

            Slider slider = BuildSlider(row);
            Place(slider.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(172f, 0f), new Vector2(390f, TouchUnits));

            TextMeshProUGUI value = BodyText(row, "Value", "100%", 44f, Gold, TextAlignmentOptions.MidlineRight, "UI_Body_Gold");
            Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(150f, 96f));
            value.rectTransform.pivot = new Vector2(1f, 0.5f);
            valueLabel = value;
            return slider;
        }

        private static Slider BuildSlider(Transform parent)
        {
            RectTransform root = Rect("Slider", parent);
            root.sizeDelta = new Vector2(390f, TouchUnits);
            // invisible full-size hit area: a 128 unit high touch zone (48 dp) around a slimmer track
            Image hit = Img(root, "HitArea", null, new Color(0f, 0f, 0f, 0f), false, true);
            Stretch(hit.rectTransform);

            Image background = Img(root, "Background", S("ui_slider_track"), Color.white);
            Center(background.rectTransform, Vector2.zero, new Vector2(390f, 48f));
            background.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            background.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            background.rectTransform.offsetMin = new Vector2(0f, -24f);
            background.rectTransform.offsetMax = new Vector2(0f, 24f);

            RectTransform fillArea = Rect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(9f, -17f);
            fillArea.offsetMax = new Vector2(-9f, 17f);
            Image fill = Img(fillArea, "Fill", S("ui_slider_fill"), Color.white);
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            RectTransform handleArea = Rect("Handle Slide Area", root);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(40f, 0f);
            handleArea.offsetMax = new Vector2(-40f, 0f);
            Image handle = Img(handleArea, "Handle", S("ui_slider_knob"), Color.white, false);
            handle.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            handle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(84f, 84f);
            handle.rectTransform.anchoredPosition = Vector2.zero;

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            return slider;
        }

        // ================================================================ Pause

        public static void BuildPause()
        {
            var settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPath);
            GameObject root = PrefabUtility.LoadPrefabContents(PausePath);
            try
            {
                ClearChildren(root.transform);

                // the HUD button that opens the pause menu: a round wood button in the top-left corner, next to the level name plate
                Button pauseButton = RoundButton(root.transform, "PauseButton", "ui_round_wood", "icon_pause", 132f, 70f);
                Place(pauseButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(32f, -28f), new Vector2(132f, 132f));

                RectTransform panel = Rect("PausePanel", root.transform);
                Stretch(panel);
                // a controlled dim: a flat veil plus a soft vignette that darkens the edges more (no real-time blur, two cheap sprites)
                Dim(panel, 0.5f);
                Image vignette = Img(panel, "Vignette", S("ui_vignette"), new Color(1f, 1f, 1f, 0.7f), false);
                vignette.preserveAspect = false;
                Stretch(vignette.rectTransform, -300f, -300f, -300f, -300f);

                // compact window: 600 x 560 (was 720 x 640); the buttons keep their 48 dp touch size (128 / 136 high)
                RectTransform window = WoodWindow(panel, new Vector2(600f, 560f));
                window.anchoredPosition = new Vector2(0f, -20f); // the PAUSED banner overlaps the window's top edge; the HUD is hidden while paused (HideWhilePaused)
                Entrance(window.gameObject, UiEntrance.Style.Pop, 0f, 0.22f);
                TitleBanner(window, "PAUSED", new Vector2(440f, 116f), 74f, new Vector2(0f, -4f));

                var buttonSize = new Vector2(460f, TouchUnits);
                Button resume = TextButton(window, "ResumeButton", "RESUME", ButtonStyle.Gold, new Vector2(460f, 140f), 80f, "icon_play");
                Center(resume.GetComponent<RectTransform>(), new Vector2(0f, 128f), new Vector2(460f, 140f));
                Button settings = TextButton(window, "SettingsButton", "SETTINGS", ButtonStyle.Wood, buttonSize, 58f, "icon_gear");
                Center(settings.GetComponent<RectTransform>(), new Vector2(0f, -22f), buttonSize);
                Button mainMenu = TextButton(window, "MainMenuButton", "MAIN MENU", ButtonStyle.Navy, buttonSize, 58f, "icon_exit");
                Center(mainMenu.GetComponent<RectTransform>(), new Vector2(0f, -172f), buttonSize);

                var nested = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, root.transform);
                nested.name = "SettingsPanel";
                Stretch((RectTransform)nested.transform);
                nested.SetActive(false);
                panel.gameObject.SetActive(false);

                var controller = root.GetComponent<PauseMenuController>();
                SetRef(controller, "pauseButton", pauseButton);
                SetRef(controller, "pausePanel", panel.gameObject);
                SetRef(controller, "resumeButton", resume);
                SetRef(controller, "settingsButton", settings);
                SetRef(controller, "mainMenuButton", mainMenu);
                SetRef(controller, "settingsPanel", nested.GetComponent<SettingsPanelController>());
                PrefabUtility.SaveAsPrefabAsset(root, PausePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ================================================================ Main menu + character selection

        public static void BuildMainMenu()
        {
            var settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
            GameObject root = PrefabUtility.LoadPrefabContents(MainMenuPath);
            try
            {
                ClearChildren(root.transform);
                Stretch((RectTransform)root.transform);

                // ---- logo: ONE lockup (title + 3D badge + two gems), centred at the top
                RectTransform titleRoot = Rect("TitleRoot", root.transform);
                Place(titleRoot, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1300f, 210f));
                titleRoot.pivot = new Vector2(0.5f, 1f);
                Entrance(titleRoot.gameObject, UiEntrance.Style.SlideFromTop, 0.02f, 0.32f, 160f);

                // a soft dark gradient behind the logo: the bright tunnel glow never fights the gold letters
                Image shadeTop = Img(titleRoot, "Shade", S("ui_shade_top"), new Color(1f, 1f, 1f, 1f), false);
                shadeTop.preserveAspect = false;
                Place(shadeTop.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 600f), new Vector2(6000f, 1120f));
                shadeTop.rectTransform.pivot = new Vector2(0.5f, 1f);

                TextMeshProUGUI title = Text(titleRoot, "Title", "ITEM COLLECTOR", 128f, "UI_Title_Gold", GoldLight);
                title.verticalAlignment = VerticalAlignmentOptions.Middle;
                title.characterSpacing = 2f;
                title.ForceMeshUpdate();
                float textWidth = title.GetPreferredValues("ITEM COLLECTOR").x + 28f;
                const float badgeWidth = 188f;
                const float badgeTuck = -6f;                                   // the badge sits right after the last letter (a small gap, never over it)
                float lockupWidth = textWidth + badgeWidth - badgeTuck;
                float titleCentre = -lockupWidth * 0.5f + textWidth * 0.5f;
                Center(title.rectTransform, new Vector2(titleCentre, -6f), new Vector2(textWidth + 40f, 190f));

                RectTransform badge = Rect("TitleBadge", titleRoot);
                Center(badge, new Vector2(titleCentre + textWidth * 0.5f - badgeTuck + badgeWidth * 0.5f, 26f), new Vector2(badgeWidth, 124f));
                badge.localRotation = Quaternion.Euler(0f, 0f, -5f);
                Shadow(badge, new Vector2(badgeWidth, 124f), new Vector2(0f, -6f), 0.34f, 20f);
                Image badgeBg = Img(badge, "Bg", S("ui_badge"), Color.white);
                Stretch(badgeBg.rectTransform);
                TextMeshProUGUI badgeText = Text(badge, "Text", "3D", 96f, "UI_Text_Green", Cream);
                Stretch(badgeText.rectTransform, 0, 6, 0, 0);
                badgeText.verticalAlignment = VerticalAlignmentOptions.Middle;
                Image gemL = Img(titleRoot, "GemL", S("icon_gem"), Color.white, false);
                Center(gemL.rectTransform, new Vector2(-lockupWidth * 0.5f - 34f, 10f), new Vector2(72f, 72f));
                gemL.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
                Image gemR = Img(titleRoot, "GemR", S("icon_gem"), Color.white, false);
                Center(gemR.rectTransform, new Vector2(lockupWidth * 0.5f + 34f, 64f), new Vector2(52f, 52f));
                gemR.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);

                // ---- the vertical button column, anchored to the BOTTOM-left of the safe area: EXIT keeps a fixed margin above the
                // bottom edge on every screen (it used to hang from the top with absolute offsets and was cut off on wide phones)
                float columnHeight = PlayHeight + 3f * MenuButtonHeight + 3f * MenuGap;
                RectTransform buttons = Rect("Buttons", root.transform);
                Place(buttons, new Vector2(0f, 0f), new Vector2(MenuColumnX, MenuColumnBottom), new Vector2(MenuColumnWidth, columnHeight));
                buttons.pivot = new Vector2(0f, 0f);

                Image shadeLeft = Img(buttons, "Shade", S("ui_shade_left"), new Color(1f, 1f, 1f, 0.8f), false);
                shadeLeft.preserveAspect = false;
                Place(shadeLeft.rectTransform, new Vector2(0f, 0.5f), new Vector2(-MenuColumnX - 700f, 0f), new Vector2(1500f, 3000f));
                shadeLeft.rectTransform.pivot = new Vector2(0f, 0.5f);

                float y = 0f;
                Button play = MenuButton(buttons, "PlayButton", "PLAY", ButtonStyle.Gold, ref y, PlayHeight, 96f, "icon_play", 84f, 0.02f, true);
                Button character = MenuButton(buttons, "CharacterButton", "CHARACTER", ButtonStyle.Wood, ref y, MenuButtonHeight, 54f, "icon_helmet", 62f, 0.07f, false);
                Button settings = MenuButton(buttons, "SettingsButton", "SETTINGS", ButtonStyle.Wood, ref y, MenuButtonHeight, 54f, "icon_gear", 62f, 0.12f, false);
                Button exit = MenuButton(buttons, "ExitButton", "EXIT", ButtonStyle.Navy, ref y, MenuButtonHeight, 54f, "icon_exit", 62f, 0.17f, false);

                // ---- sub screens (hidden until opened): rendered after the buttons so they sit on top
                var settingsInstance = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, root.transform);
                settingsInstance.name = "SettingsPanel";
                Stretch((RectTransform)settingsInstance.transform);
                settingsInstance.SetActive(false);

                RectTransform characterPanel = BuildCharacterPanel(root.transform, catalog, out CharacterSelectController selectController);
                characterPanel.gameObject.SetActive(false);

                // Area Selection (PLAY opens it): built by AreaUiBuilder, so a full rebuild keeps it
                AreaSelectController areaController = AreaUiBuilder.BuildPanel(root.transform);

                var controller = root.GetComponent<MainMenuController>();
                SetRef(controller, "playButton", play);
                SetRef(controller, "characterButton", character);
                SetRef(controller, "settingsButton", settings);
                SetRef(controller, "exitButton", exit);
                SetRef(controller, "buttonsRoot", buttons.gameObject);
                SetRef(controller, "titleRoot", titleRoot.gameObject);
                SetRef(controller, "settingsPanel", settingsInstance.GetComponent<SettingsPanelController>());
                SetRef(controller, "characterPanel", selectController);
                SetRef(controller, "areaPanel", areaController);
                PrefabUtility.SaveAsPrefabAsset(root, MainMenuPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// One menu button of the column. The column is stacked from the top of its container with equal gaps; every icon is scaled so its
        /// VISIBLE shape (not its square image) is the same size in every button of a kind, and sits on the same vertical axis.
        /// </summary>
        private static Button MenuButton(Transform parent, string name, string label, ButtonStyle style, ref float yFromTop, float height, float fontSize,
            string icon, float iconVisual, float delay, bool pulse)
        {
            Button button = TextButton(parent, name, label, style, new Vector2(MenuColumnWidth, height), fontSize, icon, pulse);
            var rt = (RectTransform)button.transform;
            Place(rt, new Vector2(0f, 1f), new Vector2(0f, -yFromTop), new Vector2(MenuColumnWidth, height));
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(MenuColumnWidth * 0.5f, -yFromTop - height * 0.5f);
            yFromTop += height + MenuGap;
            Entrance(button.gameObject, UiEntrance.Style.SlideFromLeft, delay, 0.3f, 320f);

            // icon: same visible size and the same centre axis in every button; label: centred in the space to the right of the icon
            const float iconAxis = 76f;
            var iconRect = (RectTransform)button.transform.Find("Icon");
            Rect content = ContentBounds(icon);
            // equal visual WEIGHT: scale by the geometric mean of the visible width and height (a wide helmet then looks as big as a round gear), capped
            float scale = Mathf.Min(iconVisual / Mathf.Sqrt(content.width * content.height), iconVisual * 1.3f / Mathf.Max(content.width, content.height));
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(scale, scale);
            iconRect.anchoredPosition = new Vector2(iconAxis - (content.center.x - 0.5f) * scale, 4f - (content.center.y - 0.5f) * scale);
            var labelRect = (RectTransform)button.transform.Find("Label");
            labelRect.offsetMin = new Vector2(iconAxis + iconVisual * 0.5f + 18f, 12f);
            labelRect.offsetMax = new Vector2(-24f, 0f);
            return button;
        }

        /// <summary>The visible (non-transparent) bounds of an icon sprite as a 0..1 rect of its image, y up. Used to equalise icon sizes.</summary>
        private static Rect ContentBounds(string spriteName)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes($"{UiSpriteFactory.Folder}/{spriteName}.png"));
            Color32[] pixels = texture.GetPixels32();
            int w = texture.width, h = texture.height, minX = w, minY = h, maxX = -1, maxY = -1;
            for (int py = 0; py < h; py++)
            {
                for (int px = 0; px < w; px++)
                {
                    if (pixels[py * w + px].a > 150) // the soft drop shadow (alpha about 70) does not count
                    {
                        minX = Mathf.Min(minX, px);
                        maxX = Mathf.Max(maxX, px);
                        minY = Mathf.Min(minY, py);
                        maxY = Mathf.Max(maxY, py);
                    }
                }
            }

            Object.DestroyImmediate(texture);
            return maxX < 0 ? new Rect(0f, 0f, 1f, 1f) : UnityEngine.Rect.MinMaxRect(minX / (float)w, minY / (float)h, (maxX + 1) / (float)w, (maxY + 1) / (float)h);
        }

        private static RectTransform BuildCharacterPanel(Transform parent, CharacterCatalog catalog, out CharacterSelectController controller)
        {
            RectTransform panel = Rect("CharacterPanel", parent);
            Stretch(panel);
            controller = panel.gameObject.AddComponent<CharacterSelectController>();

            // soft vignette so the miner and the buttons stay readable over the cave
            Image vignette = Img(panel, "Vignette", S("ui_vignette"), new Color(1f, 1f, 1f, 0.9f), false);
            vignette.preserveAspect = false;
            Stretch(vignette.rectTransform, -300, -300, -300, -300);

            RectTransform banner = TitleBanner(panel, "CHOOSE YOUR MINER", new Vector2(760f, 112f), 66f, new Vector2(0f, -76f));
            Entrance(banner.gameObject, UiEntrance.Style.SlideFromTop, 0f, 0.3f, 140f);

            // BACK is a labelled navy button (leaves the screen); the round wood chevrons only browse the miners: different shape, colour and word
            Button back = TextButton(panel, "CharacterBackButton", "BACK", ButtonStyle.Navy, new Vector2(250f, TouchUnits), 58f, "icon_chev_l");
            Place(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(40f, -28f), new Vector2(250f, TouchUnits));

            Button left = RoundButton(panel, "LeftArrow", "ui_round_wood", "icon_chev_l", 144f, 80f);
            Center(left.GetComponent<RectTransform>(), new Vector2(-560f, 40f), new Vector2(144f, 144f));
            Entrance(left.gameObject, UiEntrance.Style.SlideFromLeft, 0.03f, 0.28f, 160f);
            Button right = RoundButton(panel, "RightArrow", "ui_round_wood", "icon_chev_r", 144f, 80f);
            Center(right.GetComponent<RectTransform>(), new Vector2(560f, 40f), new Vector2(144f, 144f));
            Entrance(right.gameObject, UiEntrance.Style.SlideFromRight, 0.03f, 0.28f, 160f);

            // ONE centred column under the miner: name + description plate, then the SELECT button: same width, same centre axis
            const float columnWidth = 600f;
            RectTransform plate = Rect("NamePlate", panel);
            Place(plate, new Vector2(0.5f, 0f), new Vector2(0f, 36f + TouchUnits + 14f), new Vector2(columnWidth, 128f));
            plate.pivot = new Vector2(0.5f, 0f);
            Entrance(plate.gameObject, UiEntrance.Style.SlideFromBottom, 0.03f, 0.3f, 160f);
            Shadow(plate, new Vector2(columnWidth, 128f), new Vector2(0f, 64f - 8f), 0.30f, 34f);
            Image plateBg = Img(plate, "Bg", S("ui_plate"), Color.white);
            Stretch(plateBg.rectTransform);
            TextMeshProUGUI nameLabel = Text(plate, "Name", "BARNABY", 70f, "UI_Title_Gold", Gold);
            Place(nameLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(columnWidth - 40f, 80f));
            nameLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            TextMeshProUGUI tagline = BodyText(plate, "Tagline", "The Bearded Digger", 32f, Cream, TextAlignmentOptions.Center);
            Place(tagline.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(columnWidth - 40f, 40f));
            tagline.rectTransform.pivot = new Vector2(0.5f, 0f);

            Button select = TextButton(panel, "SelectButton", "SELECT", ButtonStyle.Gold, new Vector2(columnWidth, TouchUnits), 76f, "icon_check");
            var selectRect = (RectTransform)select.transform;
            Place(selectRect, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(columnWidth, TouchUnits));
            selectRect.pivot = new Vector2(0.5f, 0f);
            Entrance(select.gameObject, UiEntrance.Style.SlideFromBottom, 0.08f, 0.3f, 160f);
            // the label is centred on the whole button; the check mark (shown only on the already selected miner) sits at the left
            var selectLabelRect = (RectTransform)select.transform.Find("Label");
            selectLabelRect.offsetMin = new Vector2(70f, 12f);
            selectLabelRect.offsetMax = new Vector2(-70f, 0f);
            var checkRect = (RectTransform)select.transform.Find("Icon");
            checkRect.anchorMin = checkRect.anchorMax = new Vector2(0f, 0.5f);
            checkRect.pivot = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(70f, 70f);
            checkRect.anchoredPosition = new Vector2(76f, 4f);

            var so = new SerializedObject(controller);
            so.FindProperty("catalog").objectReferenceValue = catalog;
            so.FindProperty("leftButton").objectReferenceValue = left;
            so.FindProperty("rightButton").objectReferenceValue = right;
            so.FindProperty("selectButton").objectReferenceValue = select;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("taglineLabel").objectReferenceValue = tagline;
            so.FindProperty("selectLabel").objectReferenceValue = select.transform.Find("Label").GetComponent<TMP_Text>();
            so.FindProperty("selectedMark").objectReferenceValue = checkRect.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            // "stage" is wired by the scene builder (it lives in the scene, not in the prefab)
            return panel;
        }
    }
}
