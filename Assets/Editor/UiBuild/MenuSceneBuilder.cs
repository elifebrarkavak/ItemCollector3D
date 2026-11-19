using System.Collections.Generic;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using static ItemCollector3D.EditorTools.UiPalette;

namespace ItemCollector3D.EditorTools
{
    /// <summary>
    /// Rebuilds the Main Menu scene: a warm, stylized gold mine (rock walls, glowing crystals, timber supports, a gold cart, a tunnel
    /// mouth with light, dust motes) behind the UI, plus the 3D miner preview stage. All geometry is generated low-poly meshes and
    /// primitives: cheap on mobile (no per-frame allocations, 3 point lights, one directional light).
    /// </summary>
    public static class MenuSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string MatFolder = "Assets/Art/Menu/Materials";

        private static readonly Dictionary<string, Material> Cache = new();

        private static Mesh Sphere => Resources.GetBuiltinResource<Mesh>("Sphere.fbx");   // radius 1 in this Unity version
        private static Mesh Cylinder => Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"); // radius 1, height 2
        private static Mesh Cube => Resources.GetBuiltinResource<Mesh>("Cube.fbx");         // 1 x 1 x 1

        // ================================================================ materials

        private static Material Mat(string name, Color color, float smoothness = 0.15f, Color? emission = null)
        {
            if (Cache.TryGetValue(name, out Material cached))
            {
                return cached;
            }

            System.IO.Directory.CreateDirectory(MatFolder);
            string path = $"{MatFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            EditorUtility.SetDirty(material);
            Cache[name] = material;
            return material;
        }

        private static Material DustMaterial()
        {
            string path = MatFolder + "/Cave_Dust.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Cave_Dust" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f); // additive
            material.SetFloat("_SrcBlend", 1f);
            material.SetFloat("_DstBlend", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(UiSpriteFactory.Folder + "/ui_glow.png"));
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ================================================================ object helpers

        private static GameObject Obj(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale, Vector3? euler = null, bool shadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = shadows;
            return go;
        }

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Vector3 Cyl(float radius, float height) => new Vector3(radius, height * 0.5f, radius);

        private static GameObject Glow(Transform parent, string name, Vector3 position, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = UiSpriteFactory.Load("ui_glow");
            sr.color = color;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            return go;
        }

        private static Light PointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        // ================================================================ build

