#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ItemCollector3D.EditorTools.AncientMine
{
    /// <summary>
    /// Swatch indices of the ONE palette texture every Ancient Mine surface uses (one lit material + one emissive material share it; a face samples the centre of its swatch).
    /// Swatches 0..39 are five rock families (Gate, Hall, Crystal, Workshop, Vault) of eight tones each, dark to light; the named ones follow.
    /// </summary>
    public static class P
    {
        public const int RockBase = 0;
        public const int Named = 40;

        public const int Marble = Named + 0, MarbleShade = Named + 1, StoneLight = Named + 2, StoneMid = Named + 3, StoneDark = Named + 4, StoneMoss = Named + 5,
            RoadStone = Named + 6, RoadStoneDark = Named + 7, RoadInlay = Named + 8, Gold = Named + 9, GoldDark = Named + 10, Bronze = Named + 11,
            Wood = Named + 12, WoodDark = Named + 13, Iron = Named + 14, IronDark = Named + 15, Rust = Named + 16, Rope = Named + 17,
            ClothRed = Named + 18, ClothTeal = Named + 19, Bone = Named + 20, Black = Named + 21, Rubble = Named + 22, RubbleDark = Named + 23,
            CrystalCyan = Named + 24, CrystalCyanLight = Named + 25, CrystalBlue = Named + 26, CrystalBlueLight = Named + 27,
            CrystalViolet = Named + 28, CrystalVioletLight = Named + 29, CrystalTurquoise = Named + 30, CrystalTurquoiseLight = Named + 31,
            CrystalMagenta = Named + 32, CrystalMagentaLight = Named + 33, PortalGlow = Named + 34, PortalGlowBright = Named + 35, LampWarm = Named + 36,
            StoneTeal = Named + 37, StoneViolet = Named + 38, Sand = Named + 39;

        // stage 16B additions: gold in three tones, statue stone, warm workshop stone, crystal dark tones, water and lamp colours, cloth
        public const int GoldBright = Named + 40, GoldPale = Named + 41, GoldShade = Named + 42, BronzeDark = Named + 43,
            StatueLight = Named + 44, StatueMid = Named + 45, StatueDark = Named + 46, StatueTeal = Named + 47, Basalt = Named + 48, MarbleWarm = Named + 49,
            WoodLight = Named + 50, WoodAged = Named + 51, TealStoneLight = Named + 52, VioletStoneLight = Named + 53, Indigo = Named + 54,
            CrystalCyanDark = Named + 55, CrystalBlueDark = Named + 56, CrystalVioletDark = Named + 57, CrystalTurquoiseDark = Named + 58, CrystalMagentaDark = Named + 59,
            CrystalWhite = Named + 60, WaterTurquoise = Named + 61, WaterDeep = Named + 62, LampWarmDim = Named + 63, Ember = Named + 64, GemGold = Named + 65,
            RubbleWarm = Named + 66, RubbleWarmDark = Named + 67, ClothGold = Named + 68, ClothIndigo = Named + 69, ClothPurple = Named + 70,
            WarmStoneDark = Named + 71, WarmStoneMid = Named + 72, WarmStoneLight = Named + 73, Soot = Named + 74;

        public const int Count = Named + 75;
        public const int Columns = 16;
        public const int Cell = 8;

        /// <summary>Fixed at 8 rows (128 swatches), so a face's UV never changes when swatches are added.</summary>
        public static int Rows => 8;

        public static int Rock(AmZone zone, int tone) => RockBase + (zone == AmZone.None ? 1 : (int)zone - 1) * 8 + Mathf.Clamp(tone, 0, 7);

        public static Vector2 Uv(int swatch)
        {
            return new Vector2(((swatch % Columns) * Cell + Cell * 0.5f) / (Columns * Cell), ((swatch / Columns) * Cell + Cell * 0.5f) / (Rows * Cell));
        }

        private static readonly Color[,] RockRamp =
        {
            // dark, light
            { new Color(0.15f, 0.19f, 0.32f), new Color(0.70f, 0.80f, 0.92f) },   // Gate: bright slate blue
            { new Color(0.19f, 0.18f, 0.24f), new Color(0.82f, 0.80f, 0.86f) },   // Hall: neutral grand grey stone
            { new Color(0.15f, 0.09f, 0.28f), new Color(0.64f, 0.48f, 0.76f) },   // Crystal: purple rock
            { new Color(0.24f, 0.16f, 0.14f), new Color(0.86f, 0.70f, 0.54f) },   // Workshop: warm old stone
            { new Color(0.18f, 0.10f, 0.34f), new Color(0.80f, 0.60f, 0.96f) },   // Vault: royal purple
        };

        public static Color Colour(int swatch)
        {
            if (swatch < Named)
            {
                int family = swatch / 8, tone = swatch % 8;
                return Color.Lerp(RockRamp[family, 0], RockRamp[family, 1], Mathf.Pow(tone / 7f, 0.85f));
            }

            switch (swatch)
            {
                case Marble: return new Color(0.90f, 0.90f, 0.96f);
                case MarbleShade: return new Color(0.68f, 0.70f, 0.82f);
                case StoneLight: return new Color(0.66f, 0.70f, 0.80f);
                case StoneMid: return new Color(0.48f, 0.50f, 0.64f);
                case StoneDark: return new Color(0.30f, 0.31f, 0.46f);
                case StoneMoss: return new Color(0.36f, 0.56f, 0.56f);
                case RoadStone: return new Color(0.58f, 0.60f, 0.72f);
                case RoadStoneDark: return new Color(0.44f, 0.46f, 0.60f);
                case RoadInlay: return new Color(0.50f, 0.78f, 0.84f);
                case Gold: return new Color(0.96f, 0.76f, 0.26f);
                case GoldDark: return new Color(0.66f, 0.48f, 0.18f);
                case Bronze: return new Color(0.70f, 0.48f, 0.30f);
                case Wood: return new Color(0.52f, 0.36f, 0.26f);
                case WoodDark: return new Color(0.34f, 0.24f, 0.20f);
                case Iron: return new Color(0.40f, 0.42f, 0.52f);
                case IronDark: return new Color(0.24f, 0.25f, 0.34f);
                case Rust: return new Color(0.62f, 0.34f, 0.26f);
                case Rope: return new Color(0.74f, 0.64f, 0.46f);
                case ClothRed: return new Color(0.70f, 0.22f, 0.30f);
                case ClothTeal: return new Color(0.20f, 0.52f, 0.56f);
                case Bone: return new Color(0.92f, 0.90f, 0.82f);
                case Black: return new Color(0.08f, 0.08f, 0.12f);
                case Rubble: return new Color(0.50f, 0.46f, 0.52f);
                case RubbleDark: return new Color(0.32f, 0.29f, 0.36f);
                case CrystalCyan: return new Color(0.20f, 0.82f, 0.95f);
                case CrystalCyanLight: return new Color(0.62f, 0.96f, 1.00f);
                case CrystalBlue: return new Color(0.28f, 0.46f, 0.96f);
                case CrystalBlueLight: return new Color(0.62f, 0.76f, 1.00f);
                case CrystalViolet: return new Color(0.62f, 0.36f, 0.94f);
                case CrystalVioletLight: return new Color(0.82f, 0.66f, 1.00f);
                case CrystalTurquoise: return new Color(0.14f, 0.86f, 0.74f);
                case CrystalTurquoiseLight: return new Color(0.58f, 1.00f, 0.90f);
                case CrystalMagenta: return new Color(0.88f, 0.34f, 0.80f);
                case CrystalMagentaLight: return new Color(1.00f, 0.70f, 0.94f);
                case PortalGlow: return new Color(0.52f, 0.86f, 1.00f);
                case PortalGlowBright: return new Color(0.86f, 0.98f, 1.00f);
                case LampWarm: return new Color(1.00f, 0.82f, 0.46f);
                case StoneTeal: return new Color(0.30f, 0.50f, 0.58f);
                case StoneViolet: return new Color(0.44f, 0.36f, 0.62f);
                case Sand: return new Color(0.78f, 0.70f, 0.60f);
                case GoldBright: return new Color(1.00f, 0.88f, 0.42f);
                case GoldPale: return new Color(0.98f, 0.92f, 0.68f);
                case GoldShade: return new Color(0.58f, 0.42f, 0.14f);
                case BronzeDark: return new Color(0.46f, 0.30f, 0.20f);
                case StatueLight: return new Color(0.74f, 0.80f, 0.90f);
                case StatueMid: return new Color(0.54f, 0.60f, 0.74f);
                case StatueDark: return new Color(0.36f, 0.40f, 0.56f);
                case StatueTeal: return new Color(0.30f, 0.64f, 0.68f);
                case Basalt: return new Color(0.22f, 0.22f, 0.32f);
                case MarbleWarm: return new Color(0.93f, 0.89f, 0.83f);
                case WoodLight: return new Color(0.68f, 0.50f, 0.34f);
                case WoodAged: return new Color(0.44f, 0.36f, 0.32f);
                case TealStoneLight: return new Color(0.48f, 0.74f, 0.76f);
                case VioletStoneLight: return new Color(0.62f, 0.54f, 0.84f);
                case Indigo: return new Color(0.22f, 0.22f, 0.54f);
                case CrystalCyanDark: return new Color(0.10f, 0.52f, 0.74f);
                case CrystalBlueDark: return new Color(0.16f, 0.28f, 0.76f);
                case CrystalVioletDark: return new Color(0.42f, 0.20f, 0.74f);
                case CrystalTurquoiseDark: return new Color(0.08f, 0.60f, 0.54f);
                case CrystalMagentaDark: return new Color(0.64f, 0.18f, 0.60f);
                case CrystalWhite: return new Color(0.92f, 0.97f, 1.00f);
                case WaterTurquoise: return new Color(0.22f, 0.92f, 0.88f);
                case WaterDeep: return new Color(0.10f, 0.58f, 0.72f);
                case LampWarmDim: return new Color(0.92f, 0.64f, 0.32f);
                case Ember: return new Color(1.00f, 0.56f, 0.22f);
                case GemGold: return new Color(1.00f, 0.84f, 0.34f);
                case RubbleWarm: return new Color(0.60f, 0.50f, 0.44f);
                case RubbleWarmDark: return new Color(0.40f, 0.32f, 0.30f);
                case ClothGold: return new Color(0.84f, 0.66f, 0.26f);
                case ClothIndigo: return new Color(0.24f, 0.28f, 0.64f);
                case ClothPurple: return new Color(0.48f, 0.28f, 0.68f);
                case WarmStoneDark: return new Color(0.42f, 0.32f, 0.30f);
                case WarmStoneMid: return new Color(0.66f, 0.54f, 0.46f);
                case WarmStoneLight: return new Color(0.84f, 0.72f, 0.60f);
                case Soot: return new Color(0.18f, 0.16f, 0.18f);
                default: return Color.magenta;
            }
        }

        public static Texture2D Build()
        {
            int w = Columns * Cell, h = Rows * Cell;
            var pixels = new Color32[w * h];
            for (int i = 0; i < Rows * Columns; i++)
            {
                Color c = i < Count ? Colour(i) : Color.magenta;
                int cx = (i % Columns) * Cell, cy = (i / Columns) * Cell;
                for (int y = 0; y < Cell; y++)
                {
                    for (int x = 0; x < Cell; x++)
                    {
                        pixels[(cy + y) * w + cx + x] = c;
                    }
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            Directory.CreateDirectory(AmAssets.TextureFolder);
            string path = AmAssets.TextureFolder + "/AncientMine_Palette.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
#endif
