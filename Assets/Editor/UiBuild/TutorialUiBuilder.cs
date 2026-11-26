using ItemCollector3D.Progress;
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
    /// Stage 17B: adds the Green Mine tutorial to the OPEN GreenMine scene without touching the map, the treasures or the HUD's own parts: a small tutorial layer inside the HUD's safe-area root
    /// (a compact plate under the detector, a soft highlight, a swipe hint, a treasure arrow, SKIP TUTORIAL at the bottom centre) and one <see cref="TutorialController"/> object under GameplaySystems.
    /// Idempotent: <see cref="Apply"/> removes the old layer and builds it again. Run via <c>script eval</c>, then save the scene.
    /// </summary>
    public static class TutorialUiBuilder
    {
        public const string UiName = "Tutorial";
        public const string SystemName = "TutorialSystems";
        public const string HaloName = "TutorialHalo";

        // card layout (1920 x 1080 reference units)
        public const float CardWidth = 960f;
        public const float CardHeight = 160f;
        public const float CardTop = -226f; // below the detector's hint plate (which ends at -212)
        public const float SkipWidth = 400f;
        public const float SkipFaceHeight = 92f;
        public const float SkipTouchHeight = 126f;

        public static string Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject systems = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "GameplaySystems")
                {
                    systems = root;
                }
            }

            if (systems == null)
            {
                return "GameplaySystems not found in the active scene.";
            }

            Transform safeArea = systems.transform.Find("UI Canvas/SafeAreaRoot");
            if (safeArea == null)
            {
                return "SafeAreaRoot not found.";
            }

            Transform oldUi = safeArea.Find(UiName);
            if (oldUi != null)
            {
                Object.DestroyImmediate(oldUi.gameObject);
            }

            Transform oldHalo = safeArea.Find(HaloName);
            if (oldHalo != null)
            {
                Object.DestroyImmediate(oldHalo.gameObject);
            }

            Transform oldSystem = systems.transform.Find(SystemName);
            if (oldSystem != null)
            {
                Object.DestroyImmediate(oldSystem.gameObject);
            }

            RectTransform ui = Rect(UiName, safeArea);
            ui.gameObject.layer = safeArea.gameObject.layer;
            Stretch(ui);
            Transform progressNotification = safeArea.Find("ProgressNotification");
            if (progressNotification != null)
            {
                ui.SetSiblingIndex(progressNotification.GetSiblingIndex()); // below the progress banner and the pause menu
            }

            // ---- the card (stage 18C): the same navy + gold frame as the HUD panels, a gold step coin at the left, the title and the hint at the right with room around them, and a row of five dots
            // for the five steps. Compact (960 x 160) and tucked under the detector's hint plate, clear of the joystick, the action buttons, the gold panel and the pause plate.
            var cardSize = new Vector2(CardWidth, CardHeight);
            RectTransform plate = Rect("Plate", ui);
            Place(plate, new Vector2(0.5f, 1f), new Vector2(0f, CardTop), cardSize);
            plate.pivot = new Vector2(0.5f, 1f);
            var plateGroup = plate.gameObject.AddComponent<CanvasGroup>();
            plateGroup.blocksRaycasts = false;
            plateGroup.interactable = false;
            Shadow(plate, cardSize, new Vector2(0f, -10f), 0.42f, 26f);
            Image bg = Img(plate, "Bg", S("ui_banner"), new Color(1f, 1f, 1f, 0.97f));
            Stretch(bg.rectTransform);

            // the coin: the step number (a check mark when the tutorial is complete)
            Image coin = Img(plate, "Coin", S("ui_round_gold"), Color.white, false);
            Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(48f, 4f), new Vector2(92f, 92f));
            coin.rectTransform.pivot = new Vector2(0f, 0.5f);
            TextMeshProUGUI number = Text(coin.transform, "Number", "1", 62f, "UI_Text_GoldDeep", Cream);
            Stretch(number.rectTransform, 0f, 8f, 0f, 0f);
            number.verticalAlignment = VerticalAlignmentOptions.Middle;
            Image check = Img(coin.transform, "Check", S("icon_check"), Color.white, false);
            Center(check.rectTransform, new Vector2(0f, 4f), new Vector2(62f, 62f));
            check.gameObject.SetActive(false);

            // header row: STEP n / 5 at the left of the text column, the five dots at the right
            const float textLeft = 168f;
            const float textRight = 44f;
            float textWidth = CardWidth - textLeft - textRight;
            TextMeshProUGUI step = BodyText(plate, "Step", "STEP 1 / 5", 25f, GoldLight, TextAlignmentOptions.MidlineLeft, "UI_Body_Gold");
            Place(step.rectTransform, new Vector2(0f, 1f), new Vector2(textLeft, -20f), new Vector2(300f, 30f));
            var pips = new Image[TutorialController.StepCount];
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i] = Img(plate, "Pip" + (i + 1), S("ui_dot"), new Color(0.38f, 0.47f, 0.62f, 0.85f), false);
                Place(pips[i].rectTransform, new Vector2(1f, 1f), new Vector2(-textRight - (pips.Length - 1 - i) * 30f, -23f), new Vector2(22f, 22f));
            }

            TextMeshProUGUI title = Text(plate, "Title", "USE THE JOYSTICK TO MOVE", 50f, "UI_Title_Gold", Cream, TextAlignmentOptions.MidlineLeft);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(textLeft, -50f), new Vector2(textWidth, 58f));
            title.enableAutoSizing = true;
            title.fontSizeMin = 32f;
            title.fontSizeMax = 50f;
            TextMeshProUGUI hint = BodyText(plate, "Hint", "Push the stick on the left and walk around.", 29f, Cream, TextAlignmentOptions.MidlineLeft);
            Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(textLeft, -110f), new Vector2(textWidth, 36f));
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 26f; // the longest hint (step 3) fits in one line down to this size; never smaller (about 10.5 pt on a 3x phone)
            hint.fontSizeMax = 29f;

            // ---- a soft gold spotlight BEHIND the control the player should use (first child of the safe-area root, so every HUD control stays on top of it; it follows its target in code)
            Image highlight = Img(safeArea, HaloName, S("ui_halo"), new Color(1f, 0.82f, 0.28f, 0.6f), false);
            Center(highlight.rectTransform, Vector2.zero, new Vector2(240f, 240f));
            highlight.preserveAspect = false;
            highlight.transform.SetSiblingIndex(0);
            highlight.gameObject.SetActive(false);

            // ---- SWIPE hint on the right half: two chevrons and the word
            RectTransform swipe = Rect("SwipeHint", ui);
            Place(swipe, new Vector2(1f, 0.5f), new Vector2(-340f, -30f), new Vector2(440f, 128f));
            Shadow(swipe, new Vector2(440f, 128f), new Vector2(0f, -8f), 0.34f, 22f);
            Image swipeBg = Img(swipe, "Bg", S("ui_banner"), new Color(1f, 1f, 1f, 0.92f));
            Stretch(swipeBg.rectTransform);
            Image left = Img(swipe, "ChevL", S("icon_chev_l"), Cream, false);
            Center(left.rectTransform, new Vector2(-166f, 0f), new Vector2(64f, 64f));
            Image right = Img(swipe, "ChevR", S("icon_chev_r"), Cream, false);
            Center(right.rectTransform, new Vector2(166f, 0f), new Vector2(64f, 64f));
            TextMeshProUGUI swipeText = Text(swipe, "Label", "SWIPE", 60f, "UI_Title_Gold", Cream);
            Center(swipeText.rectTransform, new Vector2(0f, 4f), new Vector2(240f, 80f));
            swipe.gameObject.SetActive(false);

            // ---- the treasure arrow (step 3): the arrow turns towards the nearest hidden treasure, with its distance
            RectTransform guide = Rect("Guide", ui);
            Place(guide, new Vector2(0.5f, 1f), new Vector2(0f, -412f), new Vector2(200f, 150f));
            Image arrowImage = Img(guide, "Arrow", S("icon_play"), GoldLight, false);
            Center(arrowImage.rectTransform, new Vector2(0f, 24f), new Vector2(96f, 96f));
            TextMeshProUGUI distance = Text(guide, "Distance", "20 m", 44f, "UI_Title_Gold", Cream);
            Center(distance.rectTransform, new Vector2(0f, -48f), new Vector2(200f, 50f));
            guide.gameObject.SetActive(false);

            // ---- SKIP TUTORIAL: always reachable, bottom centre (between the joystick and the DIG / COLLECT buttons). The visible button is 400 x 92; the touch area is the full 126 units
            // (48 dp on a typical phone) because an invisible pad of that size sits behind it and hands its taps to the button.
            Button skip = TextButton(ui, "SkipTutorialButton", "SKIP TUTORIAL", ButtonStyle.Navy, new Vector2(SkipWidth, SkipTouchHeight), 42f);
            RectTransform skipRect = skip.GetComponent<RectTransform>();
            Place(skipRect, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(SkipWidth, SkipTouchHeight));
            skipRect.pivot = new Vector2(0.5f, 0f);
            float inset = (SkipTouchHeight - SkipFaceHeight) * 0.5f;
            foreach (string part in new[] { "Face", "Shadow", "Label" })
            {
                var partRect = (RectTransform)skipRect.Find(part);
                if (part == "Shadow")
                {
                    partRect.sizeDelta = new Vector2(SkipWidth, SkipFaceHeight) + Vector2.one * 64f;
                }
                else if (part == "Face")
                {
                    Stretch(partRect, 0f, inset, 0f, inset);
                }
                else
                {
                    Stretch(partRect, 0f, inset + 14f, 0f, inset);
                }
            }

            Image pad = Img(skipRect, "TouchPad", null, new Color(1f, 1f, 1f, 0f), false, true);
            Stretch(pad.rectTransform);
            pad.transform.SetAsFirstSibling();

            // ---- the controller (always active, so it can switch the layer on)
            var go = new GameObject(SystemName);
            go.transform.SetParent(systems.transform, false);
            var controller = go.AddComponent<TutorialController>();
            var joystick = Object.FindAnyObjectByType<VirtualJoystick>(FindObjectsInactive.Include);
            var indicator = Object.FindAnyObjectByType<DetectorSignalIndicator>(FindObjectsInactive.Include);
            var so = new SerializedObject(controller);
            so.FindProperty("progress").objectReferenceValue = Object.FindAnyObjectByType<ProgressManager>(FindObjectsInactive.Include);
            so.FindProperty("hunt").objectReferenceValue = Object.FindAnyObjectByType<TreasureHuntController>(FindObjectsInactive.Include);
            so.FindProperty("detector").objectReferenceValue = Object.FindAnyObjectByType<TreasureDetector>(FindObjectsInactive.Include);
            so.FindProperty("cameraController").objectReferenceValue = Object.FindAnyObjectByType<ItemCollector3D.Player.ThirdPersonCameraController>(FindObjectsInactive.Include);
            so.FindProperty("hud").objectReferenceValue = Object.FindAnyObjectByType<HudController>(FindObjectsInactive.Include);
            so.FindProperty("joystickRect").objectReferenceValue = joystick != null ? joystick.transform : null;
            so.FindProperty("detectorRect").objectReferenceValue = indicator != null ? indicator.transform : null;
            so.FindProperty("uiRoot").objectReferenceValue = ui.gameObject;
            so.FindProperty("plateGroup").objectReferenceValue = plateGroup;
            so.FindProperty("stepLabel").objectReferenceValue = step;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("hintLabel").objectReferenceValue = hint;
            so.FindProperty("skipButton").objectReferenceValue = skip;
            so.FindProperty("highlight").objectReferenceValue = highlight.rectTransform;
            so.FindProperty("highlightImage").objectReferenceValue = highlight;
            so.FindProperty("swipeHint").objectReferenceValue = swipe;
            so.FindProperty("arrow").objectReferenceValue = arrowImage.rectTransform;
            so.FindProperty("arrowLabel").objectReferenceValue = distance;
            so.FindProperty("badgeNumber").objectReferenceValue = number;
            so.FindProperty("badgeCheck").objectReferenceValue = check.gameObject;
            SerializedProperty pipArray = so.FindProperty("stepPips");
            pipArray.arraySize = pips.Length;
            for (int i = 0; i < pips.Length; i++)
            {
                pipArray.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
            }

            so.FindProperty("haloScale").floatValue = 1.7f;
            so.ApplyModifiedPropertiesWithoutUndo();

            ui.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            return $"tutorial layer built (joystick {(joystick != null)}, detector {(indicator != null)})";
        }
    }
}