        public static void Build()
        {
            Cache.Clear();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (string old in new[] { "Scenery", "CaveStage", "CaveLights", "MenuVolume", "CharacterStage" })
            {
                GameObject found = GameObject.Find(old);
                if (found != null) { Object.DestroyImmediate(found); }
            }

            Material rockDark = Mat("Cave_RockDark", Hex("#232E45"), 0.1f);
            Material rockMid = Mat("Cave_RockMid", Hex("#31405E"), 0.12f);
            Material rockLight = Mat("Cave_RockLight", Hex("#46587D"), 0.15f);
            Material floorMat = Mat("Cave_Floor", Hex("#4B362D"), 0.08f);
            Material floorPath = Mat("Cave_FloorPath", Hex("#6B4E3E"), 0.08f);
            Material wood = Mat("Cave_Wood", WoodBrown, 0.15f);
            Material woodDark = Mat("Cave_WoodDark", Hex("#4F3022"), 0.12f);
            Material steel = Mat("Cave_Steel", Hex("#7E8CA0"), 0.55f);
            Material crystalGold = Mat("Cave_CrystalGold", Hex("#F5BE45"), 0.7f, Gold * 1.25f);
            Material crystalGreen = Mat("Cave_CrystalGreen", Hex("#3FA88A"), 0.7f, NaturalGreen * 1.4f);
            Material goldPile = Mat("Cave_GoldNugget", Gold, 0.65f, Gold * 0.35f);
            Material lampMat = Mat("Cave_LanternGlass", Hex("#FFE7A0"), 0.8f, new Color(1f, 0.72f, 0.3f) * 2.2f);
            Material mossMat = Mat("Cave_Moss", Hex("#2F6B58"), 0.1f);

            var root = new GameObject("Scenery").transform;

            // ---------------------------------------------------------- floor + rails
            Transform floor = Group(root, "Floor");
            Obj(floor, "Ground", MeshFactory.Floor("Cave_Floor", 70f, 26, 0.5f), floorMat, new Vector3(2f, -0.12f, 16f), Vector3.one);
            Transform rails = Group(root, "Rails");
            rails.localPosition = new Vector3(3.2f, 0f, 17.2f);
            rails.localRotation = Quaternion.Euler(0f, 166f, 0f);
            Obj(rails, "PathBed", Cube, floorPath, new Vector3(0f, -0.02f, 10f), new Vector3(2.6f, 0.04f, 22f));
            for (int i = 0; i < 11; i++)
            {
                Obj(rails, "Sleeper" + i, Cube, woodDark, new Vector3(0f, 0.04f, 1.2f + i * 2f), new Vector3(2.2f, 0.09f, 0.28f));
            }

            Obj(rails, "RailL", Cube, steel, new Vector3(-0.65f, 0.14f, 10f), new Vector3(0.11f, 0.1f, 22f));
            Obj(rails, "RailR", Cube, steel, new Vector3(0.65f, 0.14f, 10f), new Vector3(0.11f, 0.1f, 22f));

            // ---------------------------------------------------------- cave walls
            Transform walls = Group(root, "CaveWalls");
            Mesh rockA = MeshFactory.Rock("Cave_RockA", 3, 0.42f, 2);
            Mesh rockB = MeshFactory.Rock("Cave_RockB", 11, 0.5f, 2);
            Mesh rockC = MeshFactory.Rock("Cave_RockC", 23, 0.34f, 1);
            Obj(walls, "BackWall", Cube, rockDark, new Vector3(2f, 6f, 27f), new Vector3(70f, 26f, 2f));
            Obj(walls, "WallLeftBig", rockA, rockMid, new Vector3(-8.5f, 3.5f, 16f), new Vector3(8.5f, 11f, 7f), new Vector3(0f, 25f, 0f));
            Obj(walls, "WallLeftFar", rockB, rockDark, new Vector3(-18f, 4.5f, 22f), new Vector3(11f, 13f, 8f), new Vector3(0f, 80f, 0f));
            Obj(walls, "WallRightBig", rockB, rockMid, new Vector3(14.5f, 4f, 15f), new Vector3(9f, 11.5f, 7f), new Vector3(0f, -30f, 0f));
            Obj(walls, "WallRightFar", rockA, rockDark, new Vector3(24f, 5f, 24f), new Vector3(11f, 14f, 8f), new Vector3(0f, 10f, 0f));
            Obj(walls, "ArchTop", rockA, rockMid, new Vector3(3f, 11.2f, 19.5f), new Vector3(10.5f, 4.2f, 4f), new Vector3(0f, 0f, 0f));
            Obj(walls, "ArchLeft", rockC, rockLight, new Vector3(-2.4f, 4.4f, 18.3f), new Vector3(2.4f, 5.6f, 2.8f), new Vector3(0f, 15f, 0f));
            Obj(walls, "ArchRight", rockC, rockLight, new Vector3(8.6f, 4.2f, 18.3f), new Vector3(2.6f, 5.4f, 2.8f), new Vector3(0f, -20f, 0f));
            Obj(walls, "CeilingA", rockB, rockDark, new Vector3(-5f, 12.5f, 9f), new Vector3(8f, 4.2f, 6f), new Vector3(0f, 40f, 0f));
            Obj(walls, "CeilingB", rockA, rockDark, new Vector3(9f, 12.8f, 7f), new Vector3(8f, 4.4f, 6f), new Vector3(0f, -10f, 0f));
            Obj(walls, "CeilingC", rockC, rockMid, new Vector3(2f, 14.5f, 15f), new Vector3(10f, 4f, 6f), new Vector3(0f, 0f, 0f));
            Obj(walls, "RockFrontLeft", rockC, rockMid, new Vector3(-6.6f, 0.4f, 0.8f), new Vector3(2.6f, 2.1f, 2.2f), new Vector3(0f, 30f, 0f));
            Obj(walls, "RockFrontLeft2", rockA, rockLight, new Vector3(-8.5f, 0.2f, 4.5f), new Vector3(3f, 2.4f, 2.6f), new Vector3(0f, 70f, 0f));
            Obj(walls, "RockMidRight", rockC, rockMid, new Vector3(8.2f, 0.5f, 6f), new Vector3(3.4f, 2.6f, 3f), new Vector3(0f, -40f, 0f));
            Obj(walls, "RockMidLeft", rockB, rockDark, new Vector3(-4.2f, 0.3f, 9.5f), new Vector3(3.6f, 2.4f, 3f), new Vector3(0f, 10f, 0f));
            Obj(walls, "RockRightFar", rockB, rockLight, new Vector3(11f, 0.6f, 11.5f), new Vector3(3.6f, 3.2f, 3f), new Vector3(0f, 55f, 0f));
            Obj(walls, "MossPatchA", rockC, mossMat, new Vector3(-3.2f, 0.05f, 6.4f), new Vector3(1.4f, 0.4f, 1.1f), new Vector3(0f, 20f, 0f));
            Obj(walls, "MossPatchB", rockC, mossMat, new Vector3(6.2f, 0.05f, 9f), new Vector3(1.8f, 0.4f, 1.3f), new Vector3(0f, -30f, 0f));

            // ---------------------------------------------------------- the mine entrance (kept name: the old test looks for it)
            Transform entrance = Group(root, "MineEntrance");
            entrance.localPosition = new Vector3(3.2f, 0f, 17.4f);
            Obj(entrance, "Opening", Cube, Mat("Cave_Opening", Hex("#FFB347"), 0f, new Color(1f, 0.62f, 0.2f) * 2.4f), new Vector3(0f, 2.4f, 1.1f), new Vector3(3.4f, 4.6f, 0.2f));
            Obj(entrance, "PostL", Cube, wood, new Vector3(-1.95f, 2.5f, 0f), new Vector3(0.6f, 5.2f, 0.7f), new Vector3(0f, 0f, 1.5f));
            Obj(entrance, "PostR", Cube, wood, new Vector3(1.95f, 2.5f, 0f), new Vector3(0.6f, 5.2f, 0.7f), new Vector3(0f, 0f, -1.5f));
            Obj(entrance, "Beam", Cube, wood, new Vector3(0f, 5.15f, 0f), new Vector3(5.2f, 0.7f, 0.8f));
            Obj(entrance, "BeamTop", Cube, woodDark, new Vector3(0f, 5.65f, 0.05f), new Vector3(5.7f, 0.35f, 0.9f));

            // ---------------------------------------------------------- crystals (gold + a little green)
            Transform crystals = Group(root, "Crystals");
            var pulseRenderers = new List<Renderer>();
            var pulseColors = new List<Color>();
            void Cluster(string name, Vector3 center, float scale, Material material, Color emission, int seedBase, int count)
            {
                Transform cluster = Group(crystals, name);
                cluster.localPosition = center;
                var random = new System.Random(seedBase);
                for (int i = 0; i < count; i++)
                {
                    Mesh mesh = MeshFactory.Crystal("Cave_Crystal" + (seedBase + i), seedBase + i);
                    float h = (float)(0.8 + random.NextDouble() * 1.7) * scale;
                    float w = (float)(0.5 + random.NextDouble() * 0.35) * scale;
                    float angle = i / (float)count * 360f + (float)random.NextDouble() * 30f;
                    float dist = i == 0 ? 0f : (0.35f + (float)random.NextDouble() * 0.55f) * scale;
                    var pos = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * dist, -0.05f, Mathf.Sin(angle * Mathf.Deg2Rad) * dist * 0.6f);
                    var tilt = new Vector3((float)(random.NextDouble() * 24 - 12), (float)(random.NextDouble() * 360), (float)(random.NextDouble() * 24 - 12));
                    GameObject crystal = Obj(cluster, "Crystal" + i, mesh, material, pos, new Vector3(w, h, w), tilt);
                    pulseRenderers.Add(crystal.GetComponent<Renderer>());
                    pulseColors.Add(emission);
                }
            }

