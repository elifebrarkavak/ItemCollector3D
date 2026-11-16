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

## Manual Play Mode report: HUD vanished and WASD ignored (investigated after PR #4)

A manual Play Mode run of `GameplayTest` showed no HUD (title, bar, joystick, NO SIGNAL), a greyed `UI Canvas` in the Hierarchy and
WASD doing nothing. The Console of that run held 161 errors (16:20:28-16:20:45 UTC): 64+ x `Invalid AABB inAABB` (from
`TextMeshProUGUI.GenerateTextMesh` -> `CanvasRenderer.SetMesh`) and 36+ x `Assertion failed on expression: 'IsFinite(gain)'`
(from `AudioSource.PlayOneShot` <- `DetectorSignalIndicator.HandlePulse`).

**HUD / Console errors - what is PROVEN**
- Both error signatures are produced by NaN RectTransforms under `SafeAreaRoot`: emulating what `SafeAreaFitter.ApplySafeArea` computes for
  a zero-sized screen (`safeArea / (0,0)`) made every HUD rect NaN, TMP logged `Invalid AABB inAABB`, and once the detector pulsed
  `PlayOneShot` logged `IsFinite(gain)` with the same stack (the detector `AudioSource` sits on a UI object, so its position was NaN too).
- `SafeAreaFitter` had no guard against zero/NaN screen size -> real code defect. FIXED (see below).
- In a normal Game View (1571x1044) Play Mode showed the HUD, an active `UI Canvas`, an `EventSystem`, a grounded Player and 0 Console errors.

**NOT proven**
- What made the screen size degenerate in the manual run. Edit Mode reported `Screen = 3024x72` (a hidden / collapsed / very flat Game
  View is the likely area) but that is a guess, not a finding.
- Why the `UI Canvas` appeared greyed in the Hierarchy. No project script deactivates it (`SetActive` is only used for HUD buttons, treasure
  visuals and a glow light); with NaN layout the canvas stays `activeSelf = True`. It may have been toggled by hand - unknown.

**Fix (small hardening, committed on `feature/rarity-feedback` as part of PR #4):** `SafeAreaFitter.TryComputeAnchors` rejects zero/negative/NaN/Infinity screen size or
safe area, keeps the current (full-stretch) anchors and retries next frame. Tests: `SafeAreaFitterPlayModeTests` (9 cases).

**WASD - what is PROVEN (not a code bug)**
- The project has no Input Settings asset, so the Input System default `PointersAndKeyboardsRespectGameViewFocus` applies: when the Game View
  does not have focus, keyboard events go to the Editor and the game does not see them.
- Controlled test: Editor focused -> W moved the Player (z 0 -> 22 at ~5 m/s, `moveInput = (0,1)`, grounded). Another app in front
  (`Application.isFocused = False`) -> the keyboard device reported W pressed but the Player's `moveInput` stayed `(0,0)` and it did not move.
- **To play manually: click inside the Game View once after entering Play Mode.** No code change was made for this.
- NOT proven: that the manual run was unfocused (not observable now). Optional (not applied): an Input Settings asset with
  `editorInputBehaviorInPlayMode = AllDeviceInputAlwaysGoesToGameView` would route keys to the game even without focus, but would also let
  Editor typing reach the game, so it was left to the team to decide.

## Dig feedback (stage 5, branch `feature/dig-feedback`): open points

- **Dig sound audibility: not yet tested by a human.** The code path is verified (triggered once per stroke, 2 per dig, through one AudioSource
  with pitch/volume set), but nobody has listened to it. This is NOT a claim that it is silent. Same status as the collect sound and the
  detector beep above.
- **Placeholders:** the dig motion (tilt/roll/dip/squash of a child `Visual`), the square brown dirt particles and the procedural thud are
  placeholders; checked only in the Editor Game View (slow-motion captures), not on a real Android device, and not profiled.
- **Movement during a dig (resolved in this branch):** the target is locked at DIG; the player may move but leaving the dig radius cancels the
  dig (treasure stays Hidden, animation back to rest, DIG usable again in range). Exact radius, no grace margin.
- **Unexplained observation (not reproduced):** in one manual run the treasure ended `Collected` although only DIG was pressed by the script.
  A controlled, instrumented repro (stack logging on both buttons and `TreasureCollected`) ended `Revealed` with gold 0 and only the DIG click
  logged; the only code path to `Collect()` is the CollectButton click. A manual click on COLLECT is the likeliest cause but is NOT confirmed.
  Note: DIG (disabled while digging) and COLLECT share the same screen spot, so a human double-tap on DIG may hit COLLECT at the reveal.

## UX observation: DIG and COLLECT share the same position (needs real Android evaluation)

DIG ve COLLECT butonları aynı konumda bulunduğundan hızlı çift dokunma istenmeden COLLECT tetikleyebilir. Bu davranışın gerçek Android
cihazda değerlendirilmesi gerekiyor.

(English: the DIG and COLLECT buttons sit at the same spot - DIG stays visible but disabled during the 0.45 s dig and COLLECT replaces it at
the reveal - so a quick double tap on DIG could trigger COLLECT unintentionally. Not reproduced in code or tests, where the only collect path is a
CollectButton click; it must be evaluated on a real Android device. Possible mitigations if it turns out to be a problem: a short input
lockout on COLLECT after the reveal, or separate button positions. Neither is implemented.)

