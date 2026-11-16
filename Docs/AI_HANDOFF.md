# AI Handoff (Codex <-> Claude Code)

Rules for whichever agent picks this up:
1. Read this file AND run `git status` / `git log` before starting; trust Git over this file if they disagree.
2. Only ONE agent may use the Unity Editor / edit project files at a time. If `git status` shows uncommitted
   changes you did not make, check "Current state" below before touching them.
3. Do not rebuild systems listed as done. No commit / push / merge without the user's explicit permission.
4. No long test-repeat loops. One quick run is enough. Never write PASS for a test you did not run.
5. No new packages without asking. Update this file when you finish or stop (even if the task is half done).

## Current state (2026-11-16)
- Branch: `ebrar`. Stage 6 (persistent progression, commit `f071a90`) and stage 7 (game menus, commit `feat: add main menu settings pause and notifications`; hash: see
  `git log`) are both COMMITTED and PUSHED to `origin/ebrar`. NOTE: PR #6 (stage 6) uses `ebrar` as its head branch, so it now also contains the stage 7 commit
  (its description was written for stage 6 only and was not edited). PR #6 is still OPEN and NOT merged. Stage 7 reads the stage 6 `ProgressManager` events.
- Last agent: Claude Code. Unity Editor left with Play Mode off and `GameplayTest` open, not dirty.
- **Manual-testing notes:** the game saves to a REAL file (`~/Library/Application Support/DefaultCompany/ItemCollector3DStaging/item_collector_progress.json`,
  see stage 6) and settings go to the Editor's PlayerPrefs. Automated tests use in-memory stores for both and never touch them. The user's own save
  (totalGold 10, coin collected) was backed up and restored byte for byte around my manual visual runs.

## Stage 7 (branch `ebrar`, pushed; inside PR #6): Game menus - Main Menu, Settings, Pause, progress notifications
Flow: Main Menu -> PLAY -> GameplayTest -> PAUSE -> SETTINGS -> RESUME -> PAUSE -> MAIN MENU. No Area Selection / Character Selection (later stages).

Code - NEW `Assets/Scripts/Menus/` (namespace `ItemCollector3D.Menus`):
- `GameSettings` (+ `ISettingsStore`, `PlayerPrefsSettingsStore`, `MemorySettingsStore`): Music volume (0..1, default 0.8), SFX volume (0..1, default 1),
  Camera sensitivity (0.25..2, default 1). Clamped, NaN/Infinity/missing values fall back to safe values, saved to PlayerPrefs on every change,
  `Changed` event. Shared instance `GameSettings.Current` (`StoreFactoryOverride` + `ResetCurrent()` for tests); static `SfxScale`, `CameraSensitivityScale`.
- `GamePause` (static): `Pause()` sets `Time.timeScale = 0` and `AudioListener.pause = true`, `Resume()` restores the previous time scale; `IsPaused`, `Changed`.
- `SceneNavigator` (static): `Load(sceneName)` resumes first, then `SceneManager.LoadScene`; `LoadHook` for tests; names `MainMenu`, `GameplayTest`.
- `SettingsPanelController`: the ONE settings panel (3 sliders + value labels + BACK); shows stored values on open without firing changes.
- `MainMenuController`: PLAY (loads `GameplayTest`), SETTINGS (hides the buttons, shows the panel), EXIT (`Application.Quit`, stops Play Mode in the Editor; `ExitHook` for tests).
- `PauseMenuController`: PAUSE button, panel with RESUME / SETTINGS / MAIN MENU, Escape (Android Back) toggles pause / closes settings; always resumes on
  leave/destroy so a scene never starts frozen.
- `NotificationBanner` (pop-in, hold, fade on UNSCALED time, never blocks touches) + `ProgressNotificationPresenter`: listens to the EXISTING
  `ProgressManager.AreaCompleted` / `AreaUnlocked`, shows "AREA COMPLETE!" and "NEW AREA UNLOCKED!" (+ area name) one after another from a queue, each
  (kind, area) at most once; loading a saved game replays nothing (the stage 6 service never re-raises).
- `AmbientMusicPlayer`: quiet procedural pad loop (no audio files; there was no music, so Music Volume needed something real to control); follows the
  setting live; keeps playing while paused (`ignoreListenerPause`); a real clip can be assigned later.
- `IdleMotion`: tiny bob/swing for the placeholder miner.
Code - CHANGED (minimal): `ThirdPersonCameraController` (orbit x Camera Sensitivity; ignores orbit input while paused), `CollectSoundPlayer` /
`DigSoundPlayer` / `DetectorSignalIndicator` (volume x SFX Volume; indicator exposes `LastBeepVolume`), `TreasureHuntController` (DIG/COLLECT ignored
while paused).
Vibration: NOT added on purpose - nothing in the game vibrates yet, so a toggle would do nothing (add it together with haptic feedback).