            Cluster("CrystalsLeft", new Vector3(-1.8f, 0f, 6.6f), 1.5f, crystalGold, Gold * 1.25f, 100, 6);
            Cluster("CrystalsTunnel", new Vector3(-0.2f, 0f, 14.6f), 1.8f, crystalGold, Gold * 1.25f, 200, 6);
            Cluster("CrystalsTunnelR", new Vector3(7.6f, 0f, 13.8f), 1.5f, crystalGold, Gold * 1.25f, 300, 5);
            Cluster("CrystalsRightFront", new Vector3(6.9f, 0f, 3.2f), 0.9f, crystalGold, Gold * 1.25f, 400, 4);
            Cluster("CrystalsGreen", new Vector3(-3.6f, 0f, 8.4f), 1.1f, crystalGreen, NaturalGreen * 1.4f, 500, 4);
            Cluster("CrystalsGreenR", new Vector3(9.4f, 0f, 8.6f), 0.9f, crystalGreen, NaturalGreen * 1.4f, 600, 3);
            // hanging gold crystals at the top-left (upside down)
            Transform hanging = Group(crystals, "CrystalsHanging");
            hanging.localPosition = new Vector3(-10.5f, 7.6f, 9f);
            for (int i = 0; i < 4; i++)
            {
                GameObject c = Obj(hanging, "Hang" + i, MeshFactory.Crystal("Cave_CrystalH" + i, 700 + i), crystalGold, new Vector3(i * 0.9f - 1.3f, 0f, i % 2 * 0.6f),
                    new Vector3(0.6f, 1.2f + i * 0.35f, 0.6f), new Vector3(180f, i * 50f, 8f - i * 5f));
                pulseRenderers.Add(c.GetComponent<Renderer>());
                pulseColors.Add(Gold * 1.25f);
            }

