using ItemCollector3D.Menus;
using ItemCollector3D.Progress;
using ItemCollector3D.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ItemCollector3D.EditorTools.UiKit;
using static ItemCollector3D.EditorTools.UiPalette;

namespace ItemCollector3D.EditorTools
{
    /// <summary>
    /// Builds the Area Selection screen (Main Menu -> PLAY) into the Main Menu prefab: SELECT AREA banner, BACK, and three big horizontal area cards.
    /// Everything is assembled from the existing design system (wood panel / banner / navy, gold and wood buttons, slider track + gold bar fill, Bangers
    /// and Liberation fonts); the only new art is the three area preview pictures (<see cref="AreaPreviewArt"/>) and the padlock icon.
    /// </summary>
    public static class AreaUiBuilder
    {
        public const string PanelName = "AreaPanel";
        public const string CatalogPath = "Assets/Data/Areas/AreaCatalog.asset";

        // ---- card layout (1920 x 1080 reference units, measured inside the safe area root)
        public const float CardWidth = 540f;
        public const float CardHeight = 770f;
        public const float CardGap = 40f;
        public const float BannerOverhang = 50f;     // the name banner sits on the card's top edge: half of it is above the card
        private const float RowTop = 176f;           // distance of the top of the name banners from the top of the safe area
        private const float PreviewInset = 32f;      // the picture sits 32 from each side of the card
        private const float PreviewTop = 62f;
        private const float PreviewHeight = 325f;    // 476 x 325: the 1024 x 700 pictures keep their aspect
        private const float ButtonHeight = 156f;     // 156 units = 58 dp on a typical phone; the card row never shrinks below TouchUnits / ButtonHeight, so a button stays at least 48 dp
        private const float ButtonTop = CardHeight - 30f - ButtonHeight;
        private const float LockBadgeSize = 128f;    // was 170 (-25 %), and moved to the top-right corner of the picture so it no longer hides the middle

        private sealed class CardSpec
        {
            public string Id;
            public string SceneName;     // empty: no map yet
            public string Display;
            public string Sprite;
        }

        private static readonly CardSpec[] Cards =
        {
            new CardSpec { Id = "green_mine", SceneName = SceneNavigator.GameplayScene, Display = "GREEN MINE", Sprite = "area_green_mine" },
            new CardSpec { Id = "rocky_canyon", SceneName = "", Display = "ROCKY CANYON", Sprite = "area_rocky_canyon" },
            new CardSpec { Id = "ancient_mine", SceneName = "", Display = "ANCIENT MINE", Sprite = "area_ancient_mine" },
        };

