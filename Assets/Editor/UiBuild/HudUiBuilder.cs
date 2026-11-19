using ItemCollector3D.Treasure;
using ItemCollector3D.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static ItemCollector3D.EditorTools.UiKit;
using static ItemCollector3D.EditorTools.UiPalette;

namespace ItemCollector3D.EditorTools
{
    /// <summary>
    /// Rebuilds the gameplay HUD in the same design family as the menus (stage 9): level name plate + PAUSE (top left), gold panel with the
    /// area progress bar (top right), translucent joystick (bottom left), one big DIG / COLLECT slot (bottom right) and the detector
    /// signal meter (top centre). The identity of the objects other things point at is kept (HUD + HudController, MovementJoystick +
    /// VirtualJoystick, DetectorIndicator), so no data connection changes; only the look and the layout are new.
    /// </summary>
    public static class HudUiBuilder
    {
        public const string PlayerUiPath = "Assets/Prefabs/UI/PlayerUI.prefab";
        public const string GameplayScenePath = "Assets/Scenes/GameplayTest.unity";

        // layout (1920x1080 reference units; every piece is anchored to a corner of the safe area)
        private const float Margin = 32f;
        private static readonly Vector2 ActionSize = new(400f, 184f);

        public static void BuildAll()
        {
            UiSpriteFactory.BuildHudSprites();
            BuildPlayerUi();
            MenuUiBuilder.BuildPause(); // the PAUSE button moves to the top-left corner
            BuildGameplayScene();
        }

        // ================================================================ PlayerUI prefab (HUD + joystick)

