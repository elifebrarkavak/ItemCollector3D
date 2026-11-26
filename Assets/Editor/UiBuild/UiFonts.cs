using TMPro;
using UnityEditor;
using UnityEngine;
using static ItemCollector3D.EditorTools.UiPalette;

namespace ItemCollector3D.EditorTools
{
    /// <summary>
    /// Text styles of the UI design system. Uses the Bangers SDF font already in the project (bold, rounded-comic, readable) with
    /// outlined / shadowed material presets of the mobile SDF shader. A softer rounded font (e.g. Fredoka / Lilita One) is the
    /// recommended final asset; the presets here are font-agnostic.
    /// </summary>
    public static class UiFonts
    {
        public const string Folder = "Assets/UI/Fonts";
        private const string SourceFontPath = Folder + "/Bangers.ttf"; // SIL OFL licence next to it: "Bangers - OFL.txt"
        private const string FontPath = Folder + "/IC3D Bangers SDF.asset";

        // The complementary body font for SMALL labels (stage 10): Liberation Sans (SIL OFL, licence next to it) is upright, open and readable
        // at small sizes, where the slanted, heavily outlined Bangers is not. Bangers stays for titles and the big button labels.
        private const string BodySourceFontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf"; // licence: "LiberationSans - OFL.txt" next to it
        private const string BodyFontPath = Folder + "/IC3D Liberation SDF.asset";

        // Printable ASCII plus a few typographic characters: baked into the asset so the UI never depends on runtime glyph generation.
        private const string BakedCharacters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\u2019\u00D7\u2713\u00B7";

        /// <summary>
        /// The UI font: our OWN dynamic TMP font asset made from the Bangers .ttf (Assets/UI/Fonts) with all ASCII glyphs pre-baked.
        /// </summary>
        public static TMP_FontAsset Font
        {
            get
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                return font != null ? font : CreateFont();
            }
        }