            // ---------------------------------------------------------- timber supports + lanterns
            Transform timber = Group(root, "Timber");
            Obj(timber, "PostLeft", Cube, wood, new Vector3(-3.6f, 3f, 7.2f), new Vector3(0.7f, 6.4f, 0.7f), new Vector3(0f, 0f, 2f));
            Obj(timber, "PostRight", Cube, wood, new Vector3(8.6f, 3f, 6.8f), new Vector3(0.7f, 6.4f, 0.7f), new Vector3(0f, 0f, -2f));
            Obj(timber, "BeamCross", Cube, wood, new Vector3(2.5f, 6.3f, 7f), new Vector3(13.4f, 0.75f, 0.8f), new Vector3(0f, 0f, 0.6f));
            Obj(timber, "BraceLeft", Cube, woodDark, new Vector3(-2.3f, 5.5f, 7.1f), new Vector3(0.4f, 2.2f, 0.45f), new Vector3(0f, 0f, 40f));
            Obj(timber, "BraceRight", Cube, woodDark, new Vector3(7.3f, 5.5f, 6.9f), new Vector3(0.4f, 2.2f, 0.45f), new Vector3(0f, 0f, -40f));
            Transform lanterns = Group(root, "Lanterns");
            var lanternLights = new List<Light>();
            void Lantern(string name, Vector3 pos)
            {
                Transform l = Group(lanterns, name);
                l.localPosition = pos;
                Obj(l, "Chain", Cube, steel, new Vector3(0f, 0.55f, 0f), new Vector3(0.05f, 1.1f, 0.05f));
                Obj(l, "Cap", Cylinder, woodDark, new Vector3(0f, 0.08f, 0f), Cyl(0.3f, 0.12f));
                Obj(l, "Glass", Cylinder, lampMat, new Vector3(0f, -0.22f, 0f), Cyl(0.22f, 0.5f));
                Obj(l, "Base", Cylinder, woodDark, new Vector3(0f, -0.52f, 0f), Cyl(0.28f, 0.1f));
                Glow(l, "Halo", new Vector3(0f, -0.22f, -0.1f), 4.2f, new Color(1f, 0.72f, 0.3f, 0.55f));
            }

            Lantern("LanternLeft", new Vector3(-3.5f, 4.5f, 6.5f));
            Lantern("LanternRight", new Vector3(8.4f, 4.5f, 6.3f));

            // ---------------------------------------------------------- gold cart
            Transform cart = Group(rails, "Cart");
            cart.localPosition = new Vector3(0f, 0.2f, 9.5f);
            cart.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Obj(cart, "Body", Cube, wood, new Vector3(0f, 0.55f, 0f), new Vector3(2.3f, 0.95f, 1.5f));
            Obj(cart, "Rim", Cube, woodDark, new Vector3(0f, 1.05f, 0f), new Vector3(2.5f, 0.12f, 1.7f));
            Obj(cart, "Band1", Cube, steel, new Vector3(-0.6f, 0.55f, 0f), new Vector3(0.12f, 1.0f, 1.56f));
            Obj(cart, "Band2", Cube, steel, new Vector3(0.6f, 0.55f, 0f), new Vector3(0.12f, 1.0f, 1.56f));
            for (int i = 0; i < 9; i++)
            {
                float a = i * 2.3f;
                Obj(cart, "Gold" + i, Sphere, goldPile, new Vector3(Mathf.Sin(a) * 0.7f, 1.22f + (i % 3) * 0.12f, Mathf.Cos(a * 1.4f) * 0.4f), Vector3.one * (0.26f + (i % 3) * 0.05f));
            }

