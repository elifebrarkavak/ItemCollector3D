#if UNITY_EDITOR
using System.Collections.Generic;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using ItemCollector3D.Treasure;
using ItemCollector3D.UI;
using ItemCollector3D.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ItemCollector3D.EditorTools.RockyCanyon
{
    /// <summary>
    /// Stage 15D: the living layer of Rocky Canyon, added to the OPEN RockyCanyon scene WITHOUT touching the 15B environment, the treasures, the player or the HUD's gameplay parts:
    /// a new scene root <c>CanyonAtmosphere</c> (zone tracker, ambience, swaying shrubs / grass tufts / bridge streamers, two small dust emitters, the ravine's vein shimmer) and a small zone-name banner
    /// in the HUD. It is idempotent: <see cref="Apply"/> removes the old root and the old banner and builds them again from the same data. The environment's own budget (three materials, merged static
    /// meshes) is unchanged because the swaying props are separate objects under the new root. Run it via <c>script eval</c> on the open scene, then save.
    /// </summary>
    public static class RcAtmosphere
    {
        public const string RootName = "CanyonAtmosphere";
        public const string NoticeName = "ZoneNotice";

        private static readonly Vector3 BridgeCentre = new(3f, 5f, 18f);
        private static readonly float BridgeYaw = Mathf.Atan2(18f, 2f) * Mathf.Rad2Deg;
        private const float TowerHeight = 8.4f;

        private static Vector3 BridgeWorld(float lx, float y, float lz) => BridgeCentre + Quaternion.Euler(0f, BridgeYaw, 0f) * new Vector3(lx, y, lz);

        public static string Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform env = RcDressing.Environment(scene);
            if (env == null)
            {
                return "RockyCanyonEnvironment not found in the active scene.";
            }

            RcLayout.ResetGrid();
            foreach (GameObject old in scene.GetRootGameObjects())
            {
                if (old.name == RootName)
                {
                    Object.DestroyImmediate(old);
                }
            }

            Material mote = Mote();
            Material palette = AssetDatabase.LoadAssetAtPath<Material>(RcAssets.MaterialFolder + "/RC_Palette.mat");
            var camera = Object.FindAnyObjectByType<ThirdPersonCameraController>();
            var hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            var detector = Object.FindAnyObjectByType<TreasureDetector>();

            var rootGo = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(rootGo, scene);
            Transform root = rootGo.transform;

            // ---- zones + ambience
            var zonesGo = new GameObject("Zones");
            zonesGo.transform.SetParent(root, false);
            var tracker = zonesGo.AddComponent<CanyonZoneTracker>();
            FillTracker(tracker, camera);

            var mood = zonesGo.AddComponent<CanyonZoneMood>();
            Link(mood, ("zones", tracker));

            var ambienceGo = new GameObject("Ambience");
            ambienceGo.transform.SetParent(root, false);
            var ambience = ambienceGo.AddComponent<CanyonAmbience>();
            Link(ambience, ("zones", tracker), ("hunt", hunt), ("detector", detector));

            // ---- swaying props
            var motion = new GameObject("Motion");
            motion.transform.SetParent(root, false);
            var sway = motion.AddComponent<AmbientSway>();
            var items = new List<(Transform t, float degrees, float period, float phase, Vector3 axis)>();
            Mesh shrub = ShrubMesh(), tuft = TuftMesh(), streamers = StreamerMesh();
            string scatter = Scatter(motion.transform, palette, shrub, tuft, items);
            AddStreamers(motion.transform, palette, streamers, items);
            SetSwayItems(sway, items);

            // ---- dust: one box of air round the player, one lane of wind across the bridge
            var dustGo = new GameObject("Dust_Player");
            dustGo.transform.SetParent(root, false);
            var dust = dustGo.AddComponent<AmbientDust>();
            Link(dust, ("material", mote), ("zones", tracker), ("cameraController", camera));
            Set(dust, so =>
            {
                so.FindProperty("followPlayer").boolValue = true;
                so.FindProperty("area").vector3Value = new Vector3(18f, 3.5f, 18f);
            });

            var bridgeDustGo = new GameObject("Dust_Bridge");
            bridgeDustGo.transform.SetParent(root, false);
            bridgeDustGo.transform.SetPositionAndRotation(BridgeWorld(0f, 1.0f, 0f), Quaternion.Euler(0f, BridgeYaw, 0f));
            var bridgeDust = bridgeDustGo.AddComponent<AmbientDust>();
            Link(bridgeDust, ("material", mote), ("zones", tracker), ("cameraController", camera));
            Vector3 acrossTheDeck = Quaternion.Euler(0f, BridgeYaw, 0f) * new Vector3(1.7f, 0.08f, 0.25f);
            Set(bridgeDust, so =>
            {
                so.FindProperty("followPlayer").boolValue = false;
                so.FindProperty("area").vector3Value = new Vector3(4.2f, 1.6f, 15f);
                so.FindProperty("activeRadius").floatValue = 38f;
                so.FindProperty("wind").vector3Value = acrossTheDeck;
                so.FindProperty("gustiness").floatValue = 0.8f;
                so.FindProperty("lifetime").vector2Value = new Vector2(3f, 5f);
                SerializedProperty rates = so.FindProperty("zoneRates");
                rates.arraySize = 1;
                rates.GetArrayElementAtIndex(0).floatValue = 3.2f;
            });

            // ---- the ravine's gold veins
            string veinReport = "ravine veins: not found";
            Transform veinTransform = env.Find("E_HiddenRavine/Props/Veins");
            if (veinTransform != null)
            {
                var shimmerGo = new GameObject("Veins");
                shimmerGo.transform.SetParent(root, false);
                var shimmer = shimmerGo.AddComponent<GoldVeinShimmer>();
                Link(shimmer, ("veins", veinTransform.GetComponent<MeshRenderer>()), ("veinMesh", SpecMesh(veinTransform.GetComponent<MeshFilter>().sharedMesh, 1)), ("specMaterial", mote), ("cameraController", camera));
                veinReport = "ravine veins: shimmer wired";
            }

            string notice = BuildNotice(tracker, hunt);
            TuneTreasureEffects();
            EditorSceneManager.MarkSceneDirty(scene);
            return $"{RootName}: tracker pieces {tracker.PieceCount}, {scatter}, {veinReport}, {notice}";
        }

        // ================================================================ data

        /// <summary>Copies the walkable floors of <see cref="RcLayout"/> (plus the bridge deck, which is not a terrain floor) into the tracker.</summary>
        private static void FillTracker(CanyonZoneTracker tracker, ThirdPersonCameraController camera)
        {
            var pieces = new List<(CanyonZone zone, Vector3[] points, float radius)>();
            foreach (RcLayout.Shape shape in RcLayout.Shapes)
            {
                pieces.Add((ToZone(shape.Zone), shape.Points, shape.Radius));
            }

            // the deck: from the west abutment (-6, 5, 17) to the east one (12, 5, 19), as wide as the corridor
            pieces.Add((CanyonZone.Railway, new[] { new Vector3(-6f, 5f, 17f), new Vector3(12f, 5f, 19f) }, 4.5f));

            var so = new SerializedObject(tracker);
            SerializedProperty array = so.FindProperty("pieces");
            array.arraySize = pieces.Count;
            for (int i = 0; i < pieces.Count; i++)
            {
                SerializedProperty e = array.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("zone").enumValueIndex = (int)pieces[i].zone;
                e.FindPropertyRelative("radius").floatValue = pieces[i].radius;
                SerializedProperty points = e.FindPropertyRelative("points");
                points.arraySize = pieces[i].points.Length;
                for (int k = 0; k < pieces[i].points.Length; k++)
                {
                    points.GetArrayElementAtIndex(k).vector3Value = pieces[i].points[k];
                }
            }

            so.FindProperty("cameraController").objectReferenceValue = camera;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static CanyonZone ToZone(RcZone zone) => zone switch
        {
            RcZone.Outpost => CanyonZone.Outpost,
            RcZone.Riverbed => CanyonZone.Riverbed,
            RcZone.Railway => CanyonZone.Railway,
            RcZone.Overlook => CanyonZone.Overlook,
            _ => CanyonZone.Ravine,
        };

        // ================================================================ materials and meshes

        /// <summary>The one material of every dust speck and vein twinkle: opaque, unlit, white (the particles carry their own colour), so there is no transparency and no overdraw.</summary>
        public static Material Mote()
        {
            string path = RcAssets.MaterialFolder + "/RC_Mote.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "RC_Mote" };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 0f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>A copy of one submesh of the vein mesh as a mesh with ONE submesh: a particle system can only emit from a mesh whose submeshes share one primitive type, and the vein mesh starts with an empty one.</summary>
        private static Mesh SpecMesh(Mesh source, int submesh)
        {
            string path = RcAssets.DressingFolder + "/RC_Vein_Specs.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "RC_Vein_Specs" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            mesh.indexFormat = source.indexFormat;
            mesh.SetVertices(source.vertices);
            mesh.SetNormals(source.normals);
            mesh.SetTriangles(source.GetTriangles(submesh), 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>A dry shrub (about 0.8 m high and 1 m wide): grey-brown stems and three small olive-brown lumps.</summary>
        private static Mesh ShrubMesh()
        {
            var rm = new RcMesh();
            var random = new System.Random(15041);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            for (int k = 0; k < 10; k++)
            {
                float a = k / 10f * Mathf.PI * 2f + R(-0.2f, 0.2f);
                float reach = R(0.22f, 0.5f), height = R(0.4f, 0.8f);
                rm.Beam(new Vector3(0f, 0.04f, 0f), new Vector3(Mathf.Cos(a) * reach, height, Mathf.Sin(a) * reach), 0.04f, 0.04f, k % 3 == 0 ? P.WoodBleach : P.WoodGrey);
            }

            for (int k = 0; k < 4; k++)
            {
                float a = k / 4f * Mathf.PI * 2f + 0.4f;
                float sc = R(0.8f, 1.1f);
                rm.Rock(new Vector3(Mathf.Cos(a) * 0.2f, 0.32f * sc + 0.08f, Mathf.Sin(a) * 0.2f), new Vector3(0.28f * sc, 0.22f * sc, 0.26f * sc), random.Next(1, 99999), P.Shrub, P.ShrubDark, P.Shrub, null, 0, 0.3f, 0.1f);
            }

            return rm.ToMesh("RC_Sway_Shrub");
        }

        /// <summary>A tuft of dry grass: nine thin leaning blades, pivot at the ground.</summary>
        private static Mesh TuftMesh()
        {
            var rm = new RcMesh();
            var random = new System.Random(15042);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * 360f + R(-15f, 15f);
                rm.Push(new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.06f, 0f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.06f), -a + 90f, R(10f, 26f), 0f);
                rm.Poly(Vector3.zero, 0.05f, 0f, R(0.5f, 0.85f), 3, i % 2 == 0 ? P.Grass : P.GrassDark);
                rm.Pop();
            }

            return rm.ToMesh("RC_Sway_Tuft");
        }

        /// <summary>Two torn cloth streamers (red and teal), hung from their top edge, double-sided; pivot at the top.</summary>
        private static Mesh StreamerMesh()
        {
            var rm = new RcMesh();
            void Strip(float x, float length, float width, P colour)
            {
                var a = new Vector3(x - width * 0.5f, 0f, 0f);
                var b = new Vector3(x + width * 0.5f, 0f, 0f);
                var c = new Vector3(x + width * 0.3f, -length, 0f);
                var d = new Vector3(x - width * 0.45f, -length * 0.86f, 0f);
                var e = new Vector3(x - width * 0.05f, -length * 0.93f, 0f);
                rm.Face(colour, new Vector3(x, -length * 0.5f, -1f), a, b, c, e, d);
                rm.Face(colour, new Vector3(x, -length * 0.5f, 1f), a, b, c, e, d);
            }

            Strip(-0.2f, 1.7f, 0.3f, P.ClothRed);
            Strip(0.2f, 1.4f, 0.28f, P.ClothTeal);
            rm.Beam(new Vector3(-0.4f, 0.02f, 0f), new Vector3(0.4f, 0.02f, 0f), 0.06f, 0.06f, P.WoodDark);
            return rm.ToMesh("RC_Sway_Streamers");
        }

        // ================================================================ placing the swaying props

        private static string Scatter(Transform parent, Material palette, Mesh shrub, Mesh tuft, List<(Transform, float, float, float, Vector3)> items)
        {
            var treasures = new List<Vector3>();
            foreach (TreasureSpot spot in Object.FindObjectsByType<TreasureSpot>(FindObjectsInactive.Include))
            {
                treasures.Add(spot.transform.position);
            }

            Physics.SyncTransforms();
            var random = new System.Random(15043);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            // zone, shrubs, tufts, edge band (m inside the floor's edge): the props stand beside the paths, not on the centre line
            (RcZone zone, int shrubs, int tufts, float minEdge, float maxEdge)[] plan =
            {
                (RcZone.Outpost, 3, 3, -9.5f, -2.0f),
                (RcZone.Riverbed, 4, 4, -5.5f, -1.2f),
                (RcZone.Railway, 2, 3, -3.5f, -1.2f),
                (RcZone.Overlook, 2, 3, -3.0f, -1.2f),
                (RcZone.Ravine, 1, 2, -3.2f, -1.2f),
            };
            var placed = new List<Vector2>();
            int count = 0;
            foreach (var (zone, shrubs, tufts, minEdge, maxEdge) in plan)
            {
                for (int kind = 0; kind < 2; kind++)
                {
                    int wanted = kind == 0 ? shrubs : tufts;
                    int made = 0;
                    for (int attempt = 0; attempt < 4000 && made < wanted; attempt++)
                    {
                        float x = R(-RcLayout.HalfX + 8f, RcLayout.HalfX - 8f), z = R(-RcLayout.HalfZ + 8f, RcLayout.HalfZ - 8f);
                        if (RcLayout.ZoneAt(x, z) != zone)
                        {
                            continue;
                        }

                        float edge = RcLayout.EdgeDistance(x, z);
                        if (edge < minEdge || edge > maxEdge || RcLayout.Slope(x, z) > 0.22f || (x > -10f && x < 16f && z > 9f && zone == RcZone.Railway))
                        {
                            continue;
                        }

                        bool clash = false;
                        foreach (Vector3 t in treasures)
                        {
                            clash |= (new Vector2(t.x, t.z) - new Vector2(x, z)).sqrMagnitude < 3.0f * 3.0f;
                        }

                        foreach (Vector2 p in placed)
                        {
                            clash |= (p - new Vector2(x, z)).sqrMagnitude < 4.5f * 4.5f;
                        }

                        float y = RcLayout.Surface(x, z);
                        if (clash || TouchesSomethingSolid(new Vector3(x, y + 0.9f, z), 0.9f))
                        {
                            continue;
                        }

                        placed.Add(new Vector2(x, z));
                        var go = new GameObject((kind == 0 ? "Shrub_" : "Tuft_") + zone + "_" + made);
                        go.transform.SetParent(parent, false);
                        go.transform.SetPositionAndRotation(new Vector3(x, y - 0.03f, z), Quaternion.Euler(0f, R(0f, 360f), 0f));
                        float scale = kind == 0 ? R(0.9f, 1.35f) : R(0.9f, 1.5f);
                        go.transform.localScale = Vector3.one * scale;
                        go.AddComponent<MeshFilter>().sharedMesh = kind == 0 ? shrub : tuft;
                        var renderer = go.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = palette;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        renderer.receiveShadows = true;
                        // shrubs rock very gently, grass sways a little more; the tilt axis is the prop's own horizontal axis
                        items.Add((go.transform, kind == 0 ? R(1.2f, 2.0f) : R(2.2f, 3.6f), kind == 0 ? R(4.2f, 6.0f) : R(2.6f, 3.8f), R(0f, 6.28f), Vector3.forward));
                        made++;
                        count++;
                    }
                }
            }

            return count + " shrubs / tufts";
        }

        /// <summary>True when a collider other than the terrain is within <paramref name="radius"/> of the point (a prop, a rock, the player): nothing is planted into those.</summary>
        private static bool TouchesSomethingSolid(Vector3 point, float radius)
        {
            foreach (Collider c in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!(c is MeshCollider))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddStreamers(Transform parent, Material palette, Mesh streamers, List<(Transform, float, float, float, Vector3)> items)
        {
            int n = 0;
            foreach (float lz in new[] { -6.2f, 6.2f })
            {
                foreach (float lx in new[] { -3.0f, 3.0f })
                {
                    var go = new GameObject($"Streamers_{(lz < 0 ? "W" : "E")}{(lx < 0 ? "L" : "R")}");
                    go.transform.SetParent(parent, false);
                    go.transform.SetPositionAndRotation(BridgeWorld(lx, TowerHeight - 0.32f, lz + (lz < 0 ? -0.34f : 0.34f)), Quaternion.Euler(0f, BridgeYaw + (lx < 0 ? 18f : -18f), 0f));
                    go.AddComponent<MeshFilter>().sharedMesh = streamers;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = palette;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    items.Add((go.transform, 14f, 1.5f + 0.2f * n, 0.9f * n, Vector3.forward));
                    n++;
                }
            }
        }

        private static void SetSwayItems(AmbientSway sway, List<(Transform t, float degrees, float period, float phase, Vector3 axis)> items)
        {
            var so = new SerializedObject(sway);
            SerializedProperty array = so.FindProperty("items");
            array.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                SerializedProperty e = array.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("target").objectReferenceValue = items[i].t;
                e.FindPropertyRelative("degrees").floatValue = items[i].degrees;
                e.FindPropertyRelative("period").floatValue = items[i].period;
                e.FindPropertyRelative("phase").floatValue = items[i].phase;
                e.FindPropertyRelative("axis").vector3Value = items[i].axis;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Rocky Canyon's own copy of the collect burst gets bigger sparks (0.14 -> 0.2 m): gold specks are easy to lose against the pale sand. Green Mine's scene is not touched.</summary>
        private static void TuneTreasureEffects()
        {
            var burst = Object.FindAnyObjectByType<CollectBurstEffect>();
            if (burst == null)
            {
                return;
            }

            var so = new SerializedObject(burst);
            so.FindProperty("particleSize").floatValue = 0.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================ the zone-name banner

        /// <summary>A second copy of the progress banner, smaller, placed under the HUD's top bar (centre column, clear of the level plate, the gold panel and the detector).</summary>
        private static string BuildNotice(CanyonZoneTracker tracker, TreasureHuntController hunt)
        {
            var progress = Object.FindAnyObjectByType<ProgressNotificationPresenter>(FindObjectsInactive.Include);
            if (progress == null)
            {
                return "zone notice: ProgressNotification not found";
            }

            Transform safeArea = progress.transform.parent;
            Transform old = safeArea.Find(NoticeName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var progressBanner = progress.GetComponentInChildren<NotificationBanner>(true);
            var holder = new GameObject(NoticeName, typeof(RectTransform));
            holder.layer = progress.gameObject.layer;
            var holderRect = (RectTransform)holder.transform;
            holderRect.SetParent(safeArea, false);
            holderRect.anchorMin = Vector2.zero;
            holderRect.anchorMax = Vector2.one;
            holderRect.offsetMin = Vector2.zero;
            holderRect.offsetMax = Vector2.zero;
            holder.transform.SetSiblingIndex(progress.transform.GetSiblingIndex() + 1); // just after the progress banner, below the pause menu

            GameObject copy = Object.Instantiate(progressBanner.gameObject, holderRect, false);
            copy.name = "Banner";
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -214f);
            rect.sizeDelta = new Vector2(720f, 88f);

            var banner = copy.GetComponent<NotificationBanner>();
            var bannerSo = new SerializedObject(banner);
            bannerSo.FindProperty("holdDuration").floatValue = 1.75f; // pop 0.25 + hold 1.75 + fade 0.4 = 2.4 s
            bannerSo.ApplyModifiedPropertiesWithoutUndo();

            // one line, centred in the box; the subtitle is not used
            var title = (RectTransform)copy.transform.Find("Title");
            title.anchorMin = Vector2.zero;
            title.anchorMax = Vector2.one;
            title.pivot = new Vector2(0.5f, 0.5f);
            title.offsetMin = new Vector2(16f, 6f);
            title.offsetMax = new Vector2(-16f, -6f);
            var titleText = title.GetComponent<TMPro.TMP_Text>();
            titleText.fontSize = 50f;
            titleText.alignment = TMPro.TextAlignmentOptions.Center;
            titleText.characterSpacing = 6f;
            titleText.text = "DESERT OUTPOST";
            copy.transform.Find("Subtitle").gameObject.SetActive(false);

            var notice = holder.AddComponent<ZoneEntryNotice>();
            Link(notice, ("zones", tracker), ("banner", (Object)banner), ("progressBanner", (Object)progressBanner), ("hunt", hunt));
            return "zone notice: " + NoticeName;
        }

        // ================================================================ small serialisation helpers

        private static void Link(Object component, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(component);
            foreach ((string field, Object value) in fields)
            {
                SerializedProperty p = so.FindProperty(field);
                if (p == null)
                {
                    Debug.LogWarning($"RcAtmosphere: {component.GetType().Name} has no field {field}");
                    continue;
                }

                p.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object component, System.Action<SerializedObject> edit)
        {
            var so = new SerializedObject(component);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
