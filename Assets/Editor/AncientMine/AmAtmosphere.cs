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
using UnityEngine.SceneManagement;

namespace ItemCollector3D.EditorTools.AncientMine
{
    /// <summary>
    /// Stage 16D: the living layer of the Ancient Mine, added to the OPEN AncientMine scene WITHOUT touching the environment meshes, the treasures, the player or the HUD's gameplay parts: a new
    /// scene root <c>MineAtmosphere</c> (zone tracker, ambience, ONE crystal-glow manager, four small mote emitters, the grotto's sound anchor) and a small zone-name banner in the HUD.
    /// Idempotent: <see cref="Apply"/> removes the old root and the old banner and builds them again from the same data. Run it via <c>script eval</c> on the open scene, then save.
    /// It lives in its own root because the 16B budget tests count what is under AncientMineEnvironment.
    /// </summary>
    public static class AmAtmosphere
    {
        public const string RootName = "MineAtmosphere";
        public const string NoticeName = "ZoneNotice";

        private static readonly string[] ZoneRoots = { "A_AncientGate", "B_HallOfPillars", "C_CrystalCaverns", "D_ForgottenWorkshop", "E_RoyalVault" };

        public static string Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform env = null;
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                if (r.name == "AncientMineEnvironment")
                {
                    env = r.transform;
                }
            }

            if (env == null)
            {
                return "AncientMineEnvironment not found in the active scene.";
            }

            foreach (GameObject old in scene.GetRootGameObjects())
            {
                if (old.name == RootName)
                {
                    Object.DestroyImmediate(old);
                }
            }

            Material mote = AssetDatabase.LoadAssetAtPath<Material>("Assets/RockyCanyon/Materials/RC_Mote.mat");
            if (mote == null)
            {
                mote = FindMote();
            }

            var camera = Object.FindAnyObjectByType<ThirdPersonCameraController>();
            var hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            var detector = Object.FindAnyObjectByType<TreasureDetector>();

            var rootGo = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(rootGo, scene);
            Transform root = rootGo.transform;

            // ---- zone tracker, ambience (+ the grotto's sound anchor)
            var tracker = rootGo.AddComponent<MineZoneTracker>();
            Link(tracker, ("cameraController", camera));
            var anchor = new GameObject("GrottoAnchor").transform;
            anchor.SetParent(root, false);
            anchor.position = new Vector3(36f, 7.2f, 14f);
            var ambience = new GameObject("Ambience").AddComponent<MineAmbience>();
            ambience.transform.SetParent(root, false);
            Link(ambience, ("zones", tracker), ("hunt", hunt), ("detector", detector), ("grotto", anchor));

