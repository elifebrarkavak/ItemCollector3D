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

            // ---- the plate: step, title, hint (compact, under the detector's hint, clear of the joystick and the action buttons)
            RectTransform plate = Rect("Plate", ui);
            Place(plate, new Vector2(0.5f, 1f), new Vector2(0f, -214f), new Vector2(900f, 176f));
            plate.pivot = new Vector2(0.5f, 1f);
            var plateGroup = plate.gameObject.AddComponent<CanvasGroup>();
            plateGroup.blocksRaycasts = false;
            plateGroup.interactable = false;
            Shadow(plate, new Vector2(900f, 176f), new Vector2(0f, -8f), 0.35f, 24f);
            Image bg = Img(plate, "Bg", S("ui_row_inset"), new Color(NavyDeep.r, NavyDeep.g, NavyDeep.b, 0.9f));
            Stretch(bg.rectTransform);
            TextMeshProUGUI step = BodyText(plate, "Step", "STEP 1 / 5", 28f, GoldLight, TextAlignmentOptions.Center, "UI_Body_Gold");
            Place(step.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(600f, 34f));
            TextMeshProUGUI title = Text(plate, "Title", "USE THE JOYSTICK TO MOVE", 54f, "UI_Title_Gold", Cream);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(840f, 64f));
            title.enableAutoSizing = true;
            title.fontSizeMin = 34f;
            title.fontSizeMax = 54f;
            TextMeshProUGUI hint = BodyText(plate, "Hint", "Push the stick on the left and walk around.", 30f, Cream, TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -134f), new Vector2(840f, 40f));

            // ---- a gold halo BEHIND the control the player should use (first child of the safe-area root, so every HUD control stays on top of it; it follows its target in code)
            Image highlight = Img(safeArea, HaloName, S("ui_pill"), new Color(1f, 0.82f, 0.3f, 0.6f));
            Center(highlight.rectTransform, Vector2.zero, new Vector2(240f, 240f));
            highlight.transform.SetSiblingIndex(0);
            highlight.gameObject.SetActive(false);

            // ---- SWIPE hint on the right half: two chevrons and the word
            RectTransform swipe = Rect("SwipeHint", ui);
            Place(swipe, new Vector2(1f, 0.5f), new Vector2(-340f, -30f), new Vector2(460f, 140f));
            Image swipeBg = Img(swipe, "Bg", S("ui_pill"), new Color(NavyDeep.r, NavyDeep.g, NavyDeep.b, 0.6f));
            Stretch(swipeBg.rectTransform);
            Image left = Img(swipe, "ChevL", S("icon_chev_l"), Cream, false);
            Center(left.rectTransform, new Vector2(-170f, 0f), new Vector2(72f, 72f));
            Image right = Img(swipe, "ChevR", S("icon_chev_r"), Cream, false);
            Center(right.rectTransform, new Vector2(170f, 0f), new Vector2(72f, 72f));
            TextMeshProUGUI swipeText = Text(swipe, "Label", "SWIPE", 60f, "UI_Title_Gold", Cream);
            Center(swipeText.rectTransform, new Vector2(0f, 4f), new Vector2(240f, 80f));
            swipe.gameObject.SetActive(false);

            // ---- the treasure arrow (step 3): the arrow turns towards the nearest hidden treasure, with its distance
            RectTransform guide = Rect("Guide", ui);
            Place(guide, new Vector2(0.5f, 1f), new Vector2(0f, -430f), new Vector2(200f, 150f));
            Image arrowImage = Img(guide, "Arrow", S("icon_play"), GoldLight, false);
            Center(arrowImage.rectTransform, new Vector2(0f, 24f), new Vector2(96f, 96f));
            TextMeshProUGUI distance = Text(guide, "Distance", "20 m", 44f, "UI_Title_Gold", Cream);
            Center(distance.rectTransform, new Vector2(0f, -48f), new Vector2(200f, 50f));
            guide.gameObject.SetActive(false);

            // ---- SKIP TUTORIAL: always reachable, bottom centre (between the joystick and the DIG / COLLECT buttons)
            Button skip = TextButton(ui, "SkipTutorialButton", "SKIP TUTORIAL", ButtonStyle.Navy, new Vector2(420f, 108f), 46f);
            RectTransform skipRect = skip.GetComponent<RectTransform>();
            Place(skipRect, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(420f, 108f));
            skipRect.pivot = new Vector2(0.5f, 0f);

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
            so.ApplyModifiedPropertiesWithoutUndo();

            ui.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            return $"tutorial layer built (joystick {(joystick != null)}, detector {(indicator != null)})";
        }
    }
}
