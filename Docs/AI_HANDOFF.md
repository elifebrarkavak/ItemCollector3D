# AI Handoff (Codex <-> Claude Code)

Rules for whichever agent picks this up:
1. Read this file AND run `git status` / `git log` before starting; trust Git over this file if they disagree.
2. Only ONE agent may use the Unity Editor / edit project files at a time. If `git status` shows uncommitted
   changes you did not make, check the "Current state" section before touching them.
3. Do not rebuild systems listed as done. No commit / push / merge without the user's explicit permission.
4. No long test-repeat loops. One quick run is enough.
5. Update this file when you finish or stop (even if the task is half done).

## Current state (2026-11-16)
- Branch: `feature/treasure-mvp` (from `origin/main` @ `ecc3b95`). Work below is committed on this branch and pushed to
  `origin/feature/treasure-mvp` (see `git log`). No PR opened, not merged to `main`.
- Last agent: Claude Code. Final verification done (see below). Status: single-treasure Gold Coin MVP loop is **done and verified** (including real
  WASD input via Input System keyboard in a PlayMode test); gold target set to 500. Next: Concept Document audit
  (open task below); open a PR only when the user asks.
- Unity Editor was left with Play Mode off and `GameplayTest` open and not dirty.

## Done on `main` (do not recreate)
- Player (`ThirdPersonPlayerController`), Camera (`ThirdPersonCameraController`), `VirtualJoystick`,
  `HudController` (+ `SafeAreaFitter`), prefabs `Player` / `PlayerUI`, scene `Assets/Scenes/Tests/PlayerTest.unity`,
  PlayMode tests `Assets/Tests/PlayMode/PlayerSystemsPlayModeTests.cs`.

## Done on this branch: Treasure MVP (Explore -> Detect -> Dig -> Reveal -> Collect -> Earn Gold)
New files (all untracked):
- `Assets/Scripts/Treasure/`
  - `TreasureDefinition.cs` - ScriptableObject: id, name, gold value, detection/dig/collect radii. New treasure
    types = new definition asset + prefab (no new code needed unless behaviour differs).
  - `TreasureSpot.cs` - state machine Hidden -> Revealed -> Collected (`Reveal()` / `Collect()` return false when
    the transition is not allowed, which is what blocks double collection). Static `TreasureSpot.Active` registry.
  - `TreasureDetector.cs` - nearest hidden spot in detection radius -> `Proximity` 0..1 -> pulse interval
    (slowest 1.6 s -> fastest 0.15 s); `Pulsed` event; static `ProximityFor` / `IntervalFor`.
  - `TreasureHuntController.cs` - drives the EXISTING `HudController` only: shows DIG within dig radius (Hidden
    spot), COLLECT within collect radius (Revealed spot), adds gold, `SetGold`, sets title and max gold (10).
  - `DetectorSignalIndicator.cs` - flashing dot + "SIGNAL n%" label + procedural beep (no audio assets).
- `Assets/Scenes/GameplayTest.unity` - copy of PlayerTest (Player / Camera / PlayerUI HUD reused) plus
  `Treasure_GoldCoin_01` at (8, 0, 20), `TreasureSystems` (detector + hunt controller) and
  `UI Canvas/SafeAreaRoot/DetectorIndicator` (added GameObject override on the PlayerUI instance).
- `Assets/Data/Treasures/GoldCoin.asset` (10 gold, detect 15 m, dig 2.5 m, collect 3 m),
  `Assets/Materials/GoldCoin.mat`, `Assets/Prefabs/Treasure/Treasure_GoldCoin.prefab`.
- `Assets/Tests/PlayMode/TreasureHuntPlayModeTests.cs` (2 tests: signal mapping, full loop in the real scene).
- `Docs/AI_HANDOFF.md` (this file).
No existing scene, prefab or script was modified. Beyza's GreenMine scene was not touched (it does not exist yet).

## Final verification (2026-11-16, no code changed in this step)
- Branch `feature/treasure-mvp`, Play Mode off, `GameplayTest` open and not dirty (state at verification time).
- Full PlayMode suite, one run, final test files: **11/11 PASSED** (8 `PlayerSystemsPlayModeTests` + 3
  `TreasureHuntPlayModeTests`). Console: 0 errors, 0 warnings, no compile failure.
- GameplayTest: `goldTarget` = 500 kept (HUD `0 / 500 GOLD` -> `10 / 500 GOLD`, fill 2%, both asserted by the passing
  full-loop test).
- Reference scan (scene + Treasure_GoldCoin prefab): 0 missing scripts; TreasureHuntController (hud, player),
  TreasureDetector (player), DetectorSignalIndicator (detector, dot, label, AudioSource), TreasureSpot (definition,
  visual) and all HudController fields are assigned; every new asset has a `.meta`.
- Scope: `GameplayTest` is a single-treasure TECHNICAL PROTOTYPE scene, not the main game scene.
- Concept Document v1.0 was NOT available in the repo or workspace, so compliance with it could not be checked.
  Whoever has the document should compare it against the "Done on this branch" list before building further.

## Changes in the follow-up session (2026-11-16, same branch, still uncommitted)
- `GameplayTest.unity`: `TreasureHuntController.goldTarget` 10 -> 500 (Inspector field only, no code change).
  HUD now reads `0 / 500 GOLD` -> `10 / 500 GOLD`, fill 0.02.
- `Assets/Tests/PlayMode/TreasureHuntPlayModeTests.cs`: updated gold expectations (`0 / 500 GOLD`, `10 / 500 GOLD`,
  fill 0.02); added focus-independent input SetUp/TearDown; added
  `GameplayTestScene_WalkWithWasdToTreasure_StaysGrounded_CameraFollows_SignalSpeedsUp`.
