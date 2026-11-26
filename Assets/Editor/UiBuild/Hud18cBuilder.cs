using ItemCollector3D.Menus;
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
    /// Stage 18C: gameplay HUD and tutorial polish, built from the existing UI kit (no new art). Two entry points, both idempotent:
    /// <list type="bullet">
    /// <item><see cref="BuildAssets"/>: the reward-text material and the shared ProgressNotification prefab (AREA COMPLETE / NEW AREA UNLOCKED banner): navy + gold frame, soft shadow, the UI font.</item>
    /// <item><see cref="ApplyScene"/>: for the OPEN gameplay scene: the floating gold reward text material, the detector's hint plate and beat lamp glow, the zone-name banner (Rocky Canyon / Ancient Mine). The Green Mine
    /// tutorial layer is built by <see cref="TutorialUiBuilder.Apply"/>.</item>
    /// </list>
    /// Only the look changes: no component, event, timing or save logic is added or removed. Run via <c>script eval</c>, then save the scene.
    /// </summary>
    public static class Hud18cBuilder
    {
        public const string BannerPrefabPath = "Assets/Prefabs/Menus/ProgressNotification.prefab";
        public const string HintPlateName = "HintPlate";
        public const string BeaconGlowName = "BeaconGlow";

        /// <summary>The banner panel of the progress banner (AREA COMPLETE / NEW AREA UNLOCKED): 1000 x 200 units.</summary>
        public static readonly Vector2 ProgressBannerSize = new(1000f, 200f);

        /// <summary>The zone-name banner (DESERT OUTPOST ...): one line, 760 x 100 units, top at -226 (below the detector's hint plate).</summary>
        public static readonly Vector2 ZoneBannerSize = new(760f, 100f);

        public const float ZoneBannerTop = -226f;

        /// <summary>Distance of the progress banner's centre from the top of the safe area, in reference units.</summary>
        public const float ProgressBannerCentre = 502f;

        // ================================================================ assets

        public static string BuildAssets()
        {
            UiFonts.EnsureStage18cAssets();
            return BuildBannerPrefab();
        }

        private static string BuildBannerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BannerPrefabPath);
            try
            {
                var banner = (RectTransform)root.transform.Find("Banner");
                banner.sizeDelta = ProgressBannerSize;
                banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 1f); // from the TOP, like the card and the zone banner: the same place on every screen shape and safe area
                banner.pivot = new Vector2(0.5f, 0.5f);
                banner.anchoredPosition = new Vector2(0f, -ProgressBannerCentre); // 402 .. 602 from the top: clear of the tutorial card (ends 386) and the zone-name banner (ends 326) above it
                StyleBanner(banner, false);
                PrefabUtility.SaveAsPrefabAsset(root, BannerPrefabPath);
                return "banner prefab restyled";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The one banner look (used by the prefab and by the scene copies that were instantiated from it, which are plain objects and do not follow the prefab): a soft shadow, the navy + gold panel of the HUD
        /// and the UI font for the title and the subtitle. <paramref name="oneLine"/>: the zone-name banner (no subtitle; the title keeps the rect the atmosphere tool gave it).
        /// </summary>
        public static void StyleBanner(RectTransform banner, bool oneLine)
        {
            Transform shadow = banner.Find("Shadow") != null ? banner.Find("Shadow") : banner.Find("Edge");
            if (shadow != null)
            {
                shadow.name = "Shadow";
                var image = shadow.GetComponent<Image>();
                image.sprite = S("ui_shadow");
                image.type = Image.Type.Sliced;
                image.color = new Color(0f, 0f, 0f, oneLine ? 0.42f : 0.5f);
                image.raycastTarget = false;
                var rt = (RectTransform)shadow;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                float grow = oneLine ? 36f : 52f;
                rt.offsetMin = new Vector2(-grow, -grow - 14f);
                rt.offsetMax = new Vector2(grow, grow - 14f);
            }

            Transform background = banner.Find("Background");
            var bg = background.GetComponent<Image>();
            bg.sprite = S("ui_banner");
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.97f);
            bg.raycastTarget = false;

            var title = banner.Find("Title").GetComponent<TextMeshProUGUI>();
            ApplyUiFont(title);
            title.fontStyle = FontStyles.Normal;
            title.alignment = TextAlignmentOptions.Center;
            title.verticalAlignment = VerticalAlignmentOptions.Middle;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Overflow;
            title.raycastTarget = false;
            var subtitle = banner.Find("Subtitle").GetComponent<TextMeshProUGUI>();
            ApplyUiFont(subtitle);
            subtitle.fontStyle = FontStyles.Normal;
            subtitle.alignment = TextAlignmentOptions.Center;
            subtitle.verticalAlignment = VerticalAlignmentOptions.Middle;
            subtitle.textWrappingMode = TextWrappingModes.NoWrap;
            subtitle.overflowMode = TextOverflowModes.Overflow;
            subtitle.raycastTarget = false;
            subtitle.color = Cream;

            if (oneLine)
            {
                title.fontSize = 54f;
                title.characterSpacing = 4f;
                var titleRect = title.rectTransform;
                titleRect.anchorMin = Vector2.zero;
                titleRect.anchorMax = Vector2.one;
                titleRect.pivot = new Vector2(0.5f, 0.5f);
                titleRect.offsetMin = new Vector2(36f, 10f);
                titleRect.offsetMax = new Vector2(-36f, -10f);
                title.enableAutoSizing = true; // a long area name shrinks to fit instead of leaving the panel
                title.fontSizeMin = 34f;
                title.fontSizeMax = 54f;
                subtitle.gameObject.SetActive(false);
                return;
            }

            // two lines inside the gold frame: the headline in the upper part, the area name in the lower part, both centred with 90 units of side room
            title.fontSize = 86f;
            title.characterSpacing = 2f;
            title.enableAutoSizing = true;
            title.fontSizeMin = 48f;
            title.fontSizeMax = 86f;
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(ProgressBannerSize.x - 180f, 94f));
            subtitle.fontSize = 56f;
            subtitle.characterSpacing = 3f;
            subtitle.enableAutoSizing = true; // a long area name shrinks to fit
            subtitle.fontSizeMin = 34f;
            subtitle.fontSizeMax = 56f;
            Place(subtitle.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(ProgressBannerSize.x - 180f, 62f));
        }

        private static void ApplyUiFont(TMP_Text text)
        {
            text.font = UiFonts.Font;
            text.fontSharedMaterial = UiFonts.Load("UI_Title_Gold");
        }

        // ================================================================ scene

        /// <summary>The OPEN scene: reward text, detector hint plate + lamp glow, zone-name banner. Returns a short summary.</summary>
        public static string ApplyScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            string summary = scene.name + ":";

            var reward = Object.FindAnyObjectByType<FloatingRewardText>(FindObjectsInactive.Include);
            if (reward != null)
            {
                var text = reward.GetComponent<TMP_Text>();
                text.font = UiFonts.Font;
                text.fontSharedMaterial = UiFonts.Load("UI_Reward_Gold");
                EditorUtility.SetDirty(text);
                summary += " reward";
            }

            var indicator = Object.FindAnyObjectByType<DetectorSignalIndicator>(FindObjectsInactive.Include);
            if (indicator != null)
            {
                BuildDetectorExtras(indicator);
                summary += " detector";
            }

            var notice = Object.FindAnyObjectByType<ZoneEntryNotice>(FindObjectsInactive.Include);
            if (notice != null && notice.Banner != null)
            {
                var rect = (RectTransform)notice.Banner.transform;
                rect.sizeDelta = new Vector2(Mathf.Max(ZoneBannerSize.x, rect.sizeDelta.x), ZoneBannerSize.y); // keeps the wider box of the Ancient Mine (FORGOTTEN WORKSHOP)
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, ZoneBannerTop);
                StyleBanner(rect, true);
                summary += " zone-banner";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return summary;
        }

        /// <summary>
        /// The detector keeps its place, its three level bars, its label and all of its numbers. Added: a navy plate behind the hint (GET CLOSER TO DIG, AREA LOCKED, TOO FAR ...), so the hint reads on a bright
        /// or a dark background alike and is larger than before; and a soft glow behind the beat lamp, which now rests as a dim amber bulb (not a grey dot) and lights up with every pulse.
        /// </summary>
        private static void BuildDetectorExtras(DetectorSignalIndicator indicator)
        {
            var root = (RectTransform)indicator.transform;
            Transform oldPlate = root.Find(HintPlateName);
            if (oldPlate != null)
            {
                Object.DestroyImmediate(oldPlate.gameObject);
            }

            Transform oldGlow = root.Find(BeaconGlowName);
            if (oldGlow != null)
            {
                Object.DestroyImmediate(oldGlow.gameObject);
            }

            Transform oldClone = root.Find("HintLabel"); // the runtime clone of an earlier run in the edit scene (never saved, but be safe)
            if (oldClone != null)
            {
                Object.DestroyImmediate(oldClone.gameObject);
            }

            Transform dot = root.Find("SignalDot");

            // the lamp's glow, directly behind the lamp
            Image glow = Img(root, BeaconGlowName, S("ui_glow"), new Color(1f, 0.85f, 0.1f, 0f), false);
            var dotRect = (RectTransform)dot;
            glow.rectTransform.anchorMin = dotRect.anchorMin;
            glow.rectTransform.anchorMax = dotRect.anchorMax;
            glow.rectTransform.pivot = dotRect.pivot;
            glow.rectTransform.anchoredPosition = dotRect.anchoredPosition;
            glow.rectTransform.sizeDelta = new Vector2(110f, 110f);
            glow.preserveAspect = false;
            glow.transform.SetSiblingIndex(dot.GetSiblingIndex());

            // the hint plate: hangs under the detector panel, wide enough for its text (layout group + size fitter), hidden until the indicator has a hint
            RectTransform plate = Rect(HintPlateName, root);
            plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0f);
            plate.pivot = new Vector2(0.5f, 1f);
            plate.anchoredPosition = new Vector2(0f, -8f);
            plate.sizeDelta = new Vector2(340f, 62f);
            var plateImage = plate.gameObject.AddComponent<Image>();
            plateImage.sprite = S("ui_banner");
            plateImage.type = Image.Type.Sliced;
            plateImage.color = new Color(1f, 1f, 1f, 0.95f);
            plateImage.raycastTarget = false;
            var layout = plate.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(46, 46, 4, 10);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false; // the plate keeps its 62 units: it never grows into the tutorial card or the zone banner below it
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = plate.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            var minimum = plate.gameObject.AddComponent<LayoutElement>();
            minimum.minWidth = 330f;
            minimum.minHeight = 62f;
            TextMeshProUGUI hint = Text(plate, "HintLabel", "GET CLOSER TO DIG", 42f, "UI_Title_Gold", GoldLight);
            hint.verticalAlignment = VerticalAlignmentOptions.Middle;
            hint.rectTransform.sizeDelta = new Vector2(0f, 52f);
            plate.gameObject.SetActive(false);

            var so = new SerializedObject(indicator);
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.FindProperty("hintPlate").objectReferenceValue = plate.gameObject;
            so.FindProperty("beaconGlow").objectReferenceValue = glow;
            so.FindProperty("idleColor").colorValue = new Color(0.5f, 0.36f, 0.14f, 1f); // an unlit amber bulb between the beats
            so.ApplyModifiedPropertiesWithoutUndo();
            var dotImage = dot.GetComponent<Image>();
            dotImage.color = new Color(0.5f, 0.36f, 0.14f, 1f);
        }
    }
}
