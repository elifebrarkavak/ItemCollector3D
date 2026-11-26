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
    /// Stage 17C: builds the LEVEL COMPLETE panel prefab (<see cref="PrefabPath"/>) from the existing design system (wood window, gold banner, gold and navy chunky buttons, the three area pictures) and puts one
    /// instance into the OPEN gameplay scene (Green Mine or Rocky Canyon), last in the HUD's safe-area root so it covers the HUD and the pause menu. The prefab is self-contained: the controller finds the scene's
    /// progress, hunt controller and reward presenters itself. Idempotent: <see cref="Apply"/> replaces an older instance. Run via <c>script eval</c>, then save the scene.
    /// </summary>
    public static class LevelCompleteUiBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/UI/LevelComplete.prefab";
        public const string InstanceName = "LevelComplete";
        public const string AreasFolder = "Assets/UI/Areas";

        // the panel, in 1920 x 1080 reference units (measured inside the safe-area root; the whole thing, banner included, is 930 units high)
        public static readonly Vector2 WindowSize = new(1500f, 860f);
        public static readonly Vector2 WindowCentre = new(0f, -44f);

        public static string BuildPrefab()
        {
            UiSpriteFactory.BuildCelebrationSprites();
            System.IO.Directory.CreateDirectory("Assets/Prefabs/UI");

            var root = new GameObject(InstanceName, typeof(RectTransform));
            try
            {
                var rootRect = (RectTransform)root.transform;
                Stretch(rootRect);
                var controller = root.AddComponent<LevelCompleteController>();

                RectTransform panel = Rect("Panel", rootRect);
                Stretch(panel);

                Image dim = Dim(panel, 0.78f);
                var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();

                RectTransform window = WoodWindow(panel, WindowSize, "Window");
                window.anchoredPosition = WindowCentre;
                var windowGroup = window.gameObject.AddComponent<CanvasGroup>();

                // ---- the banner (half above the window)
                RectTransform banner = TitleBanner(window, LevelCompleteController.TitleText, new Vector2(1060f, 176f), 104f, new Vector2(0f, 0f));
                TMP_Text title = banner.Find("Heading").GetComponent<TMP_Text>();

                // ---- the picture of the area, framed in the area colour
                Vector2 frameCentre = new(-420f, 100f);
                Vector2 frameSize = new(498f, 349f);
                Image outline = Img(window, "PictureOutline", S("ui_pill"), UiPalette.Outline);
                Center(outline.rectTransform, frameCentre, frameSize + Vector2.one * 12f);
                Shadow(outline.rectTransform, frameSize, new Vector2(0f, -10f), 0.3f, 22f);
                Image frame = Img(window, "PictureFrame", S("ui_pill"), GreenLight);
                Center(frame.rectTransform, frameCentre, frameSize);
                Image picture = Img(window, "Picture", null, Color.white, false);
                Center(picture.rectTransform, frameCentre, frameSize - Vector2.one * 28f);
                picture.preserveAspect = false;

                // ---- the right column: area, divider, treasures, gold, total
                const float columnX = 290f;
                TextMeshProUGUI area = Text(window, "AreaName", "GREEN MINE COMPLETED", 62f, "UI_Title_Gold", GreenLight);
                Center(area.rectTransform, new Vector2(columnX, 225f), new Vector2(780f, 84f));
                FitText(area, 34f, 62f);
                Image divider = Img(window, "Divider", S("ui_pill"), GreenLight);
                Center(divider.rectTransform, new Vector2(columnX, 175f), new Vector2(700f, 10f));
                TextMeshProUGUI count = Text(window, "Treasures", "15 / 15 TREASURES COLLECTED", 54f, "UI_Title_Gold", Cream);
                Center(count.rectTransform, new Vector2(columnX, 115f), new Vector2(780f, 74f));
                FitText(count, 32f, 54f);
                TextMeshProUGUI gold = Text(window, "Gold", "625 GOLD AVAILABLE IN THIS AREA", 46f, "UI_Title_Gold", GoldLight);
                Center(gold.rectTransform, new Vector2(columnX, 50f), new Vector2(780f, 66f));
                FitText(gold, 30f, 46f);
                TextMeshProUGUI total = BodyText(window, "TotalGold", "YOUR TOTAL GOLD: 0", 34f, new Color(Cream.r, Cream.g, Cream.b, 0.9f), TextAlignmentOptions.Center);
                Center(total.rectTransform, new Vector2(columnX, -10f), new Vector2(780f, 48f));
                total.enableAutoSizing = true;
                total.fontSizeMin = 22f;
                total.fontSizeMax = 34f;

                // ---- the buttons: GO TO (big, gold, pulsing), STAY HERE and AREA SELECTION (navy)
                Button next = TextButton(window, "NextButton", "GO TO ROCKY CANYON", ButtonStyle.Gold, new Vector2(1000f, 150f), 64f, "icon_play", true);
                Center((RectTransform)next.transform, new Vector2(0f, -170f), new Vector2(1000f, 150f));
                TMP_Text nextLabel = next.transform.Find("Label").GetComponent<TMP_Text>();
                nextLabel.enableAutoSizing = true;
                nextLabel.fontSizeMin = 36f;
                nextLabel.fontSizeMax = 64f;
                Button stay = TextButton(window, "StayButton", LevelCompleteController.StayText, ButtonStyle.Navy, new Vector2(500f, 126f), 48f);
                Center((RectTransform)stay.transform, new Vector2(-265f, -330f), new Vector2(500f, 126f));
                Button selection = TextButton(window, "AreaSelectionButton", LevelCompleteController.AreaSelectionText, ButtonStyle.Navy, new Vector2(500f, 126f), 48f);
                Center((RectTransform)selection.transform, new Vector2(265f, -330f), new Vector2(500f, 126f));

                // ---- the trophy on the picture's corner, a soft halo behind it, a one-shot burst of stars, a few twinkles around the banner
                Vector2 trophyCentre = new(-640f, 255f);
                Image halo = Img(window, "Halo", S("ui_glow"), new Color(GoldLight.r, GoldLight.g, GoldLight.b, 0f), false);
                Center(halo.rectTransform, trophyCentre, new Vector2(380f, 380f));
                Image trophy = Img(window, "Trophy", S("icon_trophy"), Color.white, false);
                Center(trophy.rectTransform, trophyCentre, new Vector2(200f, 200f));
                var burst = new RectTransform[10];
                for (int i = 0; i < burst.Length; i++)
                {
                    Image star = Img(window, "BurstStar" + i, S("icon_star"), Color.white, false);
                    Center(star.rectTransform, trophyCentre, new Vector2(54f, 54f));
                    burst[i] = star.rectTransform;
                }

                var twinkles = new[]
                {
                    Twinkle(window, "TwinkleA", new Vector2(-610f, 480f), 58f),
                    Twinkle(window, "TwinkleB", new Vector2(610f, 480f), 58f),
                    Twinkle(window, "TwinkleC", new Vector2(-690f, 405f), 40f),
                    Twinkle(window, "TwinkleD", new Vector2(690f, 405f), 40f),
                };

                // ---- the controller
                Sprite green = AssetDatabase.LoadAssetAtPath<Sprite>($"{AreasFolder}/area_green_mine.png");
                Sprite rocky = AssetDatabase.LoadAssetAtPath<Sprite>($"{AreasFolder}/area_rocky_canyon.png");
                var so = new SerializedObject(controller);
                so.FindProperty("panelRoot").objectReferenceValue = panel.gameObject;
                so.FindProperty("dimGroup").objectReferenceValue = dimGroup;
                so.FindProperty("window").objectReferenceValue = window;
                so.FindProperty("windowGroup").objectReferenceValue = windowGroup;
                so.FindProperty("titleLabel").objectReferenceValue = title;
                so.FindProperty("areaLabel").objectReferenceValue = area;
                so.FindProperty("countLabel").objectReferenceValue = count;
                so.FindProperty("goldLabel").objectReferenceValue = gold;
                so.FindProperty("totalLabel").objectReferenceValue = total;
                so.FindProperty("nextLabel").objectReferenceValue = nextLabel;
                so.FindProperty("picture").objectReferenceValue = picture;
                so.FindProperty("frame").objectReferenceValue = frame;
                so.FindProperty("divider").objectReferenceValue = divider;
                so.FindProperty("nextButton").objectReferenceValue = next;
                so.FindProperty("stayButton").objectReferenceValue = stay;
                so.FindProperty("areaSelectionButton").objectReferenceValue = selection;
                so.FindProperty("trophy").objectReferenceValue = trophy.rectTransform;
                so.FindProperty("halo").objectReferenceValue = halo;
                SetRects(so.FindProperty("burst"), burst);
                SetRects(so.FindProperty("twinkles"), twinkles);
                SerializedProperty styles = so.FindProperty("styles");
                styles.arraySize = 2;
                SetStyle(styles.GetArrayElementAtIndex(0), "green_mine", green, Hex("#3FA37E"), Hex("#A5F0CB"));
                SetStyle(styles.GetArrayElementAtIndex(1), "rocky_canyon", rocky, Hex("#E0792D"), Hex("#FFC48A"));
                so.ApplyModifiedPropertiesWithoutUndo();

                int uiLayer = LayerMask.NameToLayer("UI");
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = uiLayer; // like the rest of the HUD: no per-object layer overrides in the scenes
                }

                Celebration18dPolish.ApplyLevelComplete(root); // stage 18D: the final layout (the same one PolishPrefabs applies to the existing prefab)
                panel.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            return "prefab saved: " + PrefabPath;
        }

        /// <summary>Puts (or replaces) the panel in the OPEN scene: last child of GameplaySystems/UI Canvas/SafeAreaRoot. Save the scene afterwards.</summary>
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

            Transform safeArea = systems != null ? systems.transform.Find("UI Canvas/SafeAreaRoot") : null;
            if (safeArea == null)
            {
                return "SafeAreaRoot not found in " + scene.path;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                return "prefab missing: build it first";
            }

            Transform old = safeArea.Find(InstanceName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, safeArea);
            instance.name = InstanceName;
            instance.transform.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(scene);
            return "LevelComplete placed in " + scene.path;
        }

        private static RectTransform Twinkle(Transform parent, string name, Vector2 position, float size)
        {
            Image star = Img(parent, name, S("icon_star"), new Color(1f, 0.95f, 0.7f, 1f), false);
            Center(star.rectTransform, position, new Vector2(size, size));
            return star.rectTransform;
        }

        private static void FitText(TextMeshProUGUI text, float min, float max)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
        }

        private static void SetRects(SerializedProperty array, RectTransform[] rects)
        {
            array.arraySize = rects.Length;
            for (int i = 0; i < rects.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = rects[i];
            }
        }

        private static void SetStyle(SerializedProperty element, string areaId, Sprite picture, Color accent, Color accentLight)
        {
            element.FindPropertyRelative("areaId").stringValue = areaId;
            element.FindPropertyRelative("picture").objectReferenceValue = picture;
            element.FindPropertyRelative("accent").colorValue = accent;
            element.FindPropertyRelative("accentLight").colorValue = accentLight;
        }
    }
}
