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

        // Printable ASCII plus a few typographic characters: baked into the asset so the UI never depends on runtime glyph generation.
        private const string BakedCharacters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\u2019\u00D7\u2713";

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

        public static void BuildAll()
        {
            System.IO.Directory.CreateDirectory(Folder);
            TMP_FontAsset font = Font;
            font.TryAddCharacters(BakedCharacters, out _);
            EditorUtility.SetDirty(font);
            Create("UI_Title_Gold", font, Outline, 0.42f, 0.0f, new Color(0f, 0f, 0f, 0.55f), 0.55f, -0.7f, 0.04f);
            Create("UI_Text_Brown", font, WoodDeepest, 0.30f, 0f, new Color(0f, 0f, 0f, 0.35f), 0.45f, -0.55f, 0.02f);
            Create("UI_Text_GoldDeep", font, Hex("#7A4A0A"), 0.30f, 0f, new Color(0f, 0f, 0f, 0.30f), 0.45f, -0.55f, 0.02f);
            Create("UI_Text_Green", font, GreenDark, 0.30f, 0f, new Color(0f, 0f, 0f, 0.30f), 0.45f, -0.55f, 0.02f);
            Create("UI_Text_Navy", font, Outline, 0.30f, 0f, new Color(0f, 0f, 0f, 0.35f), 0.45f, -0.55f, 0.02f);
            Create("UI_Text_Label", font, WoodDeepest, 0.26f, 0f, new Color(0f, 0f, 0f, 0.30f), 0.4f, -0.5f, 0.02f);
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