            Obj(cart, "WheelFL", Cylinder, steel, new Vector3(-0.65f, 0.0f, 0.82f), new Vector3(0.3f, 0.06f, 0.3f), new Vector3(90f, 0f, 0f));
            Obj(cart, "WheelFR", Cylinder, steel, new Vector3(0.65f, 0.0f, 0.82f), new Vector3(0.3f, 0.06f, 0.3f), new Vector3(90f, 0f, 0f));
            Obj(cart, "WheelBL", Cylinder, steel, new Vector3(-0.65f, 0.0f, -0.82f), new Vector3(0.3f, 0.06f, 0.3f), new Vector3(90f, 0f, 0f));
            Obj(cart, "WheelBR", Cylinder, steel, new Vector3(0.65f, 0.0f, -0.82f), new Vector3(0.3f, 0.06f, 0.3f), new Vector3(90f, 0f, 0f));

            // ---------------------------------------------------------- glows (cheap atmosphere) + dust
            Transform glows = Group(root, "Glows");
            Glow(glows, "TunnelGlow", new Vector3(3.2f, 3f, 16.2f), 17f, new Color(1f, 0.66f, 0.22f, 0.62f));
            Glow(glows, "TunnelGlowCore", new Vector3(3.2f, 2.6f, 16.0f), 8f, new Color(1f, 0.9f, 0.55f, 0.7f));
            Glow(glows, "RimGlow", new Vector3(3.2f, 3.4f, 6f), 15f, new Color(1f, 0.7f, 0.28f, 0.28f));
            Glow(glows, "CrystalGlowLeft", new Vector3(-1.8f, 1.6f, 6f), 7f, new Color(1f, 0.75f, 0.25f, 0.4f));

