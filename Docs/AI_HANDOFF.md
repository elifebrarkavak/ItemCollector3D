# AI Handoff (Codex <-> Claude Code)

Rules for whichever agent picks this up:
1. Read this file AND run `git status` / `git log` before starting; trust Git over this file if they disagree.
2. Only ONE agent may use the Unity Editor / edit project files at a time. If `git status` shows uncommitted
   changes you did not make, check "Current state" below before touching them.
3. Do not rebuild systems listed as done. No commit / push / merge without the user's explicit permission.
4. No long test-repeat loops. One quick run is enough. Never write PASS for a test you did not run.
5. No new packages without asking. Update this file when you finish or stop (even if the task is half done).

## Current state (2026-11-16)
- Branch: `feature/rarity-feedback`. Commit `2f781bb` (rarity reward feedback + gold progress bar fix) and the follow-up commit
  `fix: guard safe area layout against invalid screen dimensions` (SafeAreaFitter hardening + tests + docs) are pushed; PR #4 into `main` is OPEN
  (not merged). Nothing is uncommitted at the time of writing (see `git log` for hashes).
- Last agent: Claude Code. Unity Editor left with Play Mode off and `GameplayTest` open, not dirty.

## Manual Play Mode investigation (after PR #4) - HUD vanished / WASD ignored
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

## Stage 4 (this branch, uncommitted): Rarity reward feedback
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

### Gold Progress Bar fix (also uncommitted on this branch)
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

## Next steps (suggested, in order)
1. Merge the stage 4 PR only when the user decides to. The particle occlusion / subtle-particle item is logged in `Docs/KnownIssues.md`.
2. (Done on this branch) HUD progress bar sprite fix. Optional polish: animated fill, final art.
3. Dig feedback (short dig animation, dirt particles, dig sound) and counter animation; optional haptics.
4. Area Complete / New Area Unlocked + persistent treasury; Main Menu and Area Selection.
5. Green Mine scene (Beyza) and other areas; final models/particles/audio (Sibel) via profile overrides.
6. Android build, add real scenes to Build Settings, device test (touch, audio, performance).