- `Docs/AI_HANDOFF.md` (this file). No Player / HUD / treasure script changed.

## Test results
- Full PlayMode suite, one run: 10/11 passed. The only failure was my NEW walk test: its camera-distance check
  measured the scene camera's start-up transient (camera starts ~10 m behind the player and SmoothDamps in; measured
  9.7 m) - a test measurement flaw, fixed by letting the camera settle 1 s first (8 m limit unchanged).
- Walk test then failed once more on a flash check because the dot colour is updated in the indicator's `Update`
  (my test sampled it inside the Pulsed event) - fixed by sampling per frame. Then it PASSED (single-test runs).
- The final test file was NOT re-run as a full suite afterwards; the other 10 tests passed in the full run and their
  code was not changed after it (only the walk test / helpers were edited).
- Real-movement check: an attempt to hold W through the CLI `runtime simulate_key` while Play Mode was running was
  unreliable (Editor focus kept flipping, `Application.isFocused=False`, player barely moved) and is NOT counted.
  Walking was verified instead by the PlayMode walk test, which queues Input System Keyboard events (W then D) through
  the game's real `ReadKeyboardInput` path (injected events, not a physical keyboard).
- Verified by that test: player stays on ground (y 0.8..1.4), camera within 8 m while moving, signal interval shrinks
  while approaching, DIG hidden beyond ~2.8 m and shown inside ~2.2 m, DIG reveals, COLLECT gives 10 gold, second
  COLLECT adds nothing.
- Detector feedback: dot flash colour observed in frames after pulses; `AudioSource.isPlaying` observed true after
  pulses; AudioSource + AudioListener present, project audio not disabled, Editor not muted. **Audibility was NOT
  verified - nobody listened to the output.**
- Console: 0 errors; 1 unrelated warning ("Account API did not become accessible within 30 seconds").

## NOT tested / known issues
- Walking with the on-screen joystick and with a physical keyboard was not exercised; beep never heard by a human.
- Real Android device testing not done (touch, Safe Area, performance).
- `TwoTouches_JoystickMovesPlayer...` PlayMode test can fail intermittently when the Editor is unfocused
  (see `Docs/KnownIssues.md`); not a verified gameplay bug.
- HUD title is still "GOLD COIN TEST" (`TreasureHuntController.levelTitle`); Beyza's GreenMine scene does not exist
  yet and was not touched - when it does, add the treasure systems there and set goldTarget to its own total.

## Concept Document v1.0 audit (done 2026-11-16; document: YAM415_G09_ConceptDocument v1.0, sections 2.4-2.9)
Verdict: NO contradictions found; the prototype is a correct technical slice of the document's core loop.
`GameplayTest` is a single-treasure technical prototype, NOT the main game scene; missing sections are not failures.

IMPLEMENTED: third-person camera behind/above + right-side swipe orbit + left virtual joystick; contextual DIG/COLLECT
buttons (DIG at valid position, then COLLECT); hidden (invisible) treasure; distance-based detector with accelerating
beeps (pitch rises, interval 1.6 s -> 0.15 s); Dig reveals, Collect adds gold; Gold Coin = 10 gold; Green Mine target
500 in the HUD (`0 / 500` -> `10 / 500`); one treasure cannot be collected twice; no combat/death/failure state.
PARTIALLY IMPLEMENTED: shared collectible system (`TreasureDefinition` has name, gold value, ranges but NO rarity,
model or feedback profile); detector "weak -> strong" visual indicator (dot flash intensity and beep volume are
constant; only interval, pitch and the SIGNAL % label change; doc names slow/fast/rapid states, we use a continuous
curve - fine, doc says timing is set by playtesting); touch controls (joystick/swipe exist and are PlayMode-tested,
DIG/COLLECT verified via onClick, never on a real touchscreen); HUD shows level title + gold (title is "GOLD COIN TEST",
not an area name).
PLANNED / NOT YET BUILT: Main Menu and Area Selection; rarity tiers (Common 10, Uncommon 25 Gold Nugget, Rare 50
Gold Bar, Epic 100 Ancient Artifact); rarity-specific feedback ("RARE! +50 Gold"); Dig animation + dirt particles +
dig sound; collect particle burst, collect sound, floating gold text, counter animation; optional haptics; Area
Complete / New Area Unlocked / return to Area Selection; area unlocking; persistent treasury across areas ("leaving an
area does not erase progress"); Rocky Canyon (1200 total) and Ancient Mine (2500 total); multiple treasures per area;
Green Mine scene (Beyza); Android build + device testing; style pass (warm earth/green/rock tones, gold glow).
DESIGN NOTES FOR LATER (not conflicts): (1) Rocky Canyon / Ancient Mine targets are CUMULATIVE ("total gold"), but
`TreasureHuntController.TotalGold` currently lives in the scene - needs a persistent treasury (and HUD target logic)
before area 2; `HudController.SetGold` also clamps to the target. (2) `GameplayTest` is not in Build Settings (only
SampleScene is) - add the real scenes when building for Android. (3) The document says the exact Unity version is to
be recorded: project uses 6000.6.9f1.
Suggested order: rarity in `TreasureDefinition` -> feedback profile (floating text/particles/sounds) -> Area Complete +
treasury persistence -> Main Menu/Area Selection -> more areas -> Android device test.

## Next steps
1. User reviews the branch; commit when the user allows it (suggested message: `feat: add single-treasure gold coin MVP`).
2. Walk-through with real input (WASD / joystick) and a device build.
3. Then extend: more treasure instances/types (new `TreasureDefinition` assets + prefabs), dig duration/animation,
   multiple treasures per level (raise `goldTarget`), GreenMine scene integration by its owner.