            var dustGo = new GameObject("Dust");
            dustGo.transform.SetParent(glows, false);
            dustGo.transform.localPosition = new Vector3(2f, 3f, 5f);
            ParticleSystem dust = dustGo.AddComponent<ParticleSystem>();
            var main = dust.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.4f, 0.7f), new Color(1f, 0.95f, 0.75f, 0.9f));
            main.maxParticles = 70;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var emission = dust.emission;
            emission.rateOverTime = 9f;
            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(18f, 7f, 14f);
            var velocity = dust.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f); // all three axes must use the same curve mode
            var fade = dust.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var dustRenderer = dustGo.GetComponent<ParticleSystemRenderer>();
            dustRenderer.sharedMaterial = DustMaterial();
            dustRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // ---------------------------------------------------------- lights
            var lights = new GameObject("CaveLights").transform;
            Light tunnel = PointLight(lights, "TunnelLight", new Vector3(3f, 3.2f, 12f), new Color(1f, 0.66f, 0.28f), 22f, 22f);
            Light crystalLeft = PointLight(lights, "CrystalLight", new Vector3(-1.5f, 2.2f, 4.5f), new Color(1f, 0.78f, 0.35f), 7f, 9f);
            Light rim = PointLight(lights, "CharacterRim", new Vector3(4.6f, 3.4f, 2.4f), new Color(1f, 0.72f, 0.4f), 9f, 9f);
            lanternLights.Add(tunnel);
            lanternLights.Add(crystalLeft);
            lanternLights.Add(rim);

            Light sun = null;
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l.type == LightType.Directional) { sun = l; }
            }

            sun.name = "Key Light";
            sun.color = new Color(1f, 0.88f, 0.68f);
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
            sun.transform.rotation = Quaternion.Euler(30f, -32f, 0f);

            var ambience = new GameObject("MenuAmbience").AddComponent<MenuAmbience>();
            var ambienceSo = new SerializedObject(ambience);
            SetArray(ambienceSo.FindProperty("flickerLights"), lanternLights.ToArray());
            SetArray(ambienceSo.FindProperty("pulseRenderers"), pulseRenderers.ToArray());
            SerializedProperty colors = ambienceSo.FindProperty("pulseColors");
            colors.arraySize = pulseColors.Count;
            for (int i = 0; i < pulseColors.Count; i++) { colors.GetArrayElementAtIndex(i).colorValue = pulseColors[i]; }
            ambienceSo.ApplyModifiedPropertiesWithoutUndo();

            // ---------------------------------------------------------- environment settings
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#51638A");
            RenderSettings.ambientEquatorColor = Hex("#34405C");
            RenderSettings.ambientGroundColor = Hex("#2C201C");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Hex("#1A2336");
            RenderSettings.fogDensity = 0.011f;
            RenderSettings.skybox = null;

            // ---------------------------------------------------------- camera
            Camera camera = Camera.main;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 2.2f, -10f), Quaternion.identity);
            camera.fieldOfView = 32f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Hex("#141C2C");
            camera.allowHDR = true;
            var camData = camera.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;

            // post processing (soft bloom makes the crystals glow, vignette keeps the eye on the miner)
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/MenuVolumeProfile.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, "Assets/Settings/MenuVolumeProfile.asset");
            }

            profile.components.Clear();
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.7f);
            bloom.scatter.Override(0.6f);
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.5f);
            var grading = profile.Add<ColorAdjustments>();
            grading.saturation.Override(12f);
            grading.contrast.Override(8f);
            EditorUtility.SetDirty(profile);
            var volumeGo = new GameObject("MenuVolume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            // ---------------------------------------------------------- the miner preview stage
            var stageGo = new GameObject("CharacterStage");
            var stage = stageGo.AddComponent<MenuCharacterStage>();
            Transform pedestal = BuildPedestal(stageGo.transform, rockLight, rockMid, Mat("Cave_PedestalGold", Gold, 0.6f, Gold * 0.9f));
            var stageSo = new SerializedObject(stage);
            stageSo.FindProperty("stageCamera").objectReferenceValue = camera;
            stageSo.FindProperty("catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CharacterBuilder.CatalogPath);
            stageSo.FindProperty("pedestal").objectReferenceValue = pedestal;
            stageSo.ApplyModifiedPropertiesWithoutUndo();

            // ---------------------------------------------------------- UI: swap in the new Main Menu prefab
            GameObject safeArea = GameObject.Find("SafeAreaRoot");
            Transform oldUi = safeArea.transform.Find("MainMenuUI");
            if (oldUi != null) { Object.DestroyImmediate(oldUi.gameObject); }
            var uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuUiBuilder.MainMenuPath);
            var ui = (GameObject)PrefabUtility.InstantiatePrefab(uiPrefab, safeArea.transform);
            ui.name = "MainMenuUI";
            var uiRect = (RectTransform)ui.transform;
            uiRect.anchorMin = Vector2.zero; uiRect.anchorMax = Vector2.one; uiRect.offsetMin = uiRect.offsetMax = Vector2.zero;
            var characterPanel = ui.GetComponentInChildren<CharacterSelectController>(true);
            if (characterPanel != null)
            {
                UiKit.SetRef(characterPanel, "stage", stage);
            }

            ConfigureUi();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Canvas setup + character stage framing of the open Main Menu scene (stage 10). Called by the full rebuild above and callable on its own
        /// (<see cref="ApplyUiSettings"/>) so the generated scene and the builder never disagree.
        /// </summary>
        private static void ConfigureUi()
        {
            UiKit.ConfigureScaler(GameObject.Find("UI Canvas").GetComponent<UnityEngine.UI.CanvasScaler>());
            UiKit.ConfigureSafeArea(GameObject.Find("SafeAreaRoot").GetComponent<SafeAreaFitter>());

            // Character Selection: the miner stands higher and a little smaller so the centred name / SELECT column fits under the pedestal
            var stage = Object.FindAnyObjectByType<MenuCharacterStage>(FindObjectsInactive.Include);
            if (stage != null)
            {
                var so = new SerializedObject(stage);
                so.FindProperty("selectAnchor").vector2Value = new Vector2(0.5f, 0.375f);
                so.FindProperty("selectHeightFraction").floatValue = 0.47f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>Re-applies only the canvas / safe area / stage settings to the Main Menu scene and saves it (no full scene rebuild).</summary>
        public static void ApplyUiSettings()
        {
            Scene scene = SceneManager.GetActiveScene().path == ScenePath ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ConfigureUi();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) { property.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }
        }

        /// <summary>Stone podium + gold ring + soft glow under the miner. Authored in model units (feet at y = 0, miner radius ~0.6).</summary>
        private static Transform BuildPedestal(Transform parent, Material stone, Material stoneDark, Material gold)
        {
            var root = new GameObject("Pedestal").transform;
            root.SetParent(parent, false);
            Obj(root, "Base", Cylinder, stoneDark, new Vector3(0f, -0.2f, 0f), Cyl(1.2f, 0.24f));
            Obj(root, "Top", Cylinder, stone, new Vector3(0f, -0.06f, 0f), Cyl(1.02f, 0.12f));
            Obj(root, "GoldRing", Cylinder, gold, new Vector3(0f, -0.005f, 0f), Cyl(1.05f, 0.03f));
            GameObject glow = Glow(root, "FloorGlow", new Vector3(0f, -0.3f, 0f), 6f, new Color(1f, 0.78f, 0.35f, 0.45f));
            glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return root;
        }
    }
}