Assets / scenes:
- NEW prefabs `Assets/Prefabs/Menus/`: `SettingsPanel` (shared), `PauseMenu` (nests SettingsPanel), `MainMenuUI` (nests SettingsPanel), `ProgressNotification`.
  Forest green / wood brown / gold palette, Canvas + TextMeshPro, placed under `SafeAreaRoot` (Safe Area kept), root has no raycast graphic.
- NEW scene `Assets/Scenes/MainMenu.unity`: landscape stylized mine scenery from ~17 primitive objects (ground, mountains, mine entrance with wooden frame and
  sign, gold cart, 6 trees, 5 rocks, placeholder miner with gold helmet + pickaxe), warm light without shadows, Canvas > SafeAreaRoot > MainMenuUI, EventSystem,
  ambient music. Materials `Assets/Materials/Menu/` (11). PLACEHOLDER art only, no colliders.
- CHANGED `GameplayTest.unity` (additions): `PauseMenu`, `ProgressNotification` (bound to the ProgressManager), `AmbientMusic`.
- CHANGED `ProjectSettings/EditorBuildSettings.asset`: scenes = MainMenu, GameplayTest, SampleScene (PLAY needs GameplayTest in Build Settings; a build starts at the menu).
- CHANGED `ProjectSettings/ProjectSettings.asset`: orientation = auto-rotate LANDSCAPE only (portrait and upside-down disabled).
Tests (all PlayMode): `SettingsPlayModeTests` (8), `MenuFlowPlayModeTests` (10), `ProgressNotificationPlayModeTests` (4), `MenuEndToEndPlayModeTests` (1);
`ProgressTestIsolation` now also forces an in-memory settings store, resets pause/hooks.

### Bug found and fixed in stage 7 (honest record)
Opening the Settings panel overwrote the saved Camera Sensitivity: `Refresh()` changes the slider min/max, Unity clamps the value and fires `onValueChanged`,
and the listener saved the clamped value (1.5 -> 0.25). Caught by a test (`Settings_ShowStoredValues_ApplyLive_AndArePersisted`), fixed with a `refreshing` guard,
regression test `OpeningSettings_NeverChangesTheStoredValues`.

### Test results (real, stage 7)
- Targeted: Settings 8/8, MenuFlow 10/10, notifications 4/4, end-to-end 1/1 (the end-to-end test uses REAL scene loads: menu -> PLAY -> dig + collect the coin ->
  PAUSE -> SETTINGS (SFX 0.3) -> RESUME -> PAUSE -> MAIN MENU -> settings persisted -> PLAY again -> coin still collected, HUD `10 / 500 GOLD`).
- Full PlayMode suite, ONE run after all fixes: **104/104 PASSED**, Console 0 errors / 0 warnings, the user's real save file unchanged. The older intermittent Player
  tests passed again; their cause is still NOT proven (see Known Issues).
- Real effect checks: SFX 0.5 halves the collect and dig volume (0 = silent); Camera Sensitivity 2x / 0.5x doubles / halves the orbit (and 0 while paused);
  Music Volume changes the music loudness live; pause freezes the player and the dig timer (frozen for longer than a whole dig, then completes after RESUME).
- Manual Game View check with real clicks (Editor, slow flow): Main Menu, Settings (menu and pause), gameplay with the PAUSE button, pause panel, "AREA COMPLETE!" and
  "NEW AREA UNLOCKED!" banners. Visual fixes made after the first look: title wrapped into 3 lines (now 2), band cut the miner, settings rows overlapped the sliders and
  the track was invisible, MAIN MENU button had no contrast, banner touched its edges and overlapped the HUD header. All re-checked in screenshots.

### Stage 7 limitations (not verified / open)
- Music / SFX / beep audibility and how the procedural music sounds: NOT tested by a human. Nothing was run on a real Android device (touch on the new buttons and
  sliders, Back key, orientation lock, PlayerPrefs, performance of the menu scene).
- Placeholder scenery / miner / UI art (primitives and flat colours); the notification banner has no sound; no Area Selection (the Concept Document flow is Main Menu
  -> Area Selection -> area), no Character Selection, no vibration setting.
- Settings sliders are touch-sized for 1920x1080 reference only; not checked on small phones. Joystick while paused: the held input is ignored by the frozen game.

## Stage 6 (branch `ebrar`, PR #6 open): Game progression & save system
Goal: persistent, cumulative progress without a second gold system. The existing `TreasureHuntController.TotalGold` / HUD now read from a
`ProgressService`; DIG -> COLLECT -> Gold Reward and the HUD are unchanged for the player.

Code - NEW `Assets/Scripts/Progress/` (namespace `ItemCollector3D.Progress`):
- `GameProgressData` - saved state (JsonUtility): `version` (1), `totalGold` (cumulative), `collectedTreasureIds`, `unlockedAreaIds`, `completedAreaIds`.
- `AreaCatalog` (ScriptableObject) + `AreaEntry` - ordered areas with unlock / goal TOTAL gold (Concept Document 2.7): Green Mine open at start,
  goal 500; Rocky Canyon unlocks at 500, goal 1200; Ancient Mine unlocks at 1200, goal 2500. `Validate()` (unique ids, first area open, increasing
  thresholds). `CreateDefault()` is the built-in fallback if the asset is missing/invalid.
