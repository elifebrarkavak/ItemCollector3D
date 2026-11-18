using System.Collections.Generic;
using ItemCollector3D.Characters;
using UnityEditor;
using UnityEngine;
using static ItemCollector3D.EditorTools.UiPalette;

namespace ItemCollector3D.EditorTools
{
    /// <summary>
    /// Builds the two stylized TEMPORARY miners (Concept Document logo: orange-bearded miner, red-braided miner girl; both with a
    /// yellow hard hat and head lamp) from primitives and a generated hemisphere mesh, and writes the character catalog. They share one
    /// rig layout (body, head, arms, legs, lamp) so the same MinerAnimator drives both. These are placeholders for real modelled
    /// characters; see Docs for the integration requirements.
    /// </summary>
    public static class CharacterBuilder
    {
        public const string ArtFolder = "Assets/Art/Characters";
        public const string PrefabFolder = "Assets/Prefabs/Characters";
        public const string CatalogPath = "Assets/Data/Characters/CharacterCatalog.asset";

        private static readonly Dictionary<string, Material> MaterialCache = new();

        /// <summary>Adds the PlayerCharacterVisual to the Player prefab (the controller, collider, camera and dig system are not touched).</summary>
        public static void IntegratePlayer()
        {
            const string playerPath = "Assets/Prefabs/Player/Player.prefab";
            GameObject player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                var visual = player.GetComponent<PlayerCharacterVisual>();
                if (visual == null)
                {
                    visual = player.AddComponent<PlayerCharacterVisual>();
                }

                var so = new SerializedObject(visual);
                so.FindProperty("catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
                so.FindProperty("visual").objectReferenceValue = player.transform.Find("Visual");
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        public static void BuildAll()
        {
            System.IO.Directory.CreateDirectory(ArtFolder + "/Materials");
            System.IO.Directory.CreateDirectory(ArtFolder + "/Meshes");
            System.IO.Directory.CreateDirectory(PrefabFolder);
            System.IO.Directory.CreateDirectory("Assets/Data/Characters");
            MaterialCache.Clear();

            GameObject male = BuildMiner(female: false);
            GameObject maleAsset = PrefabUtility.SaveAsPrefabAsset(male, PrefabFolder + "/Miner_Barnaby.prefab");
            Object.DestroyImmediate(male);
            GameObject female = BuildMiner(female: true);
            GameObject femaleAsset = PrefabUtility.SaveAsPrefabAsset(female, PrefabFolder + "/Miner_Ruby.prefab");
            Object.DestroyImmediate(female);

            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.SetEntries(new[]
            {
                new CharacterCatalog.Entry { id = CharacterSelection.MaleId, displayName = "BARNABY", tagline = "The Bearded Digger", prefab = maleAsset },
                new CharacterCatalog.Entry { id = CharacterSelection.FemaleId, displayName = "RUBY", tagline = "The Braided Explorer", prefab = femaleAsset },
            });
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        // ================================================================ materials

        public static Material Mat(string name, Color color, float smoothness = 0.3f, Color? emission = null)
        {
            if (MaterialCache.TryGetValue(name, out Material cached))
            {
                return cached;
            }

            string path = $"{ArtFolder}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            // a faint self-glow (about 20% of the base colour) keeps the faces and clothes warm and readable in the dim cave
            emission ??= color * 0.2f;
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            EditorUtility.SetDirty(material);
            MaterialCache[name] = material;
            return material;
        }

        // ================================================================ mesh helpers

        private static Mesh Builtin(string name) => Resources.GetBuiltinResource<Mesh>(name);
        private static Mesh Sphere => Builtin("Sphere.fbx");
        private static Mesh Capsule => Builtin("Capsule.fbx");
        private static Mesh Cylinder => Builtin("Cylinder.fbx");

        private static Mesh hemisphere;

        private static Mesh Hemisphere()
        {
            if (hemisphere != null)
            {
                return hemisphere;
            }

            string path = ArtFolder + "/Meshes/Hemisphere.asset";
            hemisphere = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (hemisphere == null)
            {
                hemisphere = new Mesh { name = "Hemisphere" };
                AssetDatabase.CreateAsset(hemisphere, path);
            }

            const int lon = 28, lat = 12;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int y = 0; y <= lat; y++)
            {
                float phi = y / (float)lat * Mathf.PI * 0.5f; // 0 = rim, 90 = pole
                for (int x = 0; x <= lon; x++)
                {
                    float theta = x / (float)lon * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Cos(phi) * Mathf.Cos(theta), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(theta));
                    vertices.Add(n * 0.5f);
                    normals.Add(n);
                    uvs.Add(new Vector2(x / (float)lon, y / (float)lat));
                }
            }

            var triangles = new List<int>();
            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    int a = y * (lon + 1) + x, b = a + lon + 1;
                    triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            }

            hemisphere.Clear();
            hemisphere.SetVertices(vertices);
            hemisphere.SetNormals(normals);
            hemisphere.SetUVs(0, uvs);
            hemisphere.SetTriangles(triangles, 0);
            // winding: make the outside face the camera
            hemisphere.triangles = FlipIfNeeded(hemisphere.triangles);
            hemisphere.RecalculateBounds();
            EditorUtility.SetDirty(hemisphere);
            return hemisphere;
        }

