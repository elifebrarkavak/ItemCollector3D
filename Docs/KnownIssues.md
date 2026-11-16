# Known Issues

## Unresolved test instability: joystick-release assertion (PlayMode)

**Test:** `PlayerSystemsPlayModeTests.TwoTouches_JoystickMovesPlayer_WhileRightSwipeRotatesCamera`
(`Assets/Tests/PlayMode/PlayerSystemsPlayModeTests.cs`)

**Status:** Open. This is a test-stability problem, **not a verified gameplay bug**.

**Symptom:** The final assertion (player moves less than 0.05 m in the 5 frames after the joystick is
released) failed intermittently (2 of 12 runs) when the Unity Editor did not have OS focus. It passed in
every focused run and in the full-suite runs on the final code (8/8).

**What is known:**
- The joystick itself resets correctly: `VirtualJoystick.OnPointerUp` calls `ClearMoveInput()`, so
  horizontal input stops immediately.
- A separate, earlier flake (camera yaw staying 0) was traced to Editor focus: with the default
  `editorInputBehaviorInPlayMode`, the Input System routes pointer input to the Editor when the Game View
  is unfocused and never raises `wasPressedThisFrame` for injected touches. The test class now overrides
  `backgroundBehavior` and `editorInputBehaviorInPlayMode` in `SetUp` and restores them in `TearDown`.

**Suspected (not proven) cause:** the measured distance includes vertical movement. The test spawns the
Player at y = 1 and `CharacterController` rests with a 0.08 m skin width, so the Player may still be
settling onto the ground during the measured frames; with short or variable frame times in an unfocused
Editor this can exceed 0.05 m. The horizontal (XZ) and vertical (Y) components were never measured
separately, because the diagnostic run was stopped.

**Suggested follow-up:** measure XZ and Y drift separately. If only vertical settling is present, assert
horizontal drift only in this test and cover grounding in the dedicated grounded/physics tests. If real
horizontal drift is found, investigate the cause. Do not raise the 0.05 m threshold just to make the test pass.

## Other pending items
- Not yet tested on a real Android device (touch input, Safe Area, CanvasScaler on device).
