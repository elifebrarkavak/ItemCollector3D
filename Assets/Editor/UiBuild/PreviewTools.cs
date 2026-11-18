using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ItemCollector3D.EditorTools
{
    /// <summary>Renders prefabs in a throw-away scene to a PNG (design review of the placeholder miners).</summary>
    public static class PreviewTools
    {
        /// <summary>Renders a UI prefab (optionally activating named children) on a canvas into a PNG. Design review only.</summary>
        public static string RenderUiPrefab(string prefabPath, string outPath, int width, int height, string[] activate)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var camGo = new GameObject("Cam");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.25f, 0.32f, 0.2f);
                cam.orthographic = true;
                var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 5f;
                var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), canvasGo.transform);
                var rt = (RectTransform)instance.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                foreach (string name in activate ?? new string[0])
                {
                    foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == name) { t.gameObject.SetActive(true); break; }
                    }
                }

                var rtex = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                cam.targetTexture = rtex;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rtex;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                rtex.Release();
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }

            return outPath;
        }

        public static string RenderPrefabs(string[] prefabPaths, string outPath, int width, int height, float distance = 5.5f, float yaw = 0f)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var camGo = new GameObject("Cam");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.17f, 0.21f, 0.3f);
                cam.fieldOfView = 30f;
                float spacing = 2.6f;
                float mid = (prefabPaths.Length - 1) * spacing * 0.5f;
                for (int i = 0; i < prefabPaths.Length; i++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]));
                    instance.transform.position = new Vector3(i * spacing - mid, 0f, 0f);
                    instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    foreach (var animator in instance.GetComponentsInChildren<ItemCollector3D.Characters.MinerAnimator>()) { animator.SetLampLightEnabled(false); }
                }

                camGo.transform.position = new Vector3(0f, 1.15f, -distance);
                camGo.transform.rotation = Quaternion.Euler(4f, 0f, 0f);

                var lightGo = new GameObject("Key");
                var key = lightGo.AddComponent<Light>();
                key.type = LightType.Directional;
                key.intensity = 1.2f;
                key.color = new Color(1f, 0.93f, 0.8f);
                lightGo.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
                var fillGo = new GameObject("Fill");
                var fill = fillGo.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = 0.4f;
                fill.color = new Color(0.6f, 0.7f, 1f);
                fillGo.transform.rotation = Quaternion.Euler(10f, 150f, 0f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.55f);

                var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                rt.Release();
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }

            return outPath;
        }
    }
}
