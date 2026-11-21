using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ItemCollector3D.Player;
using ItemCollector3D.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ItemCollector3D.Tests.PlayMode
{
    public sealed class PlayerSystemsPlayModeTests
    {
        private const string PlayerTestScenePath = "Assets/Scenes/Tests/PlayerTest.unity";

        private readonly List<GameObject> spawnedRoots = new();
        private readonly List<InputDevice> addedDevices = new();
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
        private Scene loadedTestScene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // The Input System routes pointer (Mouse/Touchscreen) and keyboard input to the Editor instead of the
            // game whenever the Game View is not focused, and it never raises wasPressedThisFrame for touches
            // injected in that state. Without this override the touch/mouse tests pass or fail depending on
            // whether the Editor window happens to have OS focus while the suite runs.
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (loadedTestScene.IsValid() && loadedTestScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loadedTestScene);
            }

            loadedTestScene = default;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;

            foreach (InputDevice device in addedDevices)
            {
                if (device != null && device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }

            foreach (GameObject root in spawnedRoots)
            {
                if (root != null)
                {
                    Object.Destroy(root);
                }
            }

            addedDevices.Clear();
            spawnedRoots.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MouseOrbit_RotatesOnRightDrag_AndStopsAfterRelease()
        {
            ThirdPersonCameraController controller = CreateCameraRig(out _, out Transform cameraTransform);
            Mouse mouse = AddDevice<Mouse>("Automated Test Mouse");
            mouse.MakeCurrent();

            yield return null;
            Vector3 before = cameraTransform.eulerAngles;

            MouseState heldState = new MouseState
            {
                position = new Vector2(600f, 400f),
                delta = new Vector2(120f, -80f)
            }.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(mouse, heldState);
            InputSystem.Update();
            yield return null;

            Vector3 rotated = cameraTransform.eulerAngles;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(before.y, rotated.y)), Is.GreaterThan(1f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(before.x, rotated.x)), Is.GreaterThan(1f));

            MouseState releasedState = new()
            {
                position = new Vector2(720f, 500f),
                delta = new Vector2(120f, 100f)
            };
            InputSystem.QueueStateEvent(mouse, releasedState);
            InputSystem.Update();
            yield return null;

            Vector3 afterRelease = cameraTransform.eulerAngles;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(rotated.y, afterRelease.y)), Is.LessThan(0.05f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(rotated.x, afterRelease.x)), Is.LessThan(0.05f));
            Assert.That(controller.Target, Is.Not.Null);
        }

        [UnityTest]
        [RealisticFrameRate] // unpaced frames (2-4 ms) let the CharacterController resolve against the not-yet-synced test ground: a 1.08 m Y jump, see Docs/KnownIssues.md
        public IEnumerator TwoTouches_JoystickMovesPlayer_WhileRightSwipeRotatesCamera()
        {
            CreateGroundedPlayer(out ThirdPersonPlayerController player, out Transform playerTransform);
            ThirdPersonCameraController cameraController = CreateCameraRig(playerTransform, out Transform cameraTransform);
            CreateEventSystem();
            VirtualJoystick joystick = CreateJoystick(player, out RectTransform joystickRect);
            Touchscreen touchscreen = AddDevice<Touchscreen>("Automated Test Touchscreen");
            touchscreen.MakeCurrent();

            Canvas.ForceUpdateCanvases();
            yield return null;

            Vector2 joystickCenter = RectTransformUtility.WorldToScreenPoint(null, joystickRect.position);
            PointerEventData joystickPointer = new(EventSystem.current)
            {
                pointerId = 11,
                position = joystickCenter
            };

            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 11,
                phase = UnityEngine.InputSystem.TouchPhase.Began,
                position = joystickCenter,
                pressure = 1f
            });
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 22,
                phase = UnityEngine.InputSystem.TouchPhase.Began,
                position = new Vector2(Screen.width * 0.75f, Screen.height * 0.5f),
                pressure = 1f
            });
            InputSystem.Update();
            joystick.OnPointerDown(joystickPointer);
            InvokePrivate(cameraController, "ReadOrbitInput");
            Assert.That(ActiveTouchCount(touchscreen), Is.EqualTo(2));

            Vector3 playerBefore = playerTransform.position;
            float yawBefore = cameraTransform.eulerAngles.y;

            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 11,
                phase = UnityEngine.InputSystem.TouchPhase.Moved,
                position = joystickCenter + Vector2.right * 80f,
                delta = Vector2.right * 80f,
                pressure = 1f
            });
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 22,
                phase = UnityEngine.InputSystem.TouchPhase.Moved,
                position = new Vector2(Screen.width * 0.75f + 160f, Screen.height * 0.5f),
                delta = new Vector2(160f, 0f),
                pressure = 1f
            });
            InputSystem.Update();
            joystickPointer.position = joystickCenter + Vector2.right * 80f;
            joystick.OnDrag(joystickPointer);
            InvokePrivate(cameraController, "ReadOrbitInput");

            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            Assert.That(Vector3.Distance(playerBefore, playerTransform.position), Is.GreaterThan(0.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yawBefore, cameraTransform.eulerAngles.y)), Is.GreaterThan(1f));

            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 11,
                phase = UnityEngine.InputSystem.TouchPhase.Ended,
                position = joystickCenter + Vector2.right * 80f
            });
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 22,
                phase = UnityEngine.InputSystem.TouchPhase.Ended,
                position = new Vector2(Screen.width * 0.75f + 160f, Screen.height * 0.5f)
            });
            InputSystem.Update();
            joystick.OnPointerUp(joystickPointer);
            InvokePrivate(cameraController, "ReadOrbitInput");
            Assert.That(ActiveTouchCount(touchscreen), Is.Zero);

            Vector3 releasedPosition = playerTransform.position;
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            Assert.That(joystick.Value, Is.EqualTo(Vector2.zero));
            Assert.That(Vector3.Distance(releasedPosition, playerTransform.position), Is.LessThan(0.05f));
        }

        [UnityTest]
        public IEnumerator UiRaycasts_SeparateJoystickButtonsAndSwipeArea()
        {
            EventSystem eventSystem = CreateEventSystem();
            Canvas canvas = CreateCanvas(out RectTransform canvasRect);

            RectTransform joystick = CreateRaycastImage(
                "MovementJoystick",
                canvasRect,
                Vector2.zero,
                new Vector2(0.5f, 0.5f),
                new Vector2(220f, 220f),
                new Vector2(150f, 150f));
            RectTransform dig = CreateRaycastImage(
                "DigButton",
                canvasRect,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                new Vector2(180f, 90f),
                new Vector2(-310f, -100f));
            RectTransform collect = CreateRaycastImage(
                "CollectButton",
                canvasRect,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                new Vector2(180f, 90f),
                new Vector2(-100f, -100f));

            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(RaycastNames(eventSystem, ScreenPoint(joystick)), Does.Contain("MovementJoystick"));
            Assert.That(RaycastNames(eventSystem, ScreenPoint(dig)), Does.Contain("DigButton"));
            Assert.That(RaycastNames(eventSystem, ScreenPoint(collect)), Does.Contain("CollectButton"));

            Vector2 blankSwipePoint = new(Screen.width * 0.75f, Screen.height * 0.5f);
            Assert.That(RaycastNames(eventSystem, blankSwipePoint), Is.Empty);
            Assert.That(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator CanvasScalerConfigurationAndSafeArea_ValidateVirtualResolutionsAndCurrentDisplay()
        {
            Canvas canvas = CreateCanvas(out RectTransform canvasRect);
            UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();

            GameObject safeAreaObject = Track(new GameObject("SafeArea", typeof(RectTransform)));
            safeAreaObject.transform.SetParent(canvasRect, false);
            RectTransform safeAreaRect = safeAreaObject.GetComponent<RectTransform>();
            Stretch(safeAreaRect);
            safeAreaObject.AddComponent<SafeAreaFitter>();

            yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.That(scaler.uiScaleMode, Is.EqualTo(UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize));
            AssertVector2(scaler.referenceResolution, new Vector2(1920f, 1080f), 0.001f);
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(canvas.renderingDisplaySize.x, Is.GreaterThan(0f));
            Assert.That(canvas.renderingDisplaySize.y, Is.GreaterThan(0f));

            Assert.That(CalculateCanvasScale(new Vector2(1280f, 720f), new Vector2(1920f, 1080f), 0.5f),
                Is.EqualTo(2f / 3f).Within(0.001f));
            Assert.That(CalculateCanvasScale(new Vector2(720f, 1280f), new Vector2(1920f, 1080f), 0.5f),
                Is.EqualTo(2f / 3f).Within(0.001f));
            Assert.That(CalculateCanvasScale(new Vector2(2560f, 1440f), new Vector2(1920f, 1080f), 0.5f),
                Is.EqualTo(4f / 3f).Within(0.001f));

            Rect safeArea = Screen.safeArea;
            Vector2 expectedMin = safeArea.position / new Vector2(Screen.width, Screen.height);
            Vector2 expectedMax = (safeArea.position + safeArea.size) / new Vector2(Screen.width, Screen.height);
            AssertVector2(safeAreaRect.anchorMin, expectedMin, 0.001f);
            AssertVector2(safeAreaRect.anchorMax, expectedMax, 0.001f);
            Assert.That(safeAreaRect.rect.width, Is.GreaterThan(0f));
            Assert.That(safeAreaRect.rect.height, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator CameraCollision_RetractsAndRestoresWithoutPersistentObjects()
        {
            ThirdPersonCameraController controller = CreateCameraRig(out Transform target, out Transform cameraTransform);
            Vector3 pivot = target.position + Vector3.up * 1.4f;
            Quaternion initialRotation = Quaternion.Euler(20f, 0f, 0f);
            cameraTransform.SetPositionAndRotation(pivot + initialRotation * Vector3.back * 5f, initialRotation);
            yield return null;

            float clearDistance = Vector3.Distance(pivot, cameraTransform.position);

            GameObject wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.name = "TemporaryCameraCollisionWall";
            wall.transform.SetPositionAndRotation(Vector3.Lerp(pivot, cameraTransform.position, 0.5f), cameraTransform.rotation);
            wall.transform.localScale = new Vector3(4f, 4f, 0.5f);
            yield return new WaitForSeconds(0.25f);

            float blockedDistance = Vector3.Distance(pivot, cameraTransform.position);
            Assert.That(blockedDistance, Is.LessThan(clearDistance - 1f));

            Object.Destroy(wall);
            spawnedRoots.Remove(wall);
            yield return new WaitForSeconds(0.5f);

            float restoredDistance = Vector3.Distance(pivot, cameraTransform.position);
            Assert.That(restoredDistance, Is.EqualTo(clearDistance).Within(0.1f));
            Assert.That(controller.Target, Is.EqualTo(target));
        }

        [UnityTest]
        public IEnumerator HudGoldProgressButtonsAndCallbacks_AreExternallyControllable()
        {
            GameObject root = Track(new GameObject("HUD Test Root"));
            root.SetActive(false);

            TextMeshProUGUI levelTitle = CreateChildComponent<TextMeshProUGUI>(root.transform, "LevelTitle");
            TextMeshProUGUI goldText = CreateChildComponent<TextMeshProUGUI>(root.transform, "GoldText");
            UnityEngine.UI.Image fill = CreateChildComponent<UnityEngine.UI.Image>(root.transform, "GoldFill");
            UnityEngine.UI.Button dig = CreateButton(root.transform, "DigButton");
            UnityEngine.UI.Button collect = CreateButton(root.transform, "CollectButton");
            HudController hud = root.AddComponent<HudController>();

            SetPrivateField(hud, "levelTitleText", levelTitle);
            SetPrivateField(hud, "goldText", goldText);
            SetPrivateField(hud, "goldFillImage", fill);
            SetPrivateField(hud, "digButton", dig);
            SetPrivateField(hud, "collectButton", collect);

            bool digFired = false;
            bool collectFired = false;
            hud.DigRequested += () => digFired = true;
            hud.CollectRequested += () => collectFired = true;

            root.SetActive(true);
            yield return null;

            hud.SetLevelTitle("GREEN MINE");
            hud.SetGold(250);
            hud.SetDigState(true, true);
            hud.SetCollectState(true, false);

            Assert.That(levelTitle.text, Is.EqualTo("GREEN MINE"));
            Assert.That(goldText.text, Is.EqualTo("250 / 500 GOLD"));
            Assert.That(fill.fillAmount, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(dig.gameObject.activeSelf, Is.True);
            Assert.That(dig.interactable, Is.True);
            Assert.That(collect.gameObject.activeSelf, Is.True);
            Assert.That(collect.interactable, Is.False);

            dig.onClick.Invoke();
            collect.onClick.Invoke();
            Assert.That(digFired, Is.True);
            Assert.That(collectFired, Is.True);
        }

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator PlayerTestScene_PlayerStaysOnGround_WhileWalkingWithKeyboard()
        {
#if UNITY_EDITOR
            yield return LoadPlayerTestScene();

            ThirdPersonPlayerController player = Object.FindAnyObjectByType<ThirdPersonPlayerController>();
            Assert.That(player, Is.Not.Null, "PlayerTest scene must contain the Player prefab.");
            CharacterController characterController = player.GetComponent<CharacterController>();
            GameObject ground = GameObject.Find("Ground");
            Assert.That(ground, Is.Not.Null, "PlayerTest scene must contain a Ground object.");
            Collider groundCollider = ground.GetComponent<Collider>();
            Assert.That(groundCollider, Is.Not.Null);
            Assert.That(groundCollider.enabled, Is.True);

            Keyboard keyboard = AddDevice<Keyboard>("Automated Test Keyboard");
            keyboard.MakeCurrent();
            Transform playerTransform = player.transform;

            // Settle onto the ground.
            yield return WaitSeconds(0.5f);
            float groundTop = groundCollider.bounds.max.y;
            Assert.That(characterController.isGrounded, Is.True, "Player must be grounded at rest.");
            Assert.That(playerTransform.position.y - characterController.height * 0.5f,
                Is.EqualTo(groundTop).Within(0.2f), "Player feet must rest on the ground surface.");

            // Walk forward (W) for 2 seconds; must stay on the ground.
            Vector3 start = playerTransform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return WaitSeconds(2f);
            Assert.That(playerTransform.position.z - start.z, Is.GreaterThan(5f), "W must move the player forward.");
            Assert.That(characterController.isGrounded, Is.True);
            Assert.That(playerTransform.position.y, Is.GreaterThan(groundTop - 0.5f), "Player must not sink or fall.");

            // Strafe right (D); player must turn towards +X and stay grounded.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return WaitSeconds(1.5f);
            Assert.That(playerTransform.position.x - start.x, Is.GreaterThan(3f), "D must move the player right.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(playerTransform.eulerAngles.y, 90f)), Is.LessThan(15f),
                "Player must face the movement direction.");
            Assert.That(characterController.isGrounded, Is.True);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return WaitSeconds(0.3f);
            Assert.That(playerTransform.position.y, Is.GreaterThan(groundTop - 0.5f));
#else
            Assert.Ignore("Scene based test is Editor only.");
            yield break;
#endif
        }

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator PlayerTestScene_GroundCoversLongWalk_AndCameraFollowsPlayer()
        {
#if UNITY_EDITOR
            yield return LoadPlayerTestScene();

            ThirdPersonPlayerController player = Object.FindAnyObjectByType<ThirdPersonPlayerController>();
            ThirdPersonCameraController cameraController = Object.FindAnyObjectByType<ThirdPersonCameraController>();
            Collider groundCollider = GameObject.Find("Ground").GetComponent<Collider>();
            Assert.That(player, Is.Not.Null);
            Assert.That(cameraController, Is.Not.Null);
            Assert.That(cameraController.Target, Is.EqualTo(player.transform), "Camera must target the Player instance.");

            // Regression: a 40x40 m ground let a held W key walk the player off the edge after ~4 s at 5 m/s,
            // after which it fell forever. The smoke-test ground must outlast a long continuous walk.
            const float minimumWalkSeconds = 20f;
            float walkDistance = minimumWalkSeconds * 5f;
            Bounds bounds = groundCollider.bounds;
            Assert.That(bounds.extents.z, Is.GreaterThan(walkDistance + 5f), "Ground is too small for the smoke test.");
            Assert.That(bounds.extents.x, Is.GreaterThan(walkDistance + 5f), "Ground is too small for the smoke test.");

            Transform playerTransform = player.transform;
            player.SetMoveInput(Vector2.up);
            yield return WaitSeconds(4.5f); // longer than the old 20 m / 5 m/s edge time.
            player.ClearMoveInput();

            CharacterController characterController = player.GetComponent<CharacterController>();
            Assert.That(playerTransform.position.z, Is.GreaterThan(20f), "Player must have passed the old ground edge.");
            Assert.That(characterController.isGrounded, Is.True);
            Assert.That(playerTransform.position.y, Is.GreaterThan(bounds.max.y - 0.5f));

            yield return WaitSeconds(0.5f);
            float cameraDistance = Vector3.Distance(cameraController.transform.position, playerTransform.position);
            Assert.That(cameraDistance, Is.LessThan(8f), "Camera must follow the Player.");
#else
            Assert.Ignore("Scene based test is Editor only.");
            yield break;
#endif
        }

#if UNITY_EDITOR
        private IEnumerator LoadPlayerTestScene()
        {
            AsyncOperation operation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                PlayerTestScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.That(operation, Is.Not.Null, "Could not load " + PlayerTestScenePath);
            yield return operation;
            loadedTestScene = SceneManager.GetSceneByPath(PlayerTestScenePath);
            Assert.That(loadedTestScene.isLoaded, Is.True);
            SceneManager.SetActiveScene(loadedTestScene);
            yield return null;
        }
#endif

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
            }
        }

        private ThirdPersonCameraController CreateCameraRig(out Transform target, out Transform cameraTransform)
        {
            GameObject targetObject = Track(new GameObject("Camera Target"));
            target = targetObject.transform;
            return CreateCameraRig(target, out cameraTransform);
        }

        private ThirdPersonCameraController CreateCameraRig(Transform target, out Transform cameraTransform)
        {
            GameObject cameraObject = Track(new GameObject("Test Camera", typeof(Camera)));
            cameraObject.tag = "MainCamera";
            ThirdPersonCameraController controller = cameraObject.AddComponent<ThirdPersonCameraController>();
            controller.SetTarget(target);
            cameraTransform = cameraObject.transform;
            return controller;
        }

        private void CreateGroundedPlayer(out ThirdPersonPlayerController player, out Transform playerTransform)
        {
            GameObject ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ground.name = "Test Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);

            GameObject playerObject = Track(new GameObject("Test Player", typeof(CharacterController)));
            playerObject.transform.position = Vector3.up;
            player = playerObject.AddComponent<ThirdPersonPlayerController>();
            playerTransform = playerObject.transform;
        }

        private VirtualJoystick CreateJoystick(ThirdPersonPlayerController player, out RectTransform background)
        {
            Canvas canvas = CreateCanvas(out RectTransform canvasRect);
            GameObject joystickObject = new("Test Joystick", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            joystickObject.transform.SetParent(canvasRect, false);
            background = joystickObject.GetComponent<RectTransform>();
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.zero;
            background.pivot = new Vector2(0.5f, 0.5f);
            background.sizeDelta = new Vector2(200f, 200f);
            background.anchoredPosition = new Vector2(140f, 140f);

            GameObject handleObject = new("Handle", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            handleObject.transform.SetParent(background, false);
            RectTransform handle = handleObject.GetComponent<RectTransform>();
            handle.sizeDelta = new Vector2(80f, 80f);

            VirtualJoystick joystick = joystickObject.AddComponent<VirtualJoystick>();
            joystick.SetPlayerController(player);
            SetPrivateField(joystick, "background", background);
            SetPrivateField(joystick, "handle", handle);
            Assert.That(canvas, Is.Not.Null);
            return joystick;
        }

        private Canvas CreateCanvas(out RectTransform rectTransform)
        {
            GameObject canvasObject = Track(new GameObject(
                "Test Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster)));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rectTransform = canvasObject.GetComponent<RectTransform>();

            UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private EventSystem CreateEventSystem()
        {
            GameObject eventSystemObject = Track(new GameObject("Test EventSystem", typeof(EventSystem)));
            InputSystemUIInputModule module = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            return eventSystemObject.GetComponent<EventSystem>();
        }

        private RectTransform CreateRaycastImage(
            string name,
            RectTransform parent,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 size,
            Vector2 anchoredPosition)
        {
            GameObject imageObject = new(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            imageObject.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            return rect;
        }

        private UnityEngine.UI.Button CreateButton(Transform parent, string name)
        {
            GameObject buttonObject = new(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            buttonObject.transform.SetParent(parent, false);
            return buttonObject.GetComponent<UnityEngine.UI.Button>();
        }

        private T CreateChildComponent<T>(Transform parent, string name) where T : Component
        {
            GameObject child = new(name, typeof(RectTransform), typeof(T));
            child.transform.SetParent(parent, false);
            return child.GetComponent<T>();
        }

        private T AddDevice<T>(string name) where T : InputDevice
        {
            T device = InputSystem.AddDevice<T>(name);
            addedDevices.Add(device);
            return device;
        }

        private GameObject Track(GameObject gameObject)
        {
            spawnedRoots.Add(gameObject);
            return gameObject;
        }

        private static Vector2 ScreenPoint(RectTransform rect)
        {
            return RectTransformUtility.WorldToScreenPoint(null, rect.position);
        }

        private static List<string> RaycastNames(EventSystem eventSystem, Vector2 position)
        {
            PointerEventData pointer = new(eventSystem) { position = position };
            List<RaycastResult> results = new();
            eventSystem.RaycastAll(pointer, results);
            return results.ConvertAll(result => result.gameObject.name);
        }

        private static int ActiveTouchCount(Touchscreen touchscreen)
        {
            int count = 0;
            foreach (var touch in touchscreen.touches)
            {
                if (touch.press.isPressed)
                {
                    count++;
                }
            }

            return count;
        }

        private static float CalculateCanvasScale(Vector2 screenSize, Vector2 referenceResolution, float match)
        {
            float logWidth = Mathf.Log(screenSize.x / referenceResolution.x, 2f);
            float logHeight = Mathf.Log(screenSize.y / referenceResolution.y, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, match));
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AssertVector2(Vector2 actual, Vector2 expected, float tolerance)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing method: {methodName}");
            method.Invoke(target, null);
        }

    }
}