        /// <summary>Rebuilds only the Area Selection panel inside the Main Menu prefab (everything else in the prefab stays as it is).</summary>
        public static void Rebuild()
        {
            UiSpriteFactory.BuildAreaSprites();
            // the three preview pictures are NOT re-rendered here (AreaPreviewArt.BuildAll does that, on request): the approved art stays as it is
            GameObject root = PrefabUtility.LoadPrefabContents(MenuUiBuilder.MainMenuPath);
            try
            {
                Transform old = root.transform.Find(PanelName);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }

                AreaSelectController controller = BuildPanel(root.transform);
                SetRef(root.GetComponent<MainMenuController>(), "areaPanel", controller);
                PrefabUtility.SaveAsPrefabAsset(root, MenuUiBuilder.MainMenuPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>Adds the (hidden) Area Selection panel and the scene's ProgressManager to the Main Menu prefab root. Returns the controller.</summary>
        public static AreaSelectController BuildPanel(Transform menuRoot)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AreaCatalog>(CatalogPath);

            // the Main Menu scene had no progress object; the screen reads the saved gold through one that lives with the menu prefab
            var progress = menuRoot.GetComponent<ProgressManager>();
            if (progress == null)
            {
                progress = menuRoot.gameObject.AddComponent<ProgressManager>();
            }

            SetRef(progress, "catalog", catalog);

            RectTransform panel = Rect(PanelName, menuRoot);
            Stretch(panel);
            var controller = panel.gameObject.AddComponent<AreaSelectController>();

            // dim + vignette: the cave stays visible behind the cards, but the cards are what the eye finds
            Dim(panel, 0.52f);
            Image vignette = Img(panel, "Vignette", S("ui_vignette"), new Color(1f, 1f, 1f, 0.85f), false);
            vignette.preserveAspect = false;
            Stretch(vignette.rectTransform, -300f, -300f, -300f, -300f);

            RectTransform banner = TitleBanner(panel, "SELECT AREA", new Vector2(900f, 120f), 88f, new Vector2(0f, -84f));
            Entrance(banner.gameObject, UiEntrance.Style.SlideFromTop, 0f, 0.3f, 160f);

            Button back = TextButton(panel, "AreaBackButton", "BACK", ButtonStyle.Navy, new Vector2(250f, MenuUiBuilder.TouchUnits), 58f, "icon_chev_l");
            Place(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(40f, -28f), new Vector2(250f, MenuUiBuilder.TouchUnits));
            Entrance(back.gameObject, UiEntrance.Style.SlideFromLeft, 0.02f, 0.28f, 140f);

            // the row of cards hangs from the top centre; UiFitScale only shrinks it when a cut-out / gesture bar leaves too little room
            float rowWidth = Cards.Length * CardWidth + (Cards.Length - 1) * CardGap;
            var rowSize = new Vector2(rowWidth, BannerOverhang + CardHeight + 8f);
            RectTransform row = Rect("CardRow", panel);
            Place(row, new Vector2(0.5f, 1f), new Vector2(0f, -RowTop), rowSize);
            var fit = panel.gameObject.AddComponent<UiFitScale>();
            var fitSo = new SerializedObject(fit);
            fitSo.FindProperty("target").objectReferenceValue = row;
            fitSo.FindProperty("designSize").vector2Value = rowSize;
            fitSo.FindProperty("topInset").floatValue = RowTop;
            fitSo.FindProperty("bottomMargin").floatValue = 12f;
            fitSo.FindProperty("sideMargin").floatValue = 30f;
            fitSo.FindProperty("minScale").floatValue = MenuUiBuilder.TouchUnits / ButtonHeight; // never shrink the buttons below 48 dp
            fitSo.ApplyModifiedPropertiesWithoutUndo();

            var views = new AreaCardView[Cards.Length];
            for (int i = 0; i < Cards.Length; i++)
            {
                float x = (i - (Cards.Length - 1) * 0.5f) * (CardWidth + CardGap);
                RectTransform slot = Rect("Slot_" + Cards[i].Id, row);
                Place(slot, new Vector2(0.5f, 1f), new Vector2(x, -BannerOverhang), new Vector2(CardWidth, CardHeight));
                Entrance(slot.gameObject, UiEntrance.Style.SlideFromBottom, 0.05f + i * 0.07f, 0.36f, 150f);
                views[i] = BuildCard(slot, Cards[i]);
            }

            var so = new SerializedObject(controller);
            so.FindProperty("catalog").objectReferenceValue = catalog;
            so.FindProperty("progress").objectReferenceValue = progress;
            so.FindProperty("backButton").objectReferenceValue = back;
            SerializedProperty array = so.FindProperty("cards");
            array.arraySize = views.Length;
            for (int i = 0; i < views.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            return controller;
        }

        private static AreaCardView BuildCard(RectTransform slot, CardSpec spec)
        {
            Vector2 size = new Vector2(CardWidth, CardHeight);
            RectTransform card = Rect("Card_" + spec.Id, slot);
            Stretch(card);
            var view = card.gameObject.AddComponent<AreaCardView>();

            // gold glow behind the focused card (invisible otherwise)
            Image glow = Img(card, "FocusGlow", S("ui_glow"), new Color(1f, 0.82f, 0.36f, 0f), false);
            glow.preserveAspect = false;
            Stretch(glow.rectTransform, -190f, -170f, -190f, -170f);

            Shadow(card, size, new Vector2(0f, -16f), 0.46f, 46f);
            Image frame = Img(card, "Frame", S("ui_panel_wood"), Color.white, true, true); // the card body: taps on it focus the card
            Stretch(frame.rectTransform);
            Image grain = Img(card, "Grain", S("ui_wood_grain"), new Color(1f, 1f, 1f, 0.55f), false);
            grain.type = Image.Type.Tiled;
            Stretch(grain.rectTransform, 30f, 30f, 30f, 30f);

            // ---- preview picture (rounded corners and outline are part of the picture)
            RectTransform previewHolder = Rect("Preview", card);
            Place(previewHolder, new Vector2(0.5f, 1f), new Vector2(0f, -PreviewTop), new Vector2(CardWidth - 2f * PreviewInset, PreviewHeight));
            Shadow(previewHolder, previewHolder.sizeDelta, new Vector2(0f, -8f), 0.38f, 16f);
            Image preview = Img(previewHolder, "Picture", S_Area(spec.Sprite), Color.white, false);
            preview.preserveAspect = false;
            Stretch(preview.rectTransform);

            // ---- locked: a big padlock on a navy round badge in the middle of the (dimmed) picture
            RectTransform lockOverlay = Rect("LockOverlay", previewHolder);
            Stretch(lockOverlay);
            RectTransform lockBadge = Rect("LockBadge", lockOverlay);
            lockBadge.anchorMin = lockBadge.anchorMax = new Vector2(1f, 1f);
            lockBadge.pivot = new Vector2(0.5f, 0.5f); // the wiggle turns the badge around its own centre
            lockBadge.sizeDelta = new Vector2(LockBadgeSize, LockBadgeSize);
            lockBadge.anchoredPosition = new Vector2(-(LockBadgeSize * 0.5f + 16f), -(LockBadgeSize * 0.5f + 16f));
            Shadow(lockBadge, lockBadge.sizeDelta, new Vector2(0f, -6f), 0.5f, 20f);
            Image badgeFace = Img(lockBadge, "Face", S("ui_round_navy"), Color.white, false);
            Stretch(badgeFace.rectTransform);
            badgeFace.preserveAspect = true;
            Image lockIcon = Img(lockBadge, "Icon", S("icon_lock"), new Color(1f, 0.86f, 0.5f, 1f), false);
            Center(lockIcon.rectTransform, new Vector2(0f, 3f), new Vector2(LockBadgeSize * 0.61f, LockBadgeSize * 0.61f));

            // ---- open but no map yet: a COMING SOON banner across the picture, clearly not a lock
            RectTransform ribbon = Rect("ComingSoonRibbon", previewHolder);
            Place(ribbon, new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(400f, 92f));
            ribbon.pivot = new Vector2(0.5f, 0.5f);
            ribbon.localRotation = Quaternion.Euler(0f, 0f, -4f);
            Shadow(ribbon, ribbon.sizeDelta, new Vector2(0f, -8f), 0.45f, 22f);
            Image ribbonBg = Img(ribbon, "Bg", S("ui_banner"), Color.white);
            Stretch(ribbonBg.rectTransform);
            TextMeshProUGUI ribbonText = Text(ribbon, "Label", "COMING SOON", 58f, "UI_Title_Gold", Gold);
            Stretch(ribbonText.rectTransform, 0f, 6f, 0f, 0f);
            ribbonText.verticalAlignment = VerticalAlignmentOptions.Middle;

            // ---- small chips at the top-left corner of the picture: COMPLETE (green) / UNLOCKED (gold)
            RectTransform completeChip = Chip(previewHolder, "CompleteChip", "COMPLETE", "ui_badge", "UI_Text_Green", 214f);
            RectTransform unlockedChip = Chip(previewHolder, "UnlockedChip", "UNLOCKED", "ui_btn_gold", "UI_Text_Navy", 214f);

            // ---- name banner on the top edge of the card
            RectTransform nameBanner = TitleBanner(card, "AreaName", new Vector2(420f, 100f), 58f, new Vector2(0f, 0f), "NameBanner");
            Image nameBg = nameBanner.Find("Bg").GetComponent<Image>();
            TextMeshProUGUI nameLabel = nameBanner.Find("Heading").GetComponent<TextMeshProUGUI>();
            nameLabel.name = "AreaName";

            // ---- gold + progress
            float contentWidth = CardWidth - 2f * PreviewInset;
            RectTransform goldRow = Rect("GoldRow", card);
            Place(goldRow, new Vector2(0.5f, 1f), new Vector2(0f, -(PreviewTop + PreviewHeight + 14f)), new Vector2(contentWidth, 60f));
            Image gem = Img(goldRow, "GoldIcon", S("icon_gem"), Color.white, false);
            Place(gem.rectTransform, new Vector2(0f, 0.5f), new Vector2(4f, 2f), new Vector2(60f, 60f));
            TextMeshProUGUI goldLabel = Text(goldRow, "AreaGold", "GOAL: 500 GOLD", 50f, "UI_Title_Gold", Gold, TextAlignmentOptions.MidlineLeft);
            Place(goldLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(contentWidth - 86f, 60f));
            goldLabel.enableAutoSizing = true;
            goldLabel.fontSizeMin = 30f;
            goldLabel.fontSizeMax = 50f;

            Image track = Img(card, "ProgressTrack", S("ui_slider_track"), Color.white);
            Place(track.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -(PreviewTop + PreviewHeight + 86f)), new Vector2(contentWidth, 40f));
            Image fill = Img(track.transform, "ProgressFill", S("ui_bar_fill"), Color.white, false);
            fill.preserveAspect = false;
            Stretch(fill.rectTransform, 10f, 8f, 10f, 8f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            TextMeshProUGUI caption = BodyText(card, "AreaCaption", "0 / 500 COLLECTED", 34f, Cream, TextAlignmentOptions.Center);
            Place(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -(PreviewTop + PreviewHeight + 134f)), new Vector2(contentWidth, 44f));

            // ---- one action button, three looks: PLAY (gold) / COMING SOON (wood) / LOCKED (navy)
            var buttonSize = new Vector2(contentWidth, ButtonHeight);
            Button play = TextButton(card, "AreaPlay_" + spec.Id, "PLAY", ButtonStyle.Gold, buttonSize, 84f, "icon_play", true);
            Place(play.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -ButtonTop), buttonSize);
            Button soon = TextButton(card, "AreaSoon_" + spec.Id, "COMING SOON", ButtonStyle.Wood, buttonSize, 60f);
            Place(soon.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -ButtonTop), buttonSize);
            Button locked = TextButton(card, "AreaLocked_" + spec.Id, "LOCKED", ButtonStyle.Navy, buttonSize, 66f, "icon_lock");
            Place(locked.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -ButtonTop), buttonSize);
            soon.gameObject.SetActive(false);
            locked.gameObject.SetActive(false);
            lockOverlay.gameObject.SetActive(false);
            ribbon.gameObject.SetActive(false);
            completeChip.gameObject.SetActive(false);
            unlockedChip.gameObject.SetActive(false);