        public static void BuildPlayerUi()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerUiPath);
            try
            {
                Transform safeArea = root.transform.Find("SafeAreaRoot");
                Transform hud = safeArea.Find("HUD");
                Transform controls = safeArea.Find("MobileControls");
                var controller = hud.GetComponent<HudController>();
                ClearChildren(hud);
                ConfigureScaler(root.GetComponent<CanvasScaler>());
                ConfigureSafeArea(safeArea.GetComponent<SafeAreaFitter>());

                // ---- top left: level name plate, tucked under the PAUSE button (which lives in the PauseMenu prefab)
                RectTransform plate = Rect("LevelPlate", hud);
                Place(plate, new Vector2(0f, 1f), new Vector2(Margin + 96f, -46f), new Vector2(440f, 96f));
                Shadow(plate, plate.sizeDelta, new Vector2(0f, -8f), 0.45f, 26f);
                Image plateBg = Img(plate, "Bg", S("ui_plate"), Color.white);
                Stretch(plateBg.rectTransform);
                TextMeshProUGUI levelTitle = Text(plate, "LevelTitle", "GREEN MINE", 58f, "UI_Title_Gold", Gold, TextAlignmentOptions.Midline);
                Stretch(levelTitle.rectTransform, 62f, 6f, 26f, 0f); // centred in the part of the plate the round PAUSE button does not cover
                AutoSize(levelTitle, 34f, 58f);

                // ---- top right: gold panel (icon, total gold, area progress)
                RectTransform header = Rect("HeaderPanel", hud);
                Place(header, new Vector2(1f, 1f), new Vector2(-Margin, -34f), new Vector2(500f, 132f));
                Shadow(header, header.sizeDelta, new Vector2(0f, -8f), 0.5f, 28f);
                Image headerBg = Img(header, "Bg", S("ui_banner"), new Color(1f, 1f, 1f, 0.96f));
                Stretch(headerBg.rectTransform);

                Image gem = Img(header, "GoldIcon", S("icon_gem"), Color.white, false);
                Place(gem.rectTransform, new Vector2(0f, 0.5f), new Vector2(-24f, 4f), new Vector2(112f, 112f));

                TextMeshProUGUI goldText = Text(header, "GoldText", "0 / 500 GOLD", 58f, "UI_Title_Gold", Gold, TextAlignmentOptions.MidlineLeft);
                Place(goldText.rectTransform, new Vector2(0f, 1f), new Vector2(98f, -12f), new Vector2(380f, 68f));
                AutoSize(goldText, 34f, 58f);

                Image track = Img(header, "GoldProgressBackground", S("ui_slider_track"), Color.white);
                Place(track.rectTransform, new Vector2(0f, 0f), new Vector2(98f, 20f), new Vector2(380f, 38f));
                Image fill = Img(track.transform, "GoldProgressFill", S("ui_bar_fill"), Color.white, false);
                fill.preserveAspect = false;
                Stretch(fill.rectTransform, 11f, 9f, 11f, 9f);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.fillAmount = 0f;

                // ---- bottom right: ONE big action slot; DIG and COLLECT share it (HudController shows whichever applies)
                RectTransform actions = Rect("ActionButtons", hud);
                Place(actions, new Vector2(1f, 0f), new Vector2(-Margin - 16f, Margin + 20f), ActionSize);
                Button dig = TextButton(actions, "DigButton", "DIG", ButtonStyle.Gold, ActionSize, 104f, "icon_pickaxe", true);
                Center((RectTransform)dig.transform, Vector2.zero, ActionSize);
                ClearIconFromLabel(dig);
                dig.gameObject.SetActive(false);
                Button collect = TextButton(actions, "CollectButton", "COLLECT", ButtonStyle.Green, ActionSize, 72f, "icon_gem", true);
                Center((RectTransform)collect.transform, Vector2.zero, ActionSize);
                ClearIconFromLabel(collect);
                collect.gameObject.SetActive(false);

                SetRef(controller, "levelTitleText", levelTitle);
                SetRef(controller, "goldText", goldText);
                SetRef(controller, "goldFillImage", fill);
                SetRef(controller, "digButton", dig);
                SetRef(controller, "collectButton", collect);

                // ---- bottom left: translucent joystick (same object, so the scene keeps its Player reference)
                var joystickRect = (RectTransform)controls.Find("MovementJoystick");
                ClearChildren(joystickRect);
                joystickRect.anchorMin = joystickRect.anchorMax = Vector2.zero;
                joystickRect.pivot = new Vector2(0.5f, 0.5f);
                joystickRect.anchoredPosition = new Vector2(Margin + 160f, Margin + 160f);
                joystickRect.sizeDelta = new Vector2(280f, 280f);
                var joystickImage = joystickRect.GetComponent<Image>();
                joystickImage.sprite = S("ui_joy_base");
                joystickImage.type = Image.Type.Simple;
                joystickImage.preserveAspect = true;
                joystickImage.color = Color.white;
                joystickImage.raycastTarget = true;
                Image knob = Img(joystickRect, "Handle", S("ui_joy_knob"), Color.white, false);
                Center(knob.rectTransform, Vector2.zero, new Vector2(118f, 118f));

                var joystick = joystickRect.GetComponent<VirtualJoystick>();
                SetRef(joystick, "background", joystickRect);
                SetRef(joystick, "handle", knob.rectTransform);
                SetRef(joystick, "fadeTarget", joystickImage);

                // ---- the pause menu and the settings panel opened from it must not show the HUD around them
                EnsureHidesWhilePaused(hud.gameObject);
                EnsureHidesWhilePaused(controls.gameObject);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerUiPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ================================================================ gameplay scene (detector meter, reward text)

        public static void BuildGameplayScene()
        {
            Scene scene = SceneManager.GetActiveScene().path == GameplayScenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            var indicator = Object.FindAnyObjectByType<DetectorSignalIndicator>(FindObjectsInactive.Include);
            BuildDetectorMeter(indicator);

            // the floating "+10" reward: the UI font like everything else
            var reward = Object.FindAnyObjectByType<FloatingRewardText>(FindObjectsInactive.Include);
            if (reward != null)
            {
                var text = reward.GetComponent<TMP_Text>();
                text.font = UiFonts.Font;
                text.fontSharedMaterial = UiFonts.Load("UI_Title_Gold");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Top-centre signal meter: beacon dot, status label (NO SIGNAL / SLOW / FAST / RAPID) and three level bars.</summary>
        private static void BuildDetectorMeter(DetectorSignalIndicator indicator)
        {
            var root = (RectTransform)indicator.transform;
            var size = new Vector2(440f, 108f);
            Place(root, new Vector2(0.5f, 1f), new Vector2(0f, -34f), size);

            Transform dotOld = root.Find("SignalDot");
            Transform labelOld = root.Find("SignalLabel");
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child != dotOld && child != labelOld)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            Image shadow = Shadow(root, size, new Vector2(0f, -8f), 0.5f, 28f);
            shadow.transform.SetSiblingIndex(0);
            Image bg = Img(root, "Bg", S("ui_banner"), new Color(1f, 1f, 1f, 0.94f));
            Stretch(bg.rectTransform);
            bg.transform.SetSiblingIndex(1);

            var dot = dotOld.GetComponent<Image>();
            dot.sprite = S("ui_dot");
            dot.type = Image.Type.Simple;
            dot.preserveAspect = true;
            dot.raycastTarget = false;
            var dotRect = (RectTransform)dot.transform;
            dotRect.anchorMin = dotRect.anchorMax = new Vector2(0f, 0.5f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.anchoredPosition = new Vector2(58f, 2f);
            dotRect.sizeDelta = new Vector2(46f, 46f);
            dotRect.localScale = Vector3.one;

            var label = labelOld.GetComponent<TextMeshProUGUI>();
            label.font = UiFonts.Font;
            label.fontSharedMaterial = UiFonts.Load("UI_Title_Gold");
            label.fontSize = 52f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.margin = Vector4.zero;
            label.text = "NO SIGNAL";
            AutoSize(label, 30f, 52f);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(102f, 6f);
            labelRect.sizeDelta = new Vector2(224f, 72f);

            RectTransform bars = Rect("LevelBars", root);
            Place(bars, new Vector2(1f, 0.5f), new Vector2(-34f, -2f), new Vector2(96f, 60f));
            var barImages = new Image[3];
            float[] heights = { 24f, 40f, 58f };
            for (int i = 0; i < 3; i++)
            {
                barImages[i] = Img(bars, "Bar" + (i + 1), S("ui_pill"), Color.white);
                Place(barImages[i].rectTransform, new Vector2(0f, 0f), new Vector2(i * 36f, 0f), new Vector2(24f, heights[i]));
            }

            var so = new SerializedObject(indicator);
            SerializedProperty array = so.FindProperty("levelBars");
            array.arraySize = barImages.Length;
            for (int i = 0; i < barImages.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = barImages[i];
            }

            so.FindProperty("flashGraphic").objectReferenceValue = dot;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("unlitBarColor").colorValue = new Color(0.38f, 0.47f, 0.62f, 0.85f);
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureHidesWhilePaused(root.gameObject);
        }

        // ================================================================ helpers

        /// <summary>The big action buttons keep their label clear of the icon: the text is centred in the area to the right of it.</summary>
        private static void ClearIconFromLabel(Button button)
        {
            var label = (RectTransform)button.transform.Find("Label");
            label.offsetMin = new Vector2(120f, 14f);
            label.offsetMax = new Vector2(-14f, 0f);
        }

        private static void AutoSize(TMP_Text text, float min, float max)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
        }

        private static void EnsureHidesWhilePaused(GameObject target)
        {
            if (target.GetComponent<HideWhilePaused>() == null)
            {
                target.AddComponent<HideWhilePaused>();
            }
        }
    }
}