        /// <summary>The body font asset (small labels, values, taglines): our own dynamic TMP font made from Liberation Sans, ASCII pre-baked.</summary>
        public static TMP_FontAsset BodyFont
        {
            get
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
                return font != null ? font : CreateBodyFont();
            }
        }

        private static TMP_FontAsset CreateBodyFont()
        {
            System.IO.Directory.CreateDirectory(Folder);
            var source = AssetDatabase.LoadAssetAtPath<Font>(BodySourceFontPath);
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, false);
            font.name = "IC3D Liberation SDF";
            DisableClearDynamicData(font);
            AssetDatabase.CreateAsset(font, BodyFontPath);
            font.material.name = "IC3D Liberation SDF Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            font.atlasTexture.name = "IC3D Liberation SDF Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            font.TryAddCharacters(BakedCharacters.Replace("\u2713", ""), out string missing); // Liberation Sans has no check mark glyph (the icon is a sprite)
            if (!string.IsNullOrEmpty(missing))
            {
                Debug.LogWarning("UiFonts: glyphs not in LiberationSans.ttf: " + missing);
            }

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        /// <summary>
        /// TMP dynamic font assets default to "Clear Dynamic Data On Build" = on. TMP then wipes the glyph table whenever it cleans dynamic data and rebuilds it
        /// from the few characters actually drawn, so the pre-baked ASCII set and the atlas change on their own (the Liberation font went from 98 baked characters
        /// to the 52 in use). The Bangers asset has the flag off and never changed; the body font now matches it.
        /// </summary>
        private static void DisableClearDynamicData(TMP_FontAsset font)
        {
            var so = new SerializedObject(font);
            SerializedProperty flag = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (flag != null)
            {
                flag.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// Repairs a body font whose glyph set was reduced by TMP: switches the clear-on-build flag off, clears the dynamic data and bakes the original set again
        /// in the original order (printable ASCII, the typographic apostrophe and multiplication sign, then the ellipsis), so glyphs and atlas are rebuilt
        /// exactly as committed. Returns a short summary.
        /// </summary>
        public static string RestoreBodyFontGlyphs()
        {
            TMP_FontAsset body = BodyFont;
            DisableClearDynamicData(body);
            body.ClearFontAssetData(false);
            body.TryAddCharacters(BakedCharacters.Replace("\u2713", ""), out string missing); // Liberation Sans has no check mark glyph (the icon is a sprite)
            body.TryAddCharacters("\u2026", out string missingEllipsis);
            EditorUtility.SetDirty(body);
            EditorUtility.SetDirty(body.atlasTexture);
            AssetDatabase.SaveAssets();
            return $"characters={body.characterTable.Count} glyphs={body.glyphTable.Count} missing='{missing}{missingEllipsis}' atlas={body.atlasWidth}x{body.atlasHeight}";
        }

        private static TMP_FontAsset CreateFont()
        {
            System.IO.Directory.CreateDirectory(Folder);
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 96, 10, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, false);
            font.name = "IC3D Bangers SDF";
            AssetDatabase.CreateAsset(font, FontPath);
            font.material.name = "IC3D Bangers SDF Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            font.atlasTexture.name = "IC3D Bangers SDF Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            font.TryAddCharacters(BakedCharacters, out string missing);
            if (!string.IsNullOrEmpty(missing))
            {
                Debug.LogWarning("UiFonts: glyphs not in Bangers.ttf: " + missing);
            }

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        public static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/{name}.mat");

        /// <summary>Creates only the text material of the red NEW GAME button (stage 18B) and bakes the middle dot of the Main Menu summary into the body font; everything else stays as it is.</summary>
        public static void EnsureStage18bAssets()
        {
            System.IO.Directory.CreateDirectory(Folder);
            Create("UI_Text_Red", Font, Hex("#5A1812"), 0.22f, 0f, new Color(0f, 0f, 0f, 0.26f), 0.3f, -0.4f, 0.08f);
            BodyFont.TryAddCharacters("\u00B7", out _);
            EditorUtility.SetDirty(BodyFont);
            AssetDatabase.SaveAssets();
        }

        public static void BuildAll()
        {
            System.IO.Directory.CreateDirectory(Folder);
            TMP_FontAsset font = Font;
            font.TryAddCharacters(BakedCharacters, out _);
            EditorUtility.SetDirty(font);
            // lighter outlines and softer shadows than stage 8: still crisp on any background, but no heavy black rim
            Create("UI_Title_Gold", font, Outline, 0.26f, 0.0f, new Color(0f, 0f, 0f, 0.36f), 0.35f, -0.45f, 0.10f);
            Create("UI_Text_Brown", font, WoodDeepest, 0.22f, 0f, new Color(0f, 0f, 0f, 0.24f), 0.3f, -0.4f, 0.08f);
            Create("UI_Text_GoldDeep", font, Hex("#7A4A0A"), 0.22f, 0f, new Color(0f, 0f, 0f, 0.22f), 0.3f, -0.4f, 0.08f);
            Create("UI_Text_Green", font, GreenDark, 0.22f, 0f, new Color(0f, 0f, 0f, 0.22f), 0.3f, -0.4f, 0.08f);
            Create("UI_Text_Navy", font, Outline, 0.22f, 0f, new Color(0f, 0f, 0f, 0.26f), 0.3f, -0.4f, 0.08f);
            Create("UI_Text_Label", font, WoodDeepest, 0.20f, 0f, new Color(0f, 0f, 0f, 0.22f), 0.3f, -0.4f, 0.08f);
            Create("UI_Text_Red", font, Hex("#5A1812"), 0.22f, 0f, new Color(0f, 0f, 0f, 0.26f), 0.3f, -0.4f, 0.08f);

            // body text: upright Liberation Sans, a little emboldened (face dilate) with a thin dark outline for contrast on wood
            TMP_FontAsset body = BodyFont;
            body.TryAddCharacters(BakedCharacters.Replace("\u2713", ""), out _);
            EditorUtility.SetDirty(body);
            Create("UI_Body_Cream", body, WoodDeepest, 0.14f, 0.16f, new Color(0f, 0f, 0f, 0.20f), 0.2f, -0.3f, 0.12f);
            Create("UI_Body_Gold", body, WoodDeepest, 0.14f, 0.16f, new Color(0f, 0f, 0f, 0.20f), 0.2f, -0.3f, 0.12f);
            AssetDatabase.SaveAssets();
        }

        private static void Create(string name, TMP_FontAsset font, Color outline, float outlineWidth, float dilate, Color underlay,
            float underlayX, float underlayY, float underlaySoftness)
        {
            string path = $"{Folder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("TextMeshPro/Mobile/Distance Field");
            if (material == null)
            {
                material = new Material(font.material) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.mainTexture = font.atlasTexture;
            material.EnableKeyword("OUTLINE_ON");
            material.EnableKeyword("UNDERLAY_ON");
            material.SetFloat("_GradientScale", font.atlasPadding + 1);
            material.SetFloat("_TextureWidth", font.atlasWidth);
            material.SetFloat("_TextureHeight", font.atlasHeight);
            material.SetColor("_OutlineColor", outline);
            material.SetFloat("_OutlineWidth", outlineWidth);
            material.SetFloat("_FaceDilate", dilate);
            material.SetColor("_UnderlayColor", underlay);
            material.SetFloat("_UnderlayOffsetX", underlayX);
            material.SetFloat("_UnderlayOffsetY", underlayY);
            material.SetFloat("_UnderlaySoftness", underlaySoftness);
            material.SetFloat("_UnderlayDilate", 0.1f);
            EditorUtility.SetDirty(material);
        }
    }
}
