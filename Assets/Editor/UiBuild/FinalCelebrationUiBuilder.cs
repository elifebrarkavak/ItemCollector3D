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
    /// Stage 17D: builds the final celebration prefab (<see cref="PrefabPath"/>): GAME COMPLETE (grand: royal purple window with gold rim and turquoise glow, the Ancient Mine picture, a big gold trophy, a burst of stars) and
    /// 100% COLLECTION COMPLETE (plainer: a gold badge with 60 / 60 and one row per map). Everything comes from the existing design system (fonts, gold and navy chunky buttons, trophy and star sprites, the Ancient
    /// Mine area picture), no new asset. One instance is put into each OPEN gameplay scene (Green Mine, Rocky Canyon, Ancient Mine), last in the HUD's safe-area root so it covers the HUD and the pause menu.
    /// Idempotent: <see cref="Apply"/> replaces an older instance. Run via <c>script eval</c>, then save the scene.
    /// </summary>
    public static class FinalCelebrationUiBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/UI/FinalCelebration.prefab";
        public const string InstanceName = "FinalCelebration";
        public const string PictureAsset = "Assets/UI/Areas/area_ancient_mine.png";

        // the ancient / royal vault palette: deep violet, turquoise crystal light, gold
        public static readonly Color VioletDeep = Hex("#241546");
        public static readonly Color VioletMid = Hex("#3B2570");
        public static readonly Color VioletLight = Hex("#9B7BEA");
        public static readonly Color Turquoise = Hex("#2FD4C4");
        public static readonly Color TurquoiseLight = Hex("#9AF5EA");

        public static readonly Vector2 GameWindowSize = new(1500f, 860f);
        public static readonly Vector2 GameWindowCentre = new(0f, -44f);
        public static readonly Vector2 CollectionWindowSize = new(1360f, 840f);
        public static readonly Vector2 CollectionWindowCentre = new(0f, -40f);

        public static string BuildPrefab()
        {
            UiSpriteFactory.BuildCelebrationSprites();
            System.IO.Directory.CreateDirectory("Assets/Prefabs/UI");

            var root = new GameObject(InstanceName, typeof(RectTransform));
            try
            {
                var rootRect = (RectTransform)root.transform;
                Stretch(rootRect);
                var controller = root.AddComponent<FinalCelebrationController>();

                RectTransform panel = Rect("Panel", rootRect);
                Stretch(panel);
                Image dim = Dim(panel, 0.84f);
                var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();

                BuildGameComplete(panel, controller.Game);
                BuildCollection(panel, controller.Collection);

                var so = new SerializedObject(controller);
                so.FindProperty("panelRoot").objectReferenceValue = panel.gameObject;
                so.FindProperty("dimGroup").objectReferenceValue = dimGroup;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);

                int uiLayer = LayerMask.NameToLayer("UI");
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = uiLayer; // like the rest of the HUD: no per-object layer overrides in the scenes
                }

                Celebration18dPolish.ApplyFinal(root); // stage 18D: the final layout (the same one PolishPrefabs applies to the existing prefab)
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

        // ================================================================ GAME COMPLETE

        private static void BuildGameComplete(Transform panel, FinalCelebrationController.Skin skin)
        {
            RectTransform window = RoyalWindow(panel, GameWindowSize, "GameCompleteWindow");
            window.anchoredPosition = GameWindowCentre;
            skin.window = window;
            skin.group = window.gameObject.AddComponent<CanvasGroup>();

            // ---- the banner (half above the window) and the line under it
            RectTransform banner = TitleBanner(window, FinalCelebrationController.GameCompleteTitle, new Vector2(1120f, 176f), 112f, new Vector2(0f, 0f));
            skin.title = banner.Find("Heading").GetComponent<TMP_Text>();
            TextMeshProUGUI subtitle = Text(window, "Subtitle", FinalCelebrationController.GameCompleteSubtitle, 54f, "UI_Title_Gold", TurquoiseLight);
            Center(subtitle.rectTransform, new Vector2(0f, 292f), new Vector2(1300f, 76f));
            FitText(subtitle, 30f, 54f);
            skin.subtitle = subtitle;

            // ---- the Ancient Mine picture in a gold and turquoise frame
            Vector2 frameCentre = new(-430f, 78f);
            Vector2 frameSize = new(500f, 342f);
            Image glow = Img(window, "PictureGlow", S("ui_glow"), Alpha(Turquoise, 0.55f), false);
            Center(glow.rectTransform, frameCentre, frameSize * 1.5f);
            Image outline = Img(window, "PictureOutline", S("ui_pill"), UiPalette.Outline);
            Center(outline.rectTransform, frameCentre, frameSize + Vector2.one * 16f);
            Shadow(outline.rectTransform, frameSize, new Vector2(0f, -10f), 0.35f, 24f);
            Image frame = Img(window, "PictureFrame", S("ui_pill"), Gold);
            Center(frame.rectTransform, frameCentre, frameSize);
            Image inner = Img(window, "PictureInner", S("ui_pill"), Turquoise);
            Center(inner.rectTransform, frameCentre, frameSize - Vector2.one * 14f);
            Image picture = Img(window, "Picture", AssetDatabase.LoadAssetAtPath<Sprite>(PictureAsset), Color.white, false);
            Center(picture.rectTransform, frameCentre, frameSize - Vector2.one * 28f);
            picture.preserveAspect = false;
            skin.picture = picture;

            // ---- the right column: total gold, the goal, treasures, areas, a short message
            const float columnX = 295f;
            TextMeshProUGUI gold = Text(window, "TotalGold", "TOTAL GOLD: 2640", 76f, "UI_Title_Gold", GoldLight);
            Center(gold.rectTransform, new Vector2(columnX, 212f), new Vector2(820f, 98f));
            FitText(gold, 40f, 76f);
            skin.goldLabel = gold;
            Image divider = Img(window, "Divider", S("ui_pill"), Gold);
            Center(divider.rectTransform, new Vector2(columnX, 156f), new Vector2(740f, 9f));
            TextMeshProUGUI goal = Text(window, "Goal", "2500 GOLD GOAL REACHED", 48f, "UI_Title_Gold", TurquoiseLight);
            Center(goal.rectTransform, new Vector2(columnX, 105f), new Vector2(820f, 70f));
            FitText(goal, 28f, 48f);
            skin.goalLabel = goal;
            TextMeshProUGUI treasures = Text(window, "Treasures", "54 / 60 TREASURES COLLECTED", 48f, "UI_Title_Gold", Cream);
            Center(treasures.rectTransform, new Vector2(columnX, 45f), new Vector2(820f, 70f));
            FitText(treasures, 28f, 48f);
            skin.treasuresLabel = treasures;
            TextMeshProUGUI areas = Text(window, "Areas", "ALL 3 AREAS UNLOCKED", 48f, "UI_Title_Gold", Cream);
            Center(areas.rectTransform, new Vector2(columnX, -15f), new Vector2(820f, 70f));
            FitText(areas, 28f, 48f);
            skin.areasLabel = areas;
            TextMeshProUGUI message = BodyText(window, "Message", "SOME TREASURES ARE STILL HIDDEN - KEEP DIGGING!", 34f, new Color(Cream.r, Cream.g, Cream.b, 0.92f), TextAlignmentOptions.Center);
            Center(message.rectTransform, new Vector2(columnX, -70f), new Vector2(820f, 48f));
            FitText(message, 22f, 34f);
            skin.messageLabel = message;

            // ---- the buttons: CONTINUE EXPLORING (big, gold, pulsing), AREA SELECTION and MAIN MENU (navy)
            skin.continueButton = ContinueButton(window, new Vector2(0f, -178f));
            skin.areaSelectionButton = NavyButton(window, "AreaSelectionButton", FinalCelebrationController.AreaSelectionText, new Vector2(-265f, -332f));
            skin.mainMenuButton = NavyButton(window, "MainMenuButton", FinalCelebrationController.MainMenuText, new Vector2(265f, -332f));

            // ---- the big trophy on the picture's corner, a halo behind it, a burst of stars, a few twinkles around the banner
            Vector2 trophyCentre = new(-622f, 238f);
            Image halo = Img(window, "Halo", S("ui_glow"), new Color(GoldLight.r, GoldLight.g, GoldLight.b, 0f), false);
            Center(halo.rectTransform, trophyCentre, new Vector2(450f, 450f));
            Image trophy = Img(window, "Trophy", S("icon_trophy"), Color.white, false);
            Center(trophy.rectTransform, trophyCentre, new Vector2(236f, 236f));
            skin.halo = halo;
            skin.trophy = trophy.rectTransform;
            skin.burst = Burst(window, trophyCentre, 12, 56f);
            skin.twinkles = new[]
            {
                Twinkle(window, "TwinkleA", new Vector2(-640f, 478f), 62f),
                Twinkle(window, "TwinkleB", new Vector2(640f, 478f), 62f),
                Twinkle(window, "TwinkleC", new Vector2(-705f, 395f), 42f),
                Twinkle(window, "TwinkleD", new Vector2(705f, 395f), 42f),
                Twinkle(window, "TwinkleE", new Vector2(-300f, 520f), 36f),
                Twinkle(window, "TwinkleF", new Vector2(310f, 525f), 36f),
            };
        }

        // ================================================================ 100% COLLECTION

        private static void BuildCollection(Transform panel, FinalCelebrationController.Skin skin)
        {
            RectTransform window = RoyalWindow(panel, CollectionWindowSize, "CollectionWindow");
            window.anchoredPosition = CollectionWindowCentre;
            skin.window = window;
            skin.group = window.gameObject.AddComponent<CanvasGroup>();

            RectTransform banner = TitleBanner(window, FinalCelebrationController.CollectionTitle, new Vector2(1180f, 176f), 90f, new Vector2(0f, 0f));
            TMP_Text heading = banner.Find("Heading").GetComponent<TMP_Text>();
            heading.enableAutoSizing = true;
            heading.fontSizeMin = 48f;
            heading.fontSizeMax = 90f;
            skin.title = heading;
            TextMeshProUGUI subtitle = Text(window, "Subtitle", FinalCelebrationController.CollectionSubtitle, 52f, "UI_Title_Gold", TurquoiseLight);
            Center(subtitle.rectTransform, new Vector2(0f, 285f), new Vector2(1200f, 74f));
            FitText(subtitle, 30f, 52f);
            skin.subtitle = subtitle;

            // ---- the badge: a gold disc with 60 / 60 on it, a halo behind it
            Vector2 badgeCentre = new(-420f, 70f);
            Image halo = Img(window, "Halo", S("ui_glow"), new Color(GoldLight.r, GoldLight.g, GoldLight.b, 0f), false);
            Center(halo.rectTransform, badgeCentre, new Vector2(560f, 560f));
            RectTransform badgeGroup = Rect("BadgeGroup", window); // the whole badge pops in and breathes as one
            Center(badgeGroup, badgeCentre, new Vector2(384f, 384f));
            Image ring = Img(badgeGroup, "BadgeRing", S("ui_round_navy"), TurquoiseLight, false);
            Center(ring.rectTransform, Vector2.zero, new Vector2(384f, 384f));
            Image badge = Img(badgeGroup, "Badge", S("ui_round_gold"), Color.white, false);
            Center(badge.rectTransform, Vector2.zero, new Vector2(350f, 350f));
            TextMeshProUGUI count = Text(badgeGroup, "Count", "60 / 60", 112f, "UI_Text_GoldDeep", Cream);
            Center(count.rectTransform, new Vector2(0f, 24f), new Vector2(300f, 130f));
            FitText(count, 60f, 112f);
            TextMeshProUGUI caption = Text(badgeGroup, "CountCaption", "TREASURES", 46f, "UI_Text_GoldDeep", Cream);
            Center(caption.rectTransform, new Vector2(0f, -62f), new Vector2(300f, 60f));
            FitText(caption, 26f, 46f);
            skin.treasuresLabel = count;
            Image star = Img(badgeGroup, "BadgeStar", S("icon_star"), Color.white, false);
            Center(star.rectTransform, new Vector2(0f, 126f), new Vector2(64f, 64f));
            skin.trophy = badgeGroup;
            skin.halo = halo;
            skin.burst = Burst(window, badgeCentre, 10, 52f);

            // ---- the right column: one row per map, a divider, the total gold
            const float columnX = 290f;
            var rows = new TMP_Text[3];
            for (int i = 0; i < rows.Length; i++)
            {
                float y = 176f - i * 76f;
                Image check = Img(window, "Check" + i, S("icon_check"), Color.white, false);
                Center(check.rectTransform, new Vector2(columnX - 380f, y), new Vector2(58f, 58f));
                TextMeshProUGUI row = Text(window, "AreaRow" + i, "GREEN MINE   15 / 15", 50f, "UI_Title_Gold", i == 2 ? TurquoiseLight : Cream, TextAlignmentOptions.MidlineLeft);
                Center(row.rectTransform, new Vector2(columnX + 30f, y), new Vector2(720f, 70f));
                FitText(row, 28f, 50f);
                rows[i] = row;
            }

            skin.areaRows = rows;
            Image divider = Img(window, "Divider", S("ui_pill"), Gold);
            Center(divider.rectTransform, new Vector2(columnX, -30f), new Vector2(740f, 9f));
            TextMeshProUGUI gold = Text(window, "TotalGold", "TOTAL GOLD: 2705", 60f, "UI_Title_Gold", GoldLight);
            Center(gold.rectTransform, new Vector2(columnX, -78f), new Vector2(780f, 80f));
            FitText(gold, 34f, 60f);
            skin.goldLabel = gold;
            skin.messageLabel = null;

            // ---- the buttons
            skin.continueButton = ContinueButton(window, new Vector2(0f, -202f), 140f);
            skin.areaSelectionButton = NavyButton(window, "AreaSelectionButton", FinalCelebrationController.AreaSelectionText, new Vector2(-265f, -346f));
            skin.mainMenuButton = NavyButton(window, "MainMenuButton", FinalCelebrationController.MainMenuText, new Vector2(265f, -346f));

            skin.twinkles = new[]
            {
                Twinkle(window, "TwinkleA", new Vector2(-610f, 470f), 56f),
                Twinkle(window, "TwinkleB", new Vector2(610f, 470f), 56f),
                Twinkle(window, "TwinkleC", new Vector2(-655f, 385f), 38f),
                Twinkle(window, "TwinkleD", new Vector2(655f, 385f), 38f),
            };
        }

        // ================================================================ pieces

        /// <summary>The royal window: soft shadow, turquoise aura, gold rim, deep violet body with a lighter sheen at the top and a turquoise glow at the bottom. Returns the window rect (children go inside).</summary>
        private static RectTransform RoyalWindow(Transform parent, Vector2 size, string name)
        {
            RectTransform window = Rect(name, parent);
            Center(window, Vector2.zero, size);
            Shadow(window, size, new Vector2(0f, -14f), 0.45f, 50f);
            Image aura = Img(window, "Aura", S("ui_glow"), Alpha(Turquoise, 0.32f), false);
            Center(aura.rectTransform, Vector2.zero, size * 1.28f);
            Image rimDark = Img(window, "RimDark", S("ui_pill"), GoldDeep);
            Stretch(rimDark.rectTransform, -18f, -18f, -18f, -18f);
            Image rim = Img(window, "Rim", S("ui_pill"), Gold);
            Stretch(rim.rectTransform, -12f, -12f, -12f, -12f);
            Image rimLight = Img(window, "RimLight", S("ui_pill"), GoldLight);
            Stretch(rimLight.rectTransform, -6f, -6f, -6f, -6f);
            Image body = Img(window, "Body", S("ui_pill"), VioletDeep, true, true); // takes touches itself
            Stretch(body.rectTransform);
            Image sheen = Img(window, "Sheen", S("ui_glow"), Alpha(VioletLight, 0.34f), false);
            Place(sheen.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(size.x * 0.9f, size.y * 0.6f));
            Image floor = Img(window, "FloorGlow", S("ui_glow"), Alpha(Turquoise, 0.2f), false);
            Place(floor.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(size.x * 0.9f, size.y * 0.4f));
            return window;
        }

        private static Button ContinueButton(Transform parent, Vector2 position, float height = 150f)
        {
            Vector2 size = new(1000f, height);
            Button button = TextButton(parent, "ContinueButton", FinalCelebrationController.ContinueText, ButtonStyle.Gold, size, 64f, "icon_play", true);
            Center((RectTransform)button.transform, position, size);
            TMP_Text label = button.transform.Find("Label").GetComponent<TMP_Text>();
            FitText(label, 34f, 64f);
            return button;
        }

        private static Button NavyButton(Transform parent, string name, string text, Vector2 position)
        {
            Vector2 size = new(500f, 126f);
            Button button = TextButton(parent, name, text, ButtonStyle.Navy, size, 48f);
            Center((RectTransform)button.transform, position, size);
            TMP_Text label = button.transform.Find("Label").GetComponent<TMP_Text>();
            FitText(label, 30f, 48f);
            return button;
        }

        private static RectTransform[] Burst(Transform parent, Vector2 centre, int count, float size)
        {
            var burst = new RectTransform[count];
            for (int i = 0; i < burst.Length; i++)
            {
                Image star = Img(parent, "BurstStar" + i, S("icon_star"), Color.white, false);
                Center(star.rectTransform, centre, new Vector2(size, size));
                burst[i] = star.rectTransform;
            }

            return burst;
        }

        private static RectTransform Twinkle(Transform parent, string name, Vector2 position, float size)
        {
            Image star = Img(parent, name, S("icon_star"), new Color(1f, 0.95f, 0.7f, 1f), false);
            Center(star.rectTransform, position, new Vector2(size, size));
            return star.rectTransform;
        }

        private static void FitText(TMP_Text text, float min, float max)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
        }

        // ================================================================ placing it

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
            return "FinalCelebration placed in " + scene.path;
        }
    }
}
