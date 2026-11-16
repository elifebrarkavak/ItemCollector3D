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

## Intermittent test failure: PlayerTestScene_PlayerStaysOnGround_WhileWalkingWithKeyboard

**Test:** `PlayerSystemsPlayModeTests.PlayerTestScene_PlayerStaysOnGround_WhileWalkingWithKeyboard`

**Status:** Open, cause NOT proven. Test-stability problem, **not a verified gameplay bug**.

**Symptom:** In one full-suite run (right after a recompile and asset creation) the "D must move the player right"
assertion failed (moved 1.2 m, expected more than 3 m). An isolated re-run passed, and it passed in all earlier runs.
No Player / UI / PlayerTest file had been changed.

**Notes:** The test holds a key for a fixed wall-clock time (`Time.time`) and measures distance, so a slow or hitching
Editor can shorten the effective movement time. That is a guess, not a finding. If it recurs, log player position per
frame during the D phase to see whether the key state or the frame rate is the cause. Do not raise the 3 m threshold
just to make the test pass.

## HUD gold progress bar did not visually fill - FIXED (branch `feature/rarity-feedback`)

**Where:** `PlayerUI` prefab, `HUD/HeaderPanel/GoldProgressBackground/GoldProgressFill` (`Image`).

**Root cause (confirmed in the Editor):** the Image was `Type = Filled`, `Fill Method = Horizontal` with **no sprite**. Unity
ignores `fillAmount` on a Filled image without a sprite and draws the whole quad, so the bar always looked full even though
`HudController` set the right value (`100 / 500` showed `fillAmount = 0.20` but a 100 % wide bar; reproduced again in Play Mode).

**Fix:** assigned a plain white sprite (`Assets/UI/Sprites/WhiteSquare.png`, new 8x8 asset) to `GoldProgressFill` in
`PlayerUI.prefab` (one-line prefab change; type, method, colour and the `HudController` API are untouched). A first attempt with the
built-in rounded `UISprite` filled correctly but tapered into a sliver on the thin bar, so it was replaced.

**Verified:** Game View captures + pixel measurement against the 500/500 width: 0 -> 0.0 %, 10 -> 1.9 %, 100 -> 19.8 %,
250 -> 50.0 %, 500 -> 100 %. New permanent test `HudProgressBarPlayModeTests` checks the generated mesh width (what is drawn), and
was shown to fail on the old sprite-less setup.

**Still open (visual polish, not a bug):** the bar fill is a flat orange rectangle; no animation / final art.

## Open visual improvement: reward particle burst is subtle and can be hidden by the player

**Status:** Open (visual polish, not a functional bug). Found while checking the rarity reward effects in the Editor Game View.

- **Occlusion:** the burst spawns at the treasure (0.4 m above it). The third-person camera sits behind the player, so when the
  treasure is directly ahead of the player the early part of the burst is mostly hidden behind the player capsule. Rare / Epic still
  show a coloured halo (glow) or ring around the capsule, but Common / Uncommon show almost no visible particles in that case.
- **Subtle particles:** the default particles are small square placeholders that shrink away (16 / 23 / 30 / 36 per burst for
  Common..Epic); with the treasure beside the player they are visible but modest.
- **Ideas (not done, no redesign in this stage):** spawn the burst higher or nudge it toward the camera, use a soft round particle
  sprite and slightly larger size; final particles from Sibel can replace the default through the profile's particle prefab.

## Open verification: collect sound and detector beep audibility

**Status:** Not yet tested by a human. The code path is verified (the sound is triggered once per reward with the profile pitch;
`AudioSource.isPlaying` observed), but nobody has listened to it yet, so audibility, volume and pitch on a real device are unverified.
This is NOT a report that the sound is silent. Same for the detector beep. Real Android device testing is also still pending.