            // the picture frames and banner are what fades when the area is locked; the gold numbers always stay bright
            var so = new SerializedObject(view);
            so.FindProperty("areaId").stringValue = spec.Id;
            so.FindProperty("sceneName").stringValue = spec.SceneName;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("goldLabel").objectReferenceValue = goldLabel;
            so.FindProperty("captionLabel").objectReferenceValue = caption;
            so.FindProperty("progressFill").objectReferenceValue = fill;
            so.FindProperty("previewImage").objectReferenceValue = preview;
            SerializedProperty fade = so.FindProperty("fadeImages");
            Image[] fadeTargets = { frame, grain, nameBg };
            fade.arraySize = fadeTargets.Length;
            for (int i = 0; i < fadeTargets.Length; i++)
            {
                fade.GetArrayElementAtIndex(i).objectReferenceValue = fadeTargets[i];
            }

            so.FindProperty("lockOverlay").objectReferenceValue = lockOverlay.gameObject;
            so.FindProperty("lockBadge").objectReferenceValue = lockBadge;
            so.FindProperty("comingSoonRibbon").objectReferenceValue = ribbon.gameObject;
            so.FindProperty("completeChip").objectReferenceValue = completeChip.gameObject;
            so.FindProperty("unlockedChip").objectReferenceValue = unlockedChip.gameObject;
            so.FindProperty("playButton").objectReferenceValue = play;
            so.FindProperty("comingSoonButton").objectReferenceValue = soon;
            so.FindProperty("lockedButton").objectReferenceValue = locked;
            so.FindProperty("focusGlow").objectReferenceValue = glow;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        /// <summary>A small pill (check mark + word) in the top-left corner of the preview picture.</summary>
        private static RectTransform Chip(Transform parent, string name, string text, string sprite, string textMaterial, float width)
        {
            RectTransform chip = Rect(name, parent);
            Place(chip, new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(width, 62f));
            Shadow(chip, chip.sizeDelta, new Vector2(0f, -6f), 0.4f, 14f);
            Image bg = Img(chip, "Bg", S(sprite), Color.white);
            Stretch(bg.rectTransform);
            Image check = Img(chip, "Check", S("icon_check"), Color.white, false);
            Place(check.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 3f), new Vector2(46f, 46f));
            TextMeshProUGUI label = Text(chip, "Label", text, 36f, textMaterial, Cream);
            Stretch(label.rectTransform, 50f, 8f, 8f, 0f);
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            return chip;
        }

        private static Sprite S_Area(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{AreaPreviewArt.Folder}/{name}.png");
        }
    }
}