- `IProgressStore` + `FileProgressStore` (atomic write via temp file + replace, `.corrupt` backup, never throws) + `MemoryProgressStore` (tests).
- `ProgressService` (plain C#): `RegisterCollected(treasureId, gold)` -> `Accepted` / `AlreadyCollected` / `InvalidId` / `InvalidGold`; gold is added once
  per treasure id; thresholds are evaluated and `AreaCompleted(areaId)` ("AREA COMPLETE") / `AreaUnlocked(areaId)` ("NEW AREA UNLOCKED") fire ONCE per
  area (several can fire for one big collection, in order: unlock, complete); saved after every change; `Load()` never throws: missing file = NewGame;
  empty / unparsable / unsupported-version data = Recovered (bad text backed up, clean data written); readable but inconsistent data (negative or absurd
  gold, empty/duplicate ids, unknown areas, area flags that disagree with the gold) is repaired and reported. Loading never replays notifications.
- `ProgressManager` (MonoBehaviour): creates the service (file store, or `ProgressManager.StoreFactoryOverride` for tests), relays the two area events,
  saves on `OnApplicationPause`, context menu "Reset Saved Progress".
- `ProgressLog` - all progress diagnostics (recovered save, duplicate id, I/O) go through `ProgressLog.Warning`; tests capture them via `WarningSink`.
Code - CHANGED:
- `TreasureSpot` - new serialized `spotId` (unique id of THIS placed treasure; `TreasureDefinition.Id` is only the TYPE id), `SpotId` (explicit, or a
  deterministic fallback `scene/type@x,y,z`), `HasExplicitId`, `SetSpotId`, `RestoreCollected()` (silent: no event, no gold), static
  `AlreadyCollectedProvider` consulted in `OnEnable` so a reloaded scene or a late-spawned spot starts Collected.
- NEW `TreasureIdValidator` / `TreasureIdReport` - reports duplicate ids and how many spots use a fallback id.
- `TreasureHuntController` - optional `progress` (ProgressManager) and `areaId`; `TotalGold` reads the persistent total (scene-local counter only when no
  ProgressManager is assigned, exactly as before); collect goes through `RegisterCollected` (a duplicate id pays nothing and raises no reward event);
  HUD maximum = the area's goal, title = the area's display name (`GREEN MINE`); restores collected spots on start; a LOCKED area offers no DIG/COLLECT
  (`IsAreaAccessible`, `SetAreaId`); logs duplicate treasure ids.
Assets / scene:
- NEW `Assets/Data/Areas/AreaCatalog.asset` (the three areas above).
- CHANGED `GameplayTest.unity` (additions): `ProgressManager` object (catalog assigned), controller `progress` + `areaId = green_mine`, coin
  `spotId = green_mine.gold_coin_01`.
Tests (all PlayMode):
- NEW `ProgressTestIsolation` ([SetUpFixture] for the whole `ItemCollector3D.Tests.PlayMode` namespace): forces a fresh in-memory store for every
  ProgressManager and captures progress warnings, so tests never read/write the real save and the Console stays clean.
- NEW `ProgressServiceTests` (23): catalog values/validation/fallback; new game; thresholds + once-only events + cumulative gold; one collection
  crossing several thresholds; same id pays once; invalid registrations; save/load round trip (events not replayed); reset; corrupt data
  (7 bad inputs: recovered, backed up, replaced); missing fields; inconsistent data repaired; gold above threshold repaired silently; absurd gold
  clamped; null store; real `FileProgressStore` round trip incl. corrupt file. Each test declares the warning it expects; any other warning fails it.
- NEW `ProgressSceneTests` (8, real GameplayTest + controller + HUD): collecting saves gold + id and the HUD shows `0 / 500` -> `10 / 500` with title
  `GREEN MINE`; scene reload does not pay the collected coin again and gold keeps accumulating (`35 / 500`); area events fire once per threshold;
  a locked area (`rocky_canyon`, HUD `0 / 1200`) offers no DIG until 500 total gold, then gold carries over (`510 / 1200`); treasure ids are unique
  (and duplicates are detected); two spots with the same id pay once; destroy-order regression (below); fresh save starts clean.

### Bug found and fixed during stage 6 (honest record)
The first full-suite run was 78/79: an existing dig test failed (IsDigging false after DIG). Not flaky - a real defect in my new code. Unity destroys
scene objects in no fixed order; `DisconnectProgress` only cleared the static `TreasureSpot.AlreadyCollectedProvider` while `progress` was still
alive, so a stale provider could survive into the next scene load and silently mark the coin Collected. Reproduced deterministically
(`Scene_DestroyOrder_NeverLeavesAStaleCollectedProvider_ForTheNextScene` failed before the fix), fixed by caching the connected service in the
controller. The dig class alone had passed (lucky destroy order).

### Test results (real, stage 6)
- Compile: 0 errors. `ProgressServiceTests` 23/23, `ProgressSceneTests` 8/8 (targeted).
- Full PlayMode suite, ONE run after all fixes: **81/81 PASSED**. Console: 0 errors, 0 progress warnings (1 unrelated Unity "Account API" warning).
- Real-file check in the Editor (two Play sessions): session 1 collected the coin and wrote
  `{"version":1,"totalGold":10,"collectedTreasureIds":["green_mine.gold_coin_01"],"unlockedAreaIds":["green_mine"],"completedAreaIds":[]}`;
  session 2 reported `Loaded`, total 10, coin already Collected, HUD `10 / 500 GOLD`, no DIG at the coin. The test save was deleted afterwards.
- The older intermittent Player tests passed again; their cause is still NOT proven (see Known Issues).

### Stage 6 limitations (not verified / open)
- AREA COMPLETE / NEW AREA UNLOCKED exist as EVENTS (+ message constants) only: no banner/screen yet (Area Selection etc. are later stages).
- Only `GameplayTest` (green_mine) exists; Rocky Canyon / Ancient Mine scenes do not, so locks are tested with `SetAreaId` on the same scene.
- Persistence not verified on a real Android device (`persistentDataPath`, app pause/kill); no cloud save; the file is plain JSON (not tamper-proof).
- Fallback treasure ids are position based and change if a spot is moved: give every placed treasure an explicit unique `spotId`.
- If the catalog thresholds are edited after players saved, area flags are recomputed from the saved gold on load (repair warning, no notifications).

## Stage 5 (merged to `main`, PR #5): Dig animation & feedback
Goal: DIG becomes a short dig experience (Detect -> DIG -> [dig 0.45 s: motion + dirt + sound] -> Reveal -> Collect -> Gold reward) with
no new treasure system; `TreasureSpot` (Hidden -> Revealed -> Collected), HUD, rarity reward feedback and SafeAreaFitter are unchanged.

Design / impact analysis: `TreasureSpot.Reveal()` and the HUD API stay synchronous and unchanged. The dig phase lives in
`TreasureHuntController`. Consequence: every test that pressed DIG and asserted `Revealed` one frame later had to wait for the reveal.

Code (`Assets/Scripts/Treasure/`):
- CHANGED `TreasureHuntController` - new `digDuration` (default 0.45 s, 0 = instant like before), `IsDigging`, `DigStarted(spot, seconds)`,
  `DigCompleted(spot)`, `DigCancelled(spot)`. While digging a second DIG is ignored and the DIG button is visible but disabled; the treasure is
  revealed when the timer ends, then COLLECT appears. The target is LOCKED at DIG; the player may move while digging but must stay inside the
  target's dig radius: leaving it cancels the dig (treasure stays Hidden, no reveal, no gold, no COLLECT, timer reset) and DIG is usable again
  as soon as the player is back in range. A revealed/collected treasure cannot be dug again. (Exact radius, no grace margin.)
- NEW `PlayerDigAnimator` (+ `Cancel()`: stops the motion and restores the rest pose at once; `CancelCount`) - placeholder dig motion (2 strokes: forward tilt + side roll + dip + squash) applied ONLY to a child `Visual`
  (never the player root, so Transform / CharacterController / camera follow are untouched); restores the exact rest pose when finished or
  disabled. Assign an `Animator` (+ trigger `Dig`) to switch to a real miner animation later; `visual` can point at the miner model.
- NEW `DigDirtEffect` - ONE reusable ParticleSystem, earth-tone dirt (light/dark brown), 8 particles per stroke + 14 on reveal (30 per
  dig, hard cap), wide cone, size 0.2-0.32 so it stays visible next to/around the player; opaque placeholder material (swap for final art).
- NEW `DigSoundPlayer` - ONE AudioSource (a new play restarts, no stacking, 0.05 s repeat guard); procedural placeholder thud (no audio
  files); assign `digClip` for a real sound; one sound per stroke (2 per dig).
- NEW `DigFeedbackPresenter` - listens to `DigStarted` / `DigCompleted` / `DigCancelled`; fires animator, dirt and sound at each stroke
  impact ((k+0.5)/strokes of the dig) and a final dirt puff on reveal; on cancel it stops the animator (no reveal puff, no more strokes).
Assets / scene:
- CHANGED `Assets/Prefabs/Player/Player.prefab` - the capsule `MeshFilter`/`MeshRenderer` moved from the root to a new child `Visual`
  (same mesh/material/shadow settings); `PlayerDigAnimator` added on the root. Nothing else referenced the root renderer; scene
  instances only override the transform.
- NEW `Assets/Materials/DigDirt_Placeholder.mat` (URP Particles/Unlit, vertex colours).
- CHANGED `GameplayTest.unity` (additions: `DigFeedback` object with presenter + dirt + sound + AudioSource; plus a harmless TMP
  style-hash re-serialization).
Tests:
- NEW `Assets/Tests/PlayMode/DigFeedbackPlayModeTests.cs` (9 tests; the last four cover the DIG-only isolation and leave-range cancel,
  see "Follow-up" below). Original 5: stroke curve maths; animator returns visual to rest when disabled
  mid-dig; DIG only inside dig range; full sequence (feedback once, repeated DIG blocked, DIG visible-but-disabled, COLLECT hidden until
  reveal, reveal after >= dig time, player root position/rotation untouched, camera follows, visual moved and restored, 2 strokes /
  2 sounds / 30 dirt particles, alive particles, earth tones, dirt at the spot, collect gives gold once, collected treasure cannot be
  dug again, reward text `+10 Gold`); Rare treasure keeps `RARE! +50 Gold`, burst count and glow.
- CHANGED `TreasureHuntPlayModeTests.cs` (3 sites) and `RewardFeedbackPlayModeTests.cs` (1 site): wait for the reveal after DIG.

### Test results (real, stage 5)
- Compile: 0 errors, 0 warnings. Targeted `DigFeedbackPlayModeTests`: 5/5 PASSED (first run).
- Full PlayMode suite, ONE run: **46/46 PASSED**. Console after: 0 errors, 0 warnings. The older intermittent Player tests passed
  again but their cause is still NOT proven (see Known Issues); green runs do not resolve them.
- Manual Game View check (Editor, slow motion 0.035x, real scene, coin directly ahead of the player = worst case for occlusion):
  the Player visibly dips and leans/rolls per stroke and returns upright; a cloud of light/dark brown dirt is clearly visible on both
  sides of the capsule and rises/shrinks over ~0.9 s; the treasure is revealed and the reward text `+10 Gold` follows. (First capture
  with smaller particles showed only ~5 specks - sizes/cone were increased and the prefab/scene values updated.)
  Aside: in that run the end state was `Collected` although the script only pressed DIG - see the follow-up below (not reproduced).

### Follow-up in stage 5: DIG-only isolation and cancel on leaving the dig range
1. **Unexpected `Collected` after only DIG - NOT reproduced.** Static review: the only path to `TreasureSpot.Collect()` is
   `TreasureHuntController.HandleCollectRequested`, subscribed only to `HudController.CollectRequested`, raised only by the CollectButton `onClick`;
   no persistent onClick listeners exist in the prefab/scene. Instrumented Play Mode repro (same procedure as before: coin ahead, slow motion
   0.035x, script presses only DIG, with stack-trace logging on both buttons and on `TreasureCollected`): ended `Revealed`, gold 0, exactly one
   click logged (DIG, programmatic, nothing selected), no COLLECT click, no collected event. NEW isolated test
   `Scene_DigOnly_RevealsWithoutCollecting_AndNeverAutoCollects` (Hidden -> Digging -> Revealed, gold unchanged, no `TreasureCollected`, no reward,
   COLLECT available, still Revealed after 2 s, then COLLECT -> Collected, gold 10 once, reward once, repeats safe) passes. Conclusion: no code
   evidence of an automatic collect; the earlier `Collected` was most likely a manual click on COLLECT (not confirmed). No code was changed for it.
   Note for UX: DIG (disabled during the dig) and COLLECT occupy the same spot, so a human double-tap on DIG can land on COLLECT at the reveal.
2. **Leave-range cancel implemented** (see controller bullet). New tests: `Scene_PlayerMovingInsideDigRange_DigStillCompletes`,
   `Scene_PlayerLeavingDigRange_CancelsDigSafely_AndDigCanBeRepeated` (cancel, Hidden, no gold/COLLECT/reward, animation at rest, no late
   reveal after the dig time, DIG usable again, repeated DIG ignored, second dig completes, COLLECT gold once + `+10 Gold` once),
   `Animator_Cancel_RestoresRestPose_AndIsIdempotent`; the earlier tests also assert the pose is restored after a normal finish.
3. Results: `DigFeedbackPlayModeTests` **9/9 PASSED**; full PlayMode suite ONE run **50/50 PASSED**, Console 0 errors / 0 warnings. Manual
   real-movement check in the Game View (slow motion, injected S key, Editor focused): dig started 2.1 m from the coin, walking past 2.5 m
   cancelled it once (`DigCancelled`), treasure Hidden, animation at rest, DIG hidden, no COLLECT, gold 0, no completed/collected events.
   The cancel pose-reset was verified numerically (`IsAtRest`), not by a dedicated screenshot.

### Stage 5 limitations (not verified / open)
- **Dig sound audibility: NOT yet tested by a human** (only that it is triggered twice per dig, once per stroke). Not a claim of silence.
- Dig visuals were checked in the Editor Game View only; look and performance on a real Android phone are NOT verified. Motion is a
  capsule placeholder (no miner model/Animator yet); dirt particles are plain squares.
- Player was driven by teleport/injected key events in tests; a physical keyboard and the on-screen joystick during a dig were not exercised
  (joystick tests from earlier stages still pass). Movement is not locked during the 0.45 s dig; leaving the dig radius cancels it.
- While digging, the DIG button stays visible but disabled (a design choice; hiding it would also work).

## Manual Play Mode investigation (merged to `main`, PR #4) - HUD vanished / WASD ignored
See `Docs/KnownIssues.md` ("Manual Play Mode report") for the full evidence. Summary:
- Console of the user's manual run: `Invalid AABB inAABB` (TMP) + `IsFinite(gain)` (PlayOneShot in `DetectorSignalIndicator.HandlePulse`).
  Both reproduced exactly by emulating `SafeAreaFitter`'s unguarded division at screen size 0 (NaN anchors -> NaN HUD rects / NaN audio position).
- CODE CHANGE: `Assets/Scripts/UI/SafeAreaFitter.cs` - new `TryComputeAnchors(safeArea, screenSize, out min, out max)` rejects zero/negative/NaN/
  Infinity input; `ApplySafeArea` keeps current anchors and retries next frame. NEW `Assets/Tests/PlayMode/SafeAreaFitterPlayModeTests.cs` (9 cases).
- WASD: NOT a code bug. Input System default routes keyboard to the Editor when the Game View is unfocused (reproduced: focused -> moves,
  unfocused -> no movement). Click inside the Game View once after entering Play Mode.
- NOT proven: what made the screen size degenerate in the user's run, and why the `UI Canvas` looked greyed (no script deactivates it).
- Verified after the fix: manual Play Mode (HUD visible, W/D move the Player, dig/collect -> `+10 Gold`, `10 / 500 GOLD`, fill 0.02, Console 0 errors);
  `SafeAreaFitterPlayModeTests` 9/9; full PlayMode suite ONE run **41/41 PASSED**, Console 0 errors / 0 warnings. The old intermittent Player tests
  are still unresolved in principle (see Known Issues) even though they passed in this run.

## Done on `main` (do not recreate)
- Stage 1: Player / Camera / `VirtualJoystick` / `HudController` (+ `SafeAreaFitter`), prefabs `Player` / `PlayerUI`, scene
  `Assets/Scenes/Tests/PlayerTest.unity`, `PlayerSystemsPlayModeTests`.
- Stage 2: Treasure MVP - `TreasureSpot` (Hidden -> Revealed -> Collected), `TreasureDetector`, `TreasureHuntController`
  (raises `TreasureCollected(spot, reward)` once per successful collect, after the HUD gold update), `DetectorSignalIndicator`,
  scene `Assets/Scenes/GameplayTest.unity` (single-treasure TECHNICAL PROTOTYPE, not the main game scene; HUD `0 / 500 GOLD`).
- Stage 3: `TreasureRarity`, `TreasureFeedbackProfile`, extended `TreasureDefinition`; four definitions (Gold Coin Common 10,
  Gold Nugget Uncommon 25, Gold Bar Rare 50, Ancient Artifact Epic 100), four profiles, PLACEHOLDER primitive models
  (final art = Sibel), spot prefabs, data tests.

## Stage 4 (merged to `main`, PR #4): Rarity reward feedback
Goal: when a treasure is collected the player clearly sees and hears the reward; look/feel comes from the feedback PROFILE data.

Code (`Assets/Scripts/Treasure/`):
- NEW `RewardFeedbackPresenter` - subscribes to the EXISTING `TreasureHuntController.TreasureCollected`; builds the
  message from the profile and triggers text + burst + sound; ignores a spot it already rewarded. No second HUD / gold manager.
- NEW `FloatingRewardText` - UI text (pop-in, rise, fade), non-raycast (cannot block joystick/swipe). Duration and scale =
  Lerp(min,max, profile `effectIntensity`).
- NEW `CollectBurstEffect` - one reusable ParticleSystem (count 10..36 and speed from intensity; particles shrink instead of
  alpha-fading, opaque material) + a short point-light glow from profile `glow`. A profile's `collectParticlesPrefab` replaces
  the default particles (modular for Sibel).
- NEW `CollectSoundPlayer` - ONE AudioSource; a new play restarts instead of stacking; 0.05 s repeat guard; procedural
  placeholder chime (no audio files); pitch from profile `soundPitch`; a profile `collectSound` clip replaces the chime.
- CHANGED `TreasureFeedbackProfile` - added `glow` (0..1) and `soundPitch` (0.5..2). Profile values: Common glow 0 / pitch 1.0 /
  intensity 0.25; Uncommon 0 / 1.12 / 0.5; Rare 0.6 / 1.26 / 0.75; Epic 1.0 / 1.5 / 1.0 (placeholders for tuning).
- CHANGED `DetectorSignalIndicator` - pulse STRENGTH scales with proximity (weak far, stronger mid, strongest close): flash
  brightness, dot scale pulse and beep volume; `LastPulseStrength`, static `StrengthFor`. Distance/interval logic
  (`TreasureDetector`) NOT changed.
- NOT changed: `TreasureHuntController`, `TreasureDetector`, `TreasureSpot`, `TreasureDefinition`, `HudController`, Player code.

Assets / scene:
- NEW `Assets/Materials/CollectBurst_Placeholder.mat` (URP Particles/Unlit; placeholder square particles, not final art).
- CHANGED the four `Assets/Data/Treasures/Feedback/Feedback_*.asset` (glow + pitch values above).
- CHANGED `GameplayTest.unity` (additions only, 9 root objects now): `RewardFeedback` (presenter + burst + sound + AudioSource,
  all references assigned) and `UI Canvas/SafeAreaRoot/RewardText` (FloatingRewardText + TMP + CanvasGroup).
Tests:
- NEW `Assets/Tests/PlayMode/RewardFeedbackPlayModeTests.cs` (4 tests): effect scaling driven by profile data and growing with
  rarity (particle cap respected, distinct colours, glow only Rare/Epic); signal strength mapping; full scene test for all four
  rarities (messages `+10 Gold`, `UNCOMMON! +25 Gold`, `RARE! +50 Gold`, `EPIC! +100 Gold`, gold 10/35/85/185, HUD `185 / 500 GOLD`,
  text colour/scale/duration, particle count, glow, sound triggered with profile pitch, no second reward, text hides again);
  detector scene test (strength far < mid < near, interval logic still speeds up, treasure state untouched).
- CHANGED `TreasureHuntPlayModeTests.cs` - the flash check now accepts the weaker flash colours of the new strength scaling.

### Gold Progress Bar fix (merged to `main`, PR #4)
- Root cause: `GoldProgressFill` (Image, Filled / Horizontal) had NO sprite, so Unity drew the full bar regardless of
  `fillAmount` (HUD text and `fillAmount` were correct). Confirmed in the Editor on the prefab, and reproduced in Play Mode.
- Fix: NEW `Assets/UI/Sprites/WhiteSquare.png` (+ `.meta`, 8x8 white, Sprite) assigned to `GoldProgressFill` in
  `Assets/Prefabs/UI/PlayerUI.prefab` (one-line prefab diff). `HudController` API unchanged. Scenes do not override it.
  (First tried the built-in rounded `UISprite`: filled but tapered into a sliver on the thin bar -> replaced by the plain sprite.)
- Verified on screen (Game View pixel measurement vs. the 500/500 width): 0 / 500 -> 0.0 %, 10 -> 1.9 %, 100 -> 19.8 %,
  250 -> 50.0 %, 500 -> 100 %.
- NEW test `Assets/Tests/PlayMode/HudProgressBarPlayModeTests.cs` - instantiates the PlayerUI prefab and checks the fill has a sprite
  and that the GENERATED MESH width equals gold/500 at 0, 10, 100, 250, 500. Shown to fail on the old sprite-less setup
  (fillAmount 0.20 but drawn 100 %).

### Test results (real, stage 4 + progress bar)
- Compile: 0 errors, 0 warnings. (One compile error on the way: `Object.GetInstanceID()` is obsolete in Unity 6.6 - fixed by
  tracking spots in a `HashSet<TreasureSpot>`.)
- Targeted runs: reward feedback tests 4/4 PASSED; progress bar test 1/1 PASSED.
- Full PlayMode suite, ONE run (latest): **32/32 PASSED**. Console after: 0 errors, 0 warnings. NOTE: the older intermittent Player test
  (`PlayerStaysOnGround_WhileWalkingWithKeyboard`, see `Docs/KnownIssues.md`) passed in the last two full runs, but its cause is still
  NOT proven and green runs do not resolve it; the earlier 26/27 run remains on record.
- Manual Play Mode visual check (slow motion 0.1x, real scene, screenshots): all four rarities showed the right floating text
  (`+10 Gold` gold, `UNCOMMON! +25 Gold` green, `RARE! +50 Gold` blue, `EPIC! +100 Gold` purple; size grows with rarity) and the HUD bar
  filled to 2 / 7 / 17 / 37 % for 10 / 35 / 85 / 185 gold. Particle counts 16 / 23 / 30 / 36; glow only Rare (2.4) and Epic (4.0);
  sound triggered once per reward with pitch 1.00 / 1.12 / 1.26 / 1.50. An EventSystem raycast at the reward text position hit
  NOTHING and the joystick still received its raycast (the text does not block UI).
- Visual findings (NOT fixed, no redesign): the particle burst is VISIBLE but SUBTLE - small square placeholder particles that shrink
  away (Common = a few yellow specks, Rare = a wider blue spread). When the treasure is directly ahead of the player the third-person
  camera sees the early burst mostly hidden behind the player capsule (Epic/Rare show a coloured halo/ring around the capsule). Idea
  for later: spawn the burst higher / toward the camera, softer round particle sprite (final art by Sibel).

## NOT tested / known issues
- **Sound audibility: NOT yet tested by a human** (only that it is triggered once with the profile pitch). This does NOT mean the
  sound is silent - it just has not been listened to. Same for the detector beep. See `Docs/KnownIssues.md`.
- Effects were checked in the Editor Game View only (slow motion screenshots); how they look and perform on a real phone is NOT
  verified. Particles are plain placeholder squares; models are placeholder primitives. Performance is only bounded by design
  (<= 36 particles per burst, one point light <= 0.3 s, text <= 1.5 s), not profiled.
- Real Android device testing (touch, performance, audio) not done.
- HUD progress bar: FIXED on this branch (see above and `Docs/KnownIssues.md`); the bar is still a flat orange rectangle (no animation / final art).
- Intermittent test failures (see `Docs/KnownIssues.md`): `TwoTouches_JoystickMovesPlayer...` (Editor unfocused) and
  `PlayerTestScene_PlayerStaysOnGround_WhileWalkingWithKeyboard` (failed once in a full run, passed in isolation and in the latest
  full run). Cause unproven: do NOT call them verified gameplay bugs or definite test-environment errors.
- HUD title still "GOLD COIN TEST"; `HudController.SetGold` clamps to the level target (500); targets of later areas are
  cumulative, so a persistent treasury is needed before area 2. `GameplayTest` is not in Build Settings (only SampleScene).

## Concept Document v1.0 check for stage 4 (2.6 Atmosphere, 2.7 Game Play, 2.8 Key Features)
- 2.6: collection uses a lightweight particle burst, collect sound, floating gold text (done); rare rewards get stronger but
  mobile-friendly feedback (intensity/glow/pitch grow with rarity, particle count capped at 36); calm, short effects (<= 1.5 s);
  warm gold/bright glow accent colours come from profiles. Dig animation/dirt particles/dig sound and the counter animation are NOT done.
- 2.7: "RARE! +50 Gold"-style rarity feedback shown (done); detector indicator now gets stronger when closer (weak far, stronger mid,
  strongest very close) in addition to faster beeps. Optional haptics NOT done.
- 2.8: lightweight audio/particle/floating-text feedback for mobile (done at placeholder level). Still Android-targeted, third-person,
  no combat/enemies/online/complex economy. Main Menu, Area Selection, persistent treasury, Green Mine and other areas: NOT in this stage.

## Concept Document v1.0 check for stage 5 (2.6 Atmosphere, 2.7 Game Play, 2.8 Key Features)
- 2.6: "Digging uses a short animation, dirt particles, and a sound cue" - DONE at placeholder level (0.45 s animation, earth-tone dirt, dig
  sound; low particle count for mobile). Collection feedback / rarity feedback unchanged.
- 2.7: "At the valid position, the contextual Dig button appears. Digging reveals the treasure; the button then changes to Collect." - kept
  (DIG in range -> dig phase -> reveal -> COLLECT). Short interaction, no failure state. Optional haptics NOT done.
- 2.8: lightweight audio/particle feedback for mobile, single-player Android, third-person; no combat/enemies/online/economy added.
- Not in this stage by decision: Green Mine map, Main Menu, Area Selection, other areas, final miner model/Animator, persistent treasury.

## Concept Document v1.0 check for stage 6 (2.7 Game Play, 2.8 Key Features)
- 2.7: gold accumulates and is visible as progress ("320/500 GOLD"-style HUD, now `total / current area goal`); "Reaching an area's target displays
  Area Complete and New Area Unlocked" - the EVENTS exist (UI/Area Selection later); targets 500 / 1200 total / 2500 total and unlock thresholds
  implemented as data; "leaving an area does not erase the treasury progress" - gold is persistent and cumulative.
- 2.8: "Gold treasury, ..., Area Complete feedback, and area unlocking" - treasury + unlocking done at data/logic level; offline, single player.
- Not in this stage by decision: Main Menu, Settings, Character Selection, new maps / Area Selection, notification UI.

## Concept Document v1.0 check for stage 7 (2.4 Introduction, 2.6 Atmosphere, 2.7 Game Play)
- 2.7 "The game opens to a simple main menu" - done (Area Selection, which follows it in the document, is the next stage). Virtual joystick left / swipe right and the HUD
  are unchanged; the new PAUSE button sits top-left and no overlay covers the touch controls during play.
- 2.6 / 2.5 style: stylized, colourful 3D, warm earth + green + rock tones with brighter gold; calm, curious, rewarding tone; Android landscape. 2.7 "Reaching an area's
  target displays Area Complete and New Area Unlocked" - the banners now show them. No combat / enemies / online / economy added.
- Not in this stage by decision: Area Selection, Character Selection, final art, haptics/vibration.

## Next steps (suggested, in order)
1. Decide on PR #6, which now carries stages 6 and 7 together (update its description if it is merged as is).
2. Area Selection (reads `ProgressService.IsAreaUnlocked/IsAreaCompleted`, `AreaCatalog`) between the Main Menu and gameplay; then Green Mine scene (Beyza) and other areas.
3. Real-device check on Android: touch on buttons/sliders, Back key, landscape lock, audio levels, save/load, performance.
4. Optional polish: vibration setting together with haptic feedback, final art/models/music/SFX (Sibel), notification sound.