        /// <summary>A thin tube along a circular arc in the XY plane (smile). Angles in degrees, 270 = bottom.</summary>
        private static Mesh Arc(string name, float radius, float tube, float startDeg, float endDeg)
        {
            string path = $"{ArtFolder}/Meshes/{name}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }

            const int steps = 16, around = 8;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(startDeg, endDeg, i / (float)steps) * Mathf.Deg2Rad;
                var center = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                // taper the ends so the smile has soft tips
                float taper = Mathf.Sin(Mathf.Clamp01(i / (float)steps) * Mathf.PI) * 0.6f + 0.4f;
                for (int j = 0; j < around; j++)
                {
                    float b = j / (float)around * Mathf.PI * 2f;
                    Vector3 n = radial * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                    vertices.Add(center + n * tube * taper);
                    normals.Add(n);
                }
            }

            var triangles = new List<int>();
            for (int i = 0; i < steps; i++)
            {
                for (int j = 0; j < around; j++)
                {
                    int a = i * around + j, b = i * around + (j + 1) % around;
                    int c = a + around, d = b + around;
                    triangles.AddRange(new[] { a, b, c, b, d, c });
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.triangles = FlipIfNeeded(mesh.triangles);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static int[] FlipIfNeeded(int[] triangles)
        {
            // the lat/long order above yields clockwise triangles seen from outside in Unity's left-handed system: flip them
            for (int i = 0; i < triangles.Length; i += 3)
            {
                (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
            }

            return triangles;
        }

        private static GameObject Part(Transform parent, string name, Mesh mesh, Vector3 worldPos, Vector3 scale, Material material, Vector3? euler = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos; // root is at the origin and unrotated while building, so this is "absolute"
            go.transform.localScale = Vector3.Scale(scale, UnitFactor(mesh));
            if (euler.HasValue)
            {
                go.transform.localRotation = Quaternion.Euler(euler.Value);
            }

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = false; // the brim must not shade the face away; the model still casts a shadow on the floor
            return go;
        }

        // The built-in .fbx primitives of this Unity version are twice the classic size (sphere d=2, capsule 2x4, cylinder 2x2).
        // Part scales are written for the classic unit primitives (sphere d=1, capsule 1x2x1, cylinder 1x2x1), so correct here.
        private static Vector3 UnitFactor(Mesh mesh)
        {
            if (mesh != Sphere && mesh != Capsule && mesh != Cylinder) { return Vector3.one; }
            Vector3 size = mesh.bounds.size;
            float intendedHeight = mesh == Sphere ? 1f : 2f;
            return new Vector3(1f / size.x, intendedHeight / size.y, 1f / size.z);
        }

        private static Transform Pivot(Transform parent, string name, Vector3 worldPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            return go.transform;
        }

        // ================================================================ the miner

        private static GameObject BuildMiner(bool female)
        {
            string tag = female ? "Ruby" : "Barnaby";
            var root = new GameObject("Miner_" + tag);

            Color shirt = female ? Cream : NaturalGreen;
            Color overalls = female ? Hex("#2F4A73") : WoodBrown;
            Color boots = female ? Hex("#5A3822") : Hex("#3A2418");
            Material skinMat = Mat("Char_Skin", Hex("#FFC8A2"), 0.25f, Hex("#FFB08A") * 0.55f);
            Material shirtMat = Mat($"Char_Shirt_{tag}", shirt, 0.2f);
            Material overallsMat = Mat($"Char_Overalls_{tag}", overalls, 0.2f);
            Material bootMat = Mat($"Char_Boots_{tag}", boots, 0.25f);
            Material goldMat = Mat("Char_Gold", Gold, 0.55f);
            Material hatMat = Mat("Char_Hat", Hex("#F6BE45"), 0.5f);
            Material hatDarkMat = Mat("Char_HatDark", Hex("#D9962A"), 0.45f);
            Material eyeMat = Mat("Char_Eye", Hex("#1A2233"), 0.7f);
            Material whiteMat = Mat("Char_EyeShine", Color.white, 0.9f, Color.white * 0.4f);
            Material cheekMat = Mat("Char_Cheek", Hex("#F08C7E"), 0.1f);
            Material steelMat = Mat("Char_Steel", Hex("#9AA7B8"), 0.6f);
            Material handleMat = Mat("Char_Handle", Hex("#8A5A3A"), 0.2f);
            Material lensMat = Mat("Char_Lens", Hex("#FFF6D6"), 0.8f, new Color(1f, 0.86f, 0.45f) * 1.6f);
            Material lampMat = Mat("Char_Lamp", Hex("#5B6677"), 0.5f);

            // ---- legs + boots (pivot at the hip)
            Transform legL = Pivot(root.transform, "LegL", new Vector3(-0.2f, 0.52f, 0f));
            Transform legR = Pivot(root.transform, "LegR", new Vector3(0.2f, 0.52f, 0f));
            foreach ((Transform leg, float x) in new[] { (legL, -0.2f), (legR, 0.2f) })
            {
                Part(leg, "Leg", Capsule, new Vector3(x, 0.32f, 0f), new Vector3(0.29f, 0.23f, 0.29f), overallsMat);
                Part(leg, "Boot", Sphere, new Vector3(x, 0.12f, 0.07f), new Vector3(0.36f, 0.26f, 0.5f), bootMat);
                Part(leg, "BootToe", Sphere, new Vector3(x, 0.1f, 0.2f), new Vector3(0.3f, 0.2f, 0.3f), bootMat);
            }

            // ---- body (pivot at the hips)
            Transform body = Pivot(root.transform, "Body", new Vector3(0f, 0.6f, 0f));
            Part(body, "Torso", Sphere, new Vector3(0f, 0.8f, 0f), new Vector3(0.76f, 0.66f, 0.6f), shirtMat);
            Part(body, "OverallsLower", Sphere, new Vector3(0f, 0.68f, 0f), new Vector3(0.8f, 0.48f, 0.64f), overallsMat);
            Part(body, "Bib", Sphere, new Vector3(0f, 0.86f, 0.25f), new Vector3(0.46f, 0.36f, 0.16f), overallsMat);
            Part(body, "StrapL", Capsule, new Vector3(-0.2f, 1.0f, 0.18f), new Vector3(0.08f, 0.2f, 0.08f), overallsMat, new Vector3(0f, 0f, -12f));
            Part(body, "StrapR", Capsule, new Vector3(0.2f, 1.0f, 0.18f), new Vector3(0.08f, 0.2f, 0.08f), overallsMat, new Vector3(0f, 0f, 12f));
            Part(body, "ButtonL", Sphere, new Vector3(-0.17f, 0.97f, 0.31f), new Vector3(0.07f, 0.07f, 0.04f), goldMat);
            Part(body, "ButtonR", Sphere, new Vector3(0.17f, 0.97f, 0.31f), new Vector3(0.07f, 0.07f, 0.04f), goldMat);
            Part(body, "Pocket", Sphere, new Vector3(0f, 0.8f, 0.325f), new Vector3(0.2f, 0.12f, 0.03f), Mat($"Char_Pocket_{tag}", Color.Lerp(overalls, Color.black, 0.18f), 0.2f));

            // ---- arms (pivot at the shoulders)
            Transform armL = Pivot(body, "ArmL", new Vector3(-0.44f, 1.0f, 0f));
            Transform armR = Pivot(body, "ArmR", new Vector3(0.44f, 1.0f, 0f));
            foreach ((Transform arm, float x, float sign) in new[] { (armL, -0.44f, -1f), (armR, 0.44f, 1f) })
            {
                Part(arm, "Sleeve", Capsule, new Vector3(x + sign * 0.02f, 0.78f, 0f), new Vector3(0.23f, 0.22f, 0.23f), shirtMat);
                Part(arm, "Hand", Sphere, new Vector3(x + sign * 0.03f, 0.54f, 0.02f), new Vector3(0.22f, 0.22f, 0.22f), skinMat);
            }

            // pickaxe in the right hand
            Transform pick = Pivot(armR, "Pickaxe", new Vector3(0.47f, 0.54f, 0.04f));
            Part(pick, "Handle", Cylinder, new Vector3(0.47f, 0.84f, 0.04f), new Vector3(0.07f, 0.52f, 0.07f), handleMat);
            Part(pick, "HeadL", Capsule, new Vector3(0.36f, 1.33f, 0.04f), new Vector3(0.085f, 0.115f, 0.085f), steelMat, new Vector3(0f, 0f, 62f));
            Part(pick, "HeadR", Capsule, new Vector3(0.58f, 1.33f, 0.04f), new Vector3(0.085f, 0.115f, 0.085f), steelMat, new Vector3(0f, 0f, -62f));
            Part(pick, "HeadCore", Sphere, new Vector3(0.47f, 1.36f, 0.04f), new Vector3(0.15f, 0.14f, 0.14f), steelMat);
            pick.localRotation = Quaternion.Euler(8f, 0f, -6f);
            armL.localRotation = Quaternion.Euler(0f, 0f, 9f);
            armR.localRotation = Quaternion.Euler(-14f, 0f, -9f);

            // ---- head (pivot at the neck)
            Transform head = Pivot(root.transform, "Head", new Vector3(0f, 1.02f, 0f));
            Part(head, "Skull", Sphere, new Vector3(0f, 1.32f, 0f), new Vector3(0.98f, 0.88f, 0.92f), skinMat);
            Part(head, "EarL", Sphere, new Vector3(-0.47f, 1.3f, 0f), new Vector3(0.12f, 0.17f, 0.1f), skinMat);
            Part(head, "EarR", Sphere, new Vector3(0.47f, 1.3f, 0f), new Vector3(0.12f, 0.17f, 0.1f), skinMat);
            Part(head, "EyeL", Sphere, new Vector3(-0.17f, 1.34f, 0.405f), new Vector3(0.12f, 0.17f, 0.07f), eyeMat);
            Part(head, "EyeR", Sphere, new Vector3(0.17f, 1.34f, 0.405f), new Vector3(0.12f, 0.17f, 0.07f), eyeMat);
            Part(head, "ShineL", Sphere, new Vector3(-0.145f, 1.39f, 0.44f), new Vector3(0.05f, 0.06f, 0.03f), whiteMat);
            Part(head, "ShineR", Sphere, new Vector3(0.195f, 1.39f, 0.44f), new Vector3(0.05f, 0.06f, 0.03f), whiteMat);
            Part(head, "CheekL", Sphere, new Vector3(-0.31f, 1.22f, 0.35f), new Vector3(0.14f, 0.08f, 0.05f), cheekMat);
            Part(head, "CheekR", Sphere, new Vector3(0.31f, 1.22f, 0.35f), new Vector3(0.14f, 0.08f, 0.05f), cheekMat);
            Part(head, "Nose", Sphere, new Vector3(0f, 1.25f, 0.445f), new Vector3(0.11f, 0.095f, 0.09f), Mat("Char_Nose", Hex("#EFAE8C"), 0.2f));
            Part(head, "BrowL", Capsule, new Vector3(-0.17f, 1.5f, 0.37f), new Vector3(0.05f, 0.07f, 0.04f), female ? Mat("Char_BrowRed", Hex("#9E2F1A")) : Mat("Char_BrowOrange", Hex("#C9601F")), new Vector3(0f, 0f, 78f));
            Part(head, "BrowR", Capsule, new Vector3(0.17f, 1.5f, 0.37f), new Vector3(0.05f, 0.07f, 0.04f), female ? Mat("Char_BrowRed", Hex("#9E2F1A")) : Mat("Char_BrowOrange", Hex("#C9601F")), new Vector3(0f, 0f, -78f));

            Material mouthMat = Mat("Char_Mouth", Hex("#7A2E2A"), 0.2f);
            if (female)
            {
                // little smile
                Part(head, "Smile", Arc("SmileArc", 0.085f, 0.014f, 205f, 335f), new Vector3(0f, 1.2f, 0.438f), Vector3.one, mouthMat);

                BuildFemaleHair(head);
            }
            else
            {
                BuildBeard(head, mouthMat);
            }

            // ---- hard hat + head lamp
            Part(head, "HatDome", Hemisphere(), new Vector3(0f, 1.52f, 0f), new Vector3(1.14f, 1.0f, 1.1f), hatMat);
            Part(head, "HatBrim", Cylinder, new Vector3(0f, 1.52f, 0.02f), new Vector3(1.26f, 0.03f, 1.22f), hatDarkMat);
            Part(head, "HatVisor", Cylinder, new Vector3(0f, 1.525f, 0.42f), new Vector3(0.78f, 0.03f, 0.55f), hatDarkMat, new Vector3(-7f, 0f, 0f));
            Part(head, "LampHousing", Cylinder, new Vector3(0f, 1.82f, 0.5f), new Vector3(0.22f, 0.07f, 0.22f), lampMat, new Vector3(90f, 0f, 0f));
            GameObject lens = Part(head, "LampLens", Sphere, new Vector3(0f, 1.82f, 0.585f), new Vector3(0.15f, 0.15f, 0.07f), lensMat);
            var lightObject = new GameObject("LampLight");
            lightObject.transform.SetParent(head, false);
            lightObject.transform.position = new Vector3(0f, 1.82f, 0.62f);
            lightObject.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.88f, 0.55f);
            light.intensity = 6f;
            light.range = 9f;
            light.spotAngle = 70f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            var animator = root.AddComponent<MinerAnimator>();
            animator.Bind(body, head, armL, armR, legL, legR, lens.GetComponent<Renderer>(), light);

            root.transform.position = Vector3.zero;
            return root;
        }

        private static void BuildBeard(Transform head, Material mouthMat)
        {
            Material beard = Mat("Char_Beard", Hex("#F28C28"), 0.15f);
            Material beardLight = Mat("Char_BeardLight", Hex("#FFB04D"), 0.15f);
            Part(head, "Beard", Sphere, new Vector3(0f, 0.99f, 0.16f), new Vector3(0.92f, 0.52f, 0.62f), beard);
            Part(head, "BeardCheekL", Sphere, new Vector3(-0.31f, 1.08f, 0.2f), new Vector3(0.32f, 0.36f, 0.3f), beard);
            Part(head, "BeardCheekR", Sphere, new Vector3(0.31f, 1.08f, 0.2f), new Vector3(0.32f, 0.36f, 0.3f), beard);
            Part(head, "BeardChin", Sphere, new Vector3(0f, 0.84f, 0.2f), new Vector3(0.5f, 0.38f, 0.38f), beard);
            Part(head, "BeardTip", Sphere, new Vector3(0f, 0.7f, 0.2f), new Vector3(0.3f, 0.3f, 0.28f), beardLight);
            Part(head, "Mouth", Sphere, new Vector3(0f, 1.075f, 0.46f), new Vector3(0.15f, 0.07f, 0.05f), mouthMat);
            Part(head, "MustacheL", Capsule, new Vector3(-0.115f, 1.15f, 0.43f), new Vector3(0.085f, 0.17f, 0.085f), beardLight, new Vector3(0f, 0f, 74f));
            Part(head, "MustacheR", Capsule, new Vector3(0.115f, 1.15f, 0.43f), new Vector3(0.085f, 0.17f, 0.085f), beardLight, new Vector3(0f, 0f, -74f));
        }

        private static void BuildFemaleHair(Transform head)
        {
            Material hair = Mat("Char_HairRed", Hex("#C73B23"), 0.35f);
            Material hairLight = Mat("Char_HairRedLight", Hex("#E1522F"), 0.4f);
            Material ribbon = Mat("Char_Ribbon", Gold, 0.5f);
            Part(head, "HairBack", Sphere, new Vector3(0f, 1.34f, -0.06f), new Vector3(1.02f, 0.94f, 0.92f), hair);
            Part(head, "HairSideL", Sphere, new Vector3(-0.46f, 1.2f, -0.02f), new Vector3(0.16f, 0.36f, 0.26f), hair);
            Part(head, "HairSideR", Sphere, new Vector3(0.46f, 1.2f, -0.02f), new Vector3(0.16f, 0.36f, 0.26f), hair);
            Part(head, "BangL", Sphere, new Vector3(-0.22f, 1.5f, 0.38f), new Vector3(0.26f, 0.15f, 0.12f), hairLight, new Vector3(0f, 0f, 16f));
            Part(head, "BangM", Sphere, new Vector3(0f, 1.52f, 0.41f), new Vector3(0.26f, 0.15f, 0.12f), hairLight);
            Part(head, "BangR", Sphere, new Vector3(0.22f, 1.5f, 0.38f), new Vector3(0.26f, 0.15f, 0.12f), hairLight, new Vector3(0f, 0f, -16f));

            // the braid hangs over her right shoulder (visible from the front): decreasing, slightly offset beads
            float[] ys = { 1.08f, 0.9f, 0.74f, 0.6f, 0.47f };
            float[] sizes = { 0.27f, 0.25f, 0.23f, 0.21f, 0.18f };
            float[] xs = { 0.5f, 0.55f, 0.5f, 0.55f, 0.5f };
            for (int i = 0; i < ys.Length; i++)
            {
                Part(head, "Braid" + i, Sphere, new Vector3(xs[i], ys[i], 0.16f), new Vector3(sizes[i], sizes[i] * 1.12f, sizes[i]), i % 2 == 0 ? hair : hairLight);
            }

            Part(head, "BraidTie", Sphere, new Vector3(0.5f, 0.36f, 0.16f), new Vector3(0.14f, 0.1f, 0.14f), ribbon);
            Part(head, "BraidTip", Sphere, new Vector3(0.5f, 0.28f, 0.16f), new Vector3(0.12f, 0.14f, 0.12f), hair);
            Part(head, "HairBow", Sphere, new Vector3(0.46f, 1.28f, 0.2f), new Vector3(0.15f, 0.1f, 0.1f), ribbon);
        }
    }
}
