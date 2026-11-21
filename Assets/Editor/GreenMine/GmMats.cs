// Part of the DISABLED Green Mine generator: see the header of GreenMineSceneBuilder.cs.
#if GREEN_MINE_GENERATOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ItemCollector3D.EditorTools.GreenMine
{
    /// <summary>
    /// The small shared material set of Green Mine (URP Lit, matte, no specular / reflections unless noted). Every prop uses these, so the whole
    /// environment batches into a handful of materials. Colours follow the reference art: warm orange wood, lilac-grey rock, saturated greens, amber crystals.
    /// </summary>
    internal static class GmMats
    {
        public const string Folder = "Assets/Art/GreenMine/Materials";

        private readonly struct Spec
        {
            public readonly Color Color;
            public readonly float Smoothness;
            public readonly Color Emission;
            public readonly bool Glossy;

            public Spec(Color color, float smoothness = 0.08f, Color? emission = null, bool glossy = false)
            {
                Color = color;
                Smoothness = smoothness;
                Emission = emission ?? Color.black;
                Glossy = glossy;
            }
        }

        private static Color C(float r, float g, float b) => new(r, g, b, 1f);

        private static readonly Dictionary<string, Spec> Specs = new()
        {
            // greens
            ["leafA"] = new Spec(C(0.26f, 0.64f, 0.22f)),
            ["leafB"] = new Spec(C(0.40f, 0.76f, 0.24f)),
            ["leafC"] = new Spec(C(0.58f, 0.80f, 0.26f)),
            ["pineDark"] = new Spec(C(0.12f, 0.46f, 0.22f)),
            ["pineMid"] = new Spec(C(0.20f, 0.58f, 0.24f)),
            ["pineLight"] = new Spec(C(0.34f, 0.70f, 0.26f)),
            ["grassDark"] = new Spec(C(0.30f, 0.62f, 0.18f)),
            ["grassLight"] = new Spec(C(0.52f, 0.80f, 0.22f)),
            ["moss"] = new Spec(C(0.48f, 0.70f, 0.24f)),
            ["vine"] = new Spec(C(0.36f, 0.72f, 0.22f)),
            // wood
            ["trunk"] = new Spec(C(0.48f, 0.30f, 0.16f)),
            ["trunkDark"] = new Spec(C(0.34f, 0.21f, 0.12f)),
            ["wood"] = new Spec(C(0.84f, 0.50f, 0.20f)),
            ["woodLight"] = new Spec(C(0.94f, 0.66f, 0.34f)),
            ["woodDark"] = new Spec(C(0.58f, 0.30f, 0.14f)),
            ["woodRed"] = new Spec(C(0.62f, 0.28f, 0.20f)),
            ["rope"] = new Spec(C(0.82f, 0.68f, 0.42f)),
            ["wedge"] = new Spec(C(0.93f, 0.80f, 0.55f)),
            // stone / metal
            ["rock"] = new Spec(C(0.64f, 0.60f, 0.66f)),
            ["rockDark"] = new Spec(C(0.48f, 0.45f, 0.54f)),
            ["rockWarm"] = new Spec(C(0.70f, 0.60f, 0.54f)),
            ["stone"] = new Spec(C(0.70f, 0.68f, 0.70f)),
            ["iron"] = new Spec(C(0.34f, 0.36f, 0.42f), 0.25f),
            ["ironLight"] = new Spec(C(0.58f, 0.60f, 0.66f), 0.25f),
            // gold / crystal / light
            ["gold"] = new Spec(C(1.00f, 0.74f, 0.10f), 0.55f, C(0.20f, 0.12f, 0.0f), true),
            ["goldLight"] = new Spec(C(1.00f, 0.88f, 0.34f), 0.55f, C(0.22f, 0.16f, 0.02f), true),
            ["crystal"] = new Spec(C(1.00f, 0.80f, 0.16f), 0.65f, C(0.62f, 0.38f, 0.02f), true),
            ["crystalLight"] = new Spec(C(1.00f, 0.92f, 0.46f), 0.65f, C(0.70f, 0.50f, 0.08f), true),
            ["lanternGlass"] = new Spec(C(1.00f, 0.84f, 0.46f), 0.5f, C(2.20f, 1.40f, 0.50f), true),
            ["fire"] = new Spec(C(1.00f, 0.55f, 0.12f), 0.1f, C(2.4f, 1.1f, 0.2f)),
            ["fireCore"] = new Spec(C(1.00f, 0.90f, 0.40f), 0.1f, C(2.6f, 2.0f, 0.7f)),
            ["bulb"] = new Spec(C(1.00f, 0.86f, 0.5f), 0.1f, C(1.8f, 1.2f, 0.45f)),
            // cloth / paper
            ["canvas"] = new Spec(C(0.95f, 0.88f, 0.70f)),
            ["canvasShade"] = new Spec(C(0.82f, 0.72f, 0.54f)),
            ["flagRed"] = new Spec(C(0.88f, 0.22f, 0.14f)),
            ["banner"] = new Spec(C(0.26f, 0.58f, 0.22f)),
            ["bannerEdge"] = new Spec(C(0.14f, 0.40f, 0.16f)),
            ["bedroll"] = new Spec(C(0.28f, 0.46f, 0.78f)),
            ["paper"] = new Spec(C(0.96f, 0.90f, 0.72f)),
            ["mapInk"] = new Spec(C(0.56f, 0.36f, 0.20f)),
            // flowers
            ["flowerWhite"] = new Spec(C(0.98f, 0.98f, 0.94f)),
            ["flowerYellow"] = new Spec(C(1.00f, 0.84f, 0.18f)),
            ["flowerPink"] = new Spec(C(1.00f, 0.50f, 0.68f)),
            ["flowerPurple"] = new Spec(C(0.62f, 0.34f, 0.88f)),
            ["berry"] = new Spec(C(0.88f, 0.12f, 0.12f), 0.4f),
            ["mushroom"] = new Spec(C(0.90f, 0.16f, 0.12f), 0.3f),
            // interior / far
            ["tunnel"] = new Spec(C(0.17f, 0.11f, 0.09f)),
            ["tunnelDeep"] = new Spec(C(0.05f, 0.03f, 0.03f)),
            ["tunnelWall"] = new Spec(C(0.30f, 0.21f, 0.17f)),
            ["mountain"] = new Spec(C(0.52f, 0.52f, 0.66f)),
            ["mountainLight"] = new Spec(C(0.66f, 0.68f, 0.82f)),
            ["mountainGreen"] = new Spec(C(0.36f, 0.56f, 0.40f)),
            ["snow"] = new Spec(C(0.96f, 0.98f, 1.00f)),
            ["foam"] = new Spec(C(0.92f, 0.98f, 1.00f), 0.1f, C(0.25f, 0.3f, 0.32f)),
        };

        private static readonly Dictionary<string, Material> Cache = new();

        public static Material Get(string key)
        {
            if (Cache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            if (!Specs.TryGetValue(key, out Spec spec))
            {
                Debug.LogError("GmMats: unknown material key '" + key + "'");
                spec = new Spec(Color.magenta);
            }

            Material mat = LoadOrCreate("GM_" + key);
            mat.SetColor("_BaseColor", spec.Color);
            mat.SetFloat("_Smoothness", spec.Smoothness);
            mat.SetFloat("_Metallic", 0f);
            SetCheap(mat, spec.Glossy);
            if (spec.Emission.maxColorComponent > 0.001f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", spec.Emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            Cache[key] = mat;
            return mat;
        }

        public static Material[] Get(IReadOnlyList<string> keys)
        {
            var result = new Material[keys.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Get(keys[i]);
            }

            return result;
        }

        /// <summary>Opaque Lit material with a base texture (terrain).</summary>
        public static Material Textured(string name, Texture2D texture, float smoothness = 0f)
        {
            Material mat = LoadOrCreate("GM_" + name);
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            SetCheap(mat, false);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Alpha-blended textured Lit material (water, waterfall).</summary>
        public static Material Transparent(string name, Texture2D texture, Color tint, float smoothness, Color? emission = null)
        {
            Material mat = LoadOrCreate("GM_" + name);
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", 5f);
            mat.SetFloat("_DstBlend", 10f);
            mat.SetFloat("_SrcBlendAlpha", 1f);
            mat.SetFloat("_DstBlendAlpha", 10f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            mat.SetShaderPassEnabled("ShadowCaster", false);
            SetCheap(mat, true);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Additive soft glow (lantern / fire halo), the same particle shader as the menu dust.</summary>
        public static Material Glow(Texture2D glow)
        {
            Material mat = LoadOrCreate("GM_Glow", "Universal Render Pipeline/Particles/Unlit");
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", 1f);
            mat.SetFloat("_DstBlend", 1f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            mat.SetTexture("_BaseMap", glow);
            mat.SetColor("_BaseColor", new Color(1f, 0.72f, 0.30f, 0.34f));
            mat.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material Sky(Texture2D latLong)
        {
            Material mat = LoadOrCreate("GM_Sky", "Skybox/Panoramic");
            if (mat.shader.name != "Skybox/Panoramic")
            {
                mat.shader = Shader.Find("Skybox/Panoramic"); // an earlier build used the procedural sky
            }

            mat.SetTexture("_MainTex", latLong);
            mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            mat.SetFloat("_Exposure", 1.0f);
            mat.SetFloat("_Rotation", 0f);
            mat.SetFloat("_Mapping", 1f);   // latitude / longitude layout
            mat.SetFloat("_ImageType", 0f); // 360 degrees
            mat.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreate(string name, string shader = "Universal Render Pipeline/Lit")
        {
            Directory.CreateDirectory(Folder);
            string path = $"{Folder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shader)) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            return mat;
        }

        private static void SetCheap(Material mat, bool glossy)
        {
            mat.SetFloat("_SpecularHighlights", glossy ? 1f : 0f);
            mat.SetFloat("_EnvironmentReflections", glossy ? 1f : 0f);
            if (glossy)
            {
                mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            else
            {
                mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
        }
    }
}
#endif
