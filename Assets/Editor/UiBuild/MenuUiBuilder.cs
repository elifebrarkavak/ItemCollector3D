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

                Dim(root.transform, 0.66f);
                RectTransform window = WoodWindow(root.transform, new Vector2(1300f, 740f));
                window.anchoredPosition = new Vector2(0f, -30f); // clear of the gameplay HUD header when opened from the pause menu
                Entrance(window.gameObject, UiEntrance.Style.Pop, 0f, 0.3f);

                TitleBanner(window, "SETTINGS", new Vector2(660f, 150f), 84f, new Vector2(0f, -4f));

                Button close = RoundButton(window, "CloseButton", "ui_round_navy", "icon_close", 126f, 66f);
                Place(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-34f, -34f), new Vector2(126f, 126f));
                close.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                close.GetComponent<RectTransform>().anchoredPosition = new Vector2(-30f, -22f);

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
            Center(row, new Vector2(0f, y), new Vector2(1120f, 128f));
            Image bg = Img(row, "Bg", S("ui_row_inset"), new Color(1f, 1f, 1f, 0.95f));
            Stretch(bg.rectTransform);

            Image iconImage = Img(row, "Icon", S(icon), Color.white, false);
            Place(iconImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 2f), new Vector2(84f, 84f));

            TextMeshProUGUI text = Text(row, "Label", label, 44f, "UI_Text_Label", Cream, TextAlignmentOptions.MidlineLeft);
            Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(124f, 2f), new Vector2(380f, 100f));

            Slider slider = BuildSlider(row);
            Place(slider.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(172f, 0f), new Vector2(390f, 124f));

            TextMeshProUGUI value = Text(row, "Value", "100%", 50f, "UI_Text_Brown", Gold, TextAlignmentOptions.MidlineRight);
            Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(-28f, 2f), new Vector2(150f, 100f));
            value.rectTransform.pivot = new Vector2(1f, 0.5f);
            valueLabel = value;
            return slider;
        }

        private static Slider BuildSlider(Transform parent)
        {
            RectTransform root = Rect("Slider", parent);
            root.sizeDelta = new Vector2(390f, 124f);
            // invisible full-size hit area: a 124 px high touch zone (about 47 dp) around a slimmer track
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

                // the HUD button that opens the pause menu: a round wood button in the top-right corner
                Button pauseButton = RoundButton(root.transform, "PauseButton", "ui_round_wood", "icon_pause", 132f, 70f);
                Place(pauseButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(132f, 132f));

                RectTransform panel = Rect("PausePanel", root.transform);
                Stretch(panel);
                Dim(panel, 0.62f);
                RectTransform window = WoodWindow(panel, new Vector2(720f, 640f));
                window.anchoredPosition = new Vector2(0f, -50f); // sits below the HUD header (the PAUSED banner overlaps the window's top edge)
                Entrance(window.gameObject, UiEntrance.Style.Pop, 0f, 0.28f);
                TitleBanner(window, "PAUSED", new Vector2(520f, 140f), 82f, new Vector2(0f, -4f));

                Button resume = TextButton(window, "ResumeButton", "RESUME", ButtonStyle.Gold, new Vector2(540f, 156f), 86f, "icon_play");
                Center(resume.GetComponent<RectTransform>(), new Vector2(0f, 112f), new Vector2(540f, 156f));
                Button settings = TextButton(window, "SettingsButton", "SETTINGS", ButtonStyle.Wood, new Vector2(540f, 128f), 62f, "icon_gear");
                Center(settings.GetComponent<RectTransform>(), new Vector2(0f, -62f), new Vector2(540f, 128f));
                Button mainMenu = TextButton(window, "MainMenuButton", "MAIN MENU", ButtonStyle.Navy, new Vector2(540f, 128f), 62f, "icon_exit");
                Center(mainMenu.GetComponent<RectTransform>(), new Vector2(0f, -216f), new Vector2(540f, 128f));

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

                // ---- title
                RectTransform titleRoot = Rect("TitleRoot", root.transform);
                Place(titleRoot, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1500f, 220f));
                titleRoot.pivot = new Vector2(0.5f, 1f);
                Entrance(titleRoot.gameObject, UiEntrance.Style.SlideFromTop, 0.05f, 0.5f, 180f);

                TextMeshProUGUI title = Text(titleRoot, "Title", "ITEM COLLECTOR", 140f, "UI_Title_Gold", Gold);
                Center(title.rectTransform, new Vector2(-150f, -4f), new Vector2(1100f, 200f));
                title.verticalAlignment = VerticalAlignmentOptions.Middle;
                title.characterSpacing = 2f;

                RectTransform badge = Rect("TitleBadge", titleRoot);
                Center(badge, new Vector2(520f, -10f), new Vector2(210f, 138f));
                badge.localRotation = Quaternion.Euler(0f, 0f, -6f);
                Shadow(badge, new Vector2(210f, 138f), new Vector2(0f, -8f), 0.5f, 26f);
                Image badgeBg = Img(badge, "Bg", S("ui_badge"), Color.white);
                Stretch(badgeBg.rectTransform);
                TextMeshProUGUI badgeText = Text(badge, "Text", "3D", 104f, "UI_Text_Green", Cream);
                Stretch(badgeText.rectTransform, 0, 6, 0, 0);
                badgeText.verticalAlignment = VerticalAlignmentOptions.Middle;
                Image gemL = Img(titleRoot, "GemL", S("icon_gem"), Color.white, false);
                Center(gemL.rectTransform, new Vector2(-680f, 14f), new Vector2(92f, 92f));
                gemL.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
                Image gemR = Img(titleRoot, "GemR", S("icon_gem"), Color.white, false);
                Center(gemR.rectTransform, new Vector2(660f, 62f), new Vector2(62f, 62f));
                gemR.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);

                // ---- the vertical button column on the left
                RectTransform buttons = Rect("Buttons", root.transform);
                Place(buttons, new Vector2(0f, 1f), new Vector2(110f, -272f), new Vector2(520f, 720f)); // starts 28 units higher so EXIT keeps a margin on 21:9 screens
                buttons.pivot = new Vector2(0f, 1f);

                Button play = MenuButton(buttons, "PlayButton", "PLAY", ButtonStyle.Gold, 0f, 176f, 98f, "icon_play", 0.12f, true);
                Button character = MenuButton(buttons, "CharacterButton", "CHARACTER", ButtonStyle.Wood, 200f, 128f, 56f, "icon_helmet", 0.2f, false);
                Button settings = MenuButton(buttons, "SettingsButton", "SETTINGS", ButtonStyle.Wood, 348f, 128f, 56f, "icon_gear", 0.28f, false);
                Button exit = MenuButton(buttons, "ExitButton", "EXIT", ButtonStyle.Navy, 496f, 128f, 56f, "icon_exit", 0.36f, false);

                // ---- sub screens (hidden until opened): rendered after the buttons so they sit on top
                var settingsInstance = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, root.transform);
                settingsInstance.name = "SettingsPanel";
                Stretch((RectTransform)settingsInstance.transform);
                settingsInstance.SetActive(false);

                RectTransform characterPanel = BuildCharacterPanel(root.transform, catalog, out CharacterSelectController selectController);
                characterPanel.gameObject.SetActive(false);

                var controller = root.GetComponent<MainMenuController>();
                SetRef(controller, "playButton", play);
                SetRef(controller, "characterButton", character);
                SetRef(controller, "settingsButton", settings);
                SetRef(controller, "exitButton", exit);
                SetRef(controller, "buttonsRoot", buttons.gameObject);
                SetRef(controller, "titleRoot", titleRoot.gameObject);
                SetRef(controller, "settingsPanel", settingsInstance.GetComponent<SettingsPanelController>());
                SetRef(controller, "characterPanel", selectController);
                PrefabUtility.SaveAsPrefabAsset(root, MainMenuPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Button MenuButton(Transform parent, string name, string label, ButtonStyle style, float y, float height, float fontSize,
            string icon, float delay, bool pulse)
        {
            Button button = TextButton(parent, name, label, style, new Vector2(520f, height), fontSize, icon, pulse);
            var rt = (RectTransform)button.transform;
            Place(rt, new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(520f, height));
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(260f, -y - height * 0.5f);
            Entrance(button.gameObject, UiEntrance.Style.SlideFromLeft, delay, 0.4f, 420f);
            return button;
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

            RectTransform banner = TitleBanner(panel, "CHOOSE YOUR MINER", new Vector2(820f, 140f), 74f, new Vector2(0f, -100f));
            Entrance(banner.gameObject, UiEntrance.Style.SlideFromTop, 0f, 0.4f, 160f);

            Button back = RoundButton(panel, "CharacterBackButton", "ui_round_navy", "icon_chev_l", 132f, 70f);
            Place(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(132f, 132f));

            Button left = RoundButton(panel, "LeftArrow", "ui_round_wood", "icon_chev_l", 150f, 84f);
            Center(left.GetComponent<RectTransform>(), new Vector2(-560f, 30f), new Vector2(150f, 150f));
            Entrance(left.gameObject, UiEntrance.Style.SlideFromLeft, 0.05f, 0.35f, 200f);
            Button right = RoundButton(panel, "RightArrow", "ui_round_wood", "icon_chev_r", 150f, 84f);
            Center(right.GetComponent<RectTransform>(), new Vector2(560f, 30f), new Vector2(150f, 150f));
            Entrance(right.gameObject, UiEntrance.Style.SlideFromRight, 0.05f, 0.35f, 200f);

            // name plate (bottom left of centre) + SELECT (bottom right of centre): both stay below the miner's feet
            RectTransform plate = Rect("NamePlate", panel);
            Place(plate, new Vector2(0.5f, 0f), new Vector2(-330f, 56f), new Vector2(580f, 156f));
            plate.pivot = new Vector2(0.5f, 0f);
            Entrance(plate.gameObject, UiEntrance.Style.SlideFromBottom, 0.05f, 0.38f, 200f);
            Shadow(plate, new Vector2(580f, 156f), new Vector2(0f, 68f - 12f), 0.5f, 34f);
            Image plateBg = Img(plate, "Bg", S("ui_plate"), Color.white);
            Stretch(plateBg.rectTransform);
            TextMeshProUGUI nameLabel = Text(plate, "Name", "BARNABY", 74f, "UI_Title_Gold", Gold);
            Place(nameLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(560f, 90f));
            nameLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            TextMeshProUGUI tagline = Text(plate, "Tagline", "The Bearded Digger", 38f, "UI_Text_Brown", Cream);
            Place(tagline.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(560f, 50f));
            tagline.rectTransform.pivot = new Vector2(0.5f, 0f);

            Button select = TextButton(panel, "SelectButton", "SELECT", ButtonStyle.Gold, new Vector2(560f, 156f), 86f, "icon_check");
            var selectRect = (RectTransform)select.transform;
            Place(selectRect, new Vector2(0.5f, 0f), new Vector2(330f, 56f), new Vector2(560f, 156f));
            selectRect.pivot = new Vector2(0.5f, 0f);
            Entrance(select.gameObject, UiEntrance.Style.SlideFromBottom, 0.12f, 0.38f, 200f);

            // EQUIPPED tag on the plate corner
            RectTransform tag = Rect("EquippedBadge", plate);
            Place(tag, new Vector2(0f, 1f), new Vector2(20f, 28f), new Vector2(236f, 66f));
            tag.pivot = new Vector2(0f, 0.5f);
            Image tagBg = Img(tag, "Bg", S("ui_badge"), Color.white);
            Stretch(tagBg.rectTransform);
            Image tagIcon = Img(tag, "Check", S("icon_check"), Color.white, false);
            Place(tagIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(46f, 46f));
            tagIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            TextMeshProUGUI tagText = Text(tag, "Text", "EQUIPPED", 34f, "UI_Text_Green", Cream);
            Stretch(tagText.rectTransform, 40f, 4f, 0f, 0f);

            var so = new SerializedObject(controller);
            so.FindProperty("catalog").objectReferenceValue = catalog;
            so.FindProperty("leftButton").objectReferenceValue = left;
            so.FindProperty("rightButton").objectReferenceValue = right;
            so.FindProperty("selectButton").objectReferenceValue = select;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("taglineLabel").objectReferenceValue = tagline;
            so.FindProperty("selectLabel").objectReferenceValue = select.transform.Find("Label").GetComponent<TMP_Text>();
            so.FindProperty("equippedBadge").objectReferenceValue = tag.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            // "stage" is wired by the scene builder (it lives in the scene, not in the prefab)
            return panel;
        }
    }
}