            // ---- crystal glow: one manager, one entry per area's crystal mesh (+ the gold inlay slot of the vault's and the hall's structures)
            var glow = new GameObject("CrystalGlow").AddComponent<MineCrystalGlow>();
            glow.transform.SetParent(root, false);
            var entries = new List<(Renderer r, int slot, MineZone zone, float intensity, float swing, float period, float phase, float tint, bool swell)>();
            // (zone, intensity, swing, period, phase, tint, swell): the Caverns and the Vault glow strongest and slowest; every phase differs
            var plan = new (int zone, float intensity, float swing, float period, float phase, float tint, bool swell)[]
            {
                (0, 0.9f, 0.14f, 9f, 0.4f, 0.12f, false),
                (1, 0.85f, 0.12f, 12f, 2.3f, 0.10f, false),
                (2, 1.05f, 0.34f, 8f, 4.1f, 0.38f, false),
                (3, 0.85f, 0.12f, 11f, 1.2f, 0.10f, false),
                (4, 1.1f, 0.36f, 13f, 5.3f, 0.32f, true),
            };
            for (int z = 0; z < ZoneRoots.Length; z++)
            {
                Transform zoneRoot = env.Find(ZoneRoots[z]);
                if (zoneRoot == null)
                {
                    continue;
                }

                var p = plan[z];
                foreach (MeshRenderer mr in zoneRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr.name == "Crystals")
                    {
                        entries.Add((mr, -1, (MineZone)z, p.intensity, p.swing, p.period, p.phase, p.tint, p.swell));
                    }
                    else if (mr.name == "Structures" && mr.sharedMaterials.Length > 1 && (z == 1 || z == 4 || z == 2))
                    {
                        // the gold inlay (material slot 1): a calmer, different cycle from the crystals of the same area
                        entries.Add((mr, 1, (MineZone)z, 1f, z == 4 ? 0.2f : 0.1f, p.period * 0.8f, p.phase + 1.7f, 0.05f, p.swell));
                    }
                }
            }

            var gso = new SerializedObject(glow);
            gso.FindProperty("zones").objectReferenceValue = tracker;
            gso.FindProperty("cameraController").objectReferenceValue = camera;
            SerializedProperty array = gso.FindProperty("entries");
            array.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                SerializedProperty e = array.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("renderer").objectReferenceValue = entries[i].r;
                e.FindPropertyRelative("materialIndex").intValue = entries[i].slot;
                e.FindPropertyRelative("zone").enumValueIndex = (int)entries[i].zone;
                e.FindPropertyRelative("intensity").floatValue = entries[i].intensity;
                e.FindPropertyRelative("swing").floatValue = entries[i].swing;
                e.FindPropertyRelative("period").floatValue = entries[i].period;
                e.FindPropertyRelative("phase").floatValue = entries[i].phase;
                e.FindPropertyRelative("tint").floatValue = entries[i].tint;
                e.FindPropertyRelative("vaultSwell").boolValue = entries[i].swell;
            }

            gso.ApplyModifiedPropertiesWithoutUndo();

            // ---- motes: four small places (at most 12 + 10 + 14 + 16 live specks, only near the player)
            var motes = new GameObject("Motes").transform;
            motes.SetParent(root, false);
            Motes(motes, "CavernsSparkles", new Vector3(50f, 10f, -2f), mote, tracker, camera, false, new Vector3(20f, 5f, 16f), 30f, true, MineZone.Caverns, 12, 0.9f,
                new Color(0.55f, 0.95f, 1f), new Color(0.85f, 0.7f, 1f), new Vector2(0.05f, 0.11f), new Vector2(2.5f, 4f), new Vector3(0.03f, 0.1f, 0.03f));
            Motes(motes, "GrottoMotes", new Vector3(36f, 8f, 14f), mote, tracker, camera, false, new Vector3(9f, 2.5f, 9f), 28f, true, MineZone.Caverns, 10, 1.0f,
                new Color(0.4f, 1f, 0.9f), new Color(0.7f, 0.95f, 1f), new Vector2(0.04f, 0.09f), new Vector2(3f, 5f), new Vector3(0.04f, 0.14f, 0.04f));
            Motes(motes, "WorkshopDust", new Vector3(-12f, 10f, 40f), mote, tracker, camera, true, new Vector3(14f, 3f, 14f), 30f, true, MineZone.Workshop, 14, 0.9f,
                new Color(0.86f, 0.72f, 0.5f), new Color(0.78f, 0.62f, 0.42f), new Vector2(0.04f, 0.09f), new Vector2(6f, 9f), new Vector3(0.12f, 0.03f, 0.08f));
            Motes(motes, "VaultSparks", new Vector3(10f, 5f, -38f), mote, tracker, camera, false, new Vector3(24f, 8f, 14f), 40f, true, MineZone.Vault, 16, 1.2f,
                new Color(0.85f, 0.7f, 1f), new Color(1f, 0.88f, 0.55f), new Vector2(0.05f, 0.12f), new Vector2(8f, 12f), new Vector3(0.03f, 0.22f, 0.03f));

            string notice = BuildNotice(tracker, hunt);
            EditorSceneManager.MarkSceneDirty(scene);
            return $"{RootName}: glow entries {entries.Count}, motes 4, {notice}";
        }

        private static Material FindMote()
        {
            foreach (string guid in AssetDatabase.FindAssets("RC_Mote t:Material"))
            {
                return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            }

            return null;
        }

        private static void Motes(Transform parent, string name, Vector3 position, Material material, MineZoneTracker tracker, ThirdPersonCameraController camera, bool follow, Vector3 area,
            float radius, bool onlyInZone, MineZone zone, int max, float rate, Color a, Color b, Vector2 size, Vector2 lifetime, Vector3 drift)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var m = go.AddComponent<MineMotes>();
            var so = new SerializedObject(m);
            so.FindProperty("material").objectReferenceValue = material;
            so.FindProperty("zones").objectReferenceValue = tracker;
            so.FindProperty("cameraController").objectReferenceValue = camera;
            so.FindProperty("followPlayer").boolValue = follow;
            so.FindProperty("area").vector3Value = area;
            so.FindProperty("activeRadius").floatValue = radius;
            so.FindProperty("onlyInZone").boolValue = onlyInZone;
            so.FindProperty("zone").enumValueIndex = (int)zone;
            so.FindProperty("maxParticles").intValue = max;
            so.FindProperty("rate").floatValue = rate;
            so.FindProperty("colorA").colorValue = a;
            so.FindProperty("colorB").colorValue = b;
            so.FindProperty("size").vector2Value = size;
            so.FindProperty("lifetime").vector2Value = lifetime;
            so.FindProperty("drift").vector3Value = drift;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================ the zone-name banner

        /// <summary>A second copy of the progress banner, smaller, placed under the HUD's top bar (same method and size as Rocky Canyon's).</summary>
        private static string BuildNotice(MineZoneTracker tracker, TreasureHuntController hunt)
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
            holder.transform.SetSiblingIndex(progress.transform.GetSiblingIndex() + 1);

            GameObject copy = Object.Instantiate(progressBanner.gameObject, holderRect, false);
            copy.name = "Banner";
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -214f);
            rect.sizeDelta = new Vector2(820f, 88f); // "FORGOTTEN WORKSHOP" is the longest name

            var banner = copy.GetComponent<NotificationBanner>();
            var bannerSo = new SerializedObject(banner);
            bannerSo.FindProperty("holdDuration").floatValue = 1.75f;
            bannerSo.ApplyModifiedPropertiesWithoutUndo();

            var title = (RectTransform)copy.transform.Find("Title");
            title.anchorMin = Vector2.zero;
            title.anchorMax = Vector2.one;
            title.pivot = new Vector2(0.5f, 0.5f);
            title.offsetMin = new Vector2(16f, 6f);
            title.offsetMax = new Vector2(-16f, -6f);
            var titleText = title.GetComponent<TMPro.TMP_Text>();
            titleText.fontSize = 46f;
            titleText.alignment = TMPro.TextAlignmentOptions.Center;
            titleText.characterSpacing = 6f;
            titleText.text = "ANCIENT GATE";
            copy.transform.Find("Subtitle").gameObject.SetActive(false);

            var notice = holder.AddComponent<ZoneEntryNotice>();
            var so = new SerializedObject(notice);
            so.FindProperty("mineZones").objectReferenceValue = tracker;
            so.FindProperty("banner").objectReferenceValue = banner;
            so.FindProperty("progressBanner").objectReferenceValue = progressBanner;
            so.FindProperty("hunt").objectReferenceValue = hunt;
            so.FindProperty("color").colorValue = new Color(0.82f, 0.93f, 1f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return "zone notice: " + NoticeName;
        }

        private static void Link(Object component, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(component);
            foreach ((string field, Object value) in fields)
            {
                SerializedProperty p = so.FindProperty(field);
                if (p == null)
                {
                    Debug.LogWarning($"AmAtmosphere: {component.GetType().Name} has no field {field}");
                    continue;
                }

                p.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
