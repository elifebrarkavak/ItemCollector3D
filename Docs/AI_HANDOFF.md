# AI Handoff (Codex <-> Claude Code)

Rules for whichever agent picks this up:
1. Read this file AND run `git status` / `git log` before starting; trust Git over this file if they disagree.
2. Only ONE agent may use the Unity Editor / edit project files at a time. If `git status` shows uncommitted
   changes you did not make, check "Current state" below before touching them.
3. Do not rebuild systems listed as done. No commit / push / merge without the user's explicit permission.
4. No long test-repeat loops. One quick run is enough. Never write PASS for a test you did not run.
5. No new packages without asking. Update this file when you finish or stop (even if the task is half done).

## Current state (2026-11-16)
- Branch: `feature/rarity-collectibles` (from `origin/main` @ `b88bd18`). Stage 3 is COMMITTED and PUSHED to
  `origin/feature/rarity-collectibles` (message `feat: add collectible rarity tiers and treasure data`; hash: see `git log`).
  No Pull Request opened yet; not merged. `main` already contains stages 1-2 (Player/Camera/UI + Treasure MVP).
- Last agent: Claude Code. Status: stage 3 (rarity & collectible data) implemented, checked and pushed. Next: user decides
  on a PR. Unity Editor left with Play Mode off and `GameplayTest` open, not dirty.
- Team branches `beyza`, `emre`, `sibel` were fast-forwarded to `main` (`b88bd18`) at the start of stage 3.

## Done on `main` (do not recreate)
- Stage 1: Player (`ThirdPersonPlayerController`), Camera (`ThirdPersonCameraController`), `VirtualJoystick`,
  `HudController` (+ `SafeAreaFitter`), prefabs `Player` / `PlayerUI`, scene `Assets/Scenes/Tests/PlayerTest.unity`,
  tests `Assets/Tests/PlayMode/PlayerSystemsPlayModeTests.cs`.
- Stage 2 (Treasure MVP, single Gold Coin): `Assets/Scripts/Treasure/` (`TreasureDefinition`, `TreasureSpot`,
  `TreasureDetector`, `TreasureHuntController`, `DetectorSignalIndicator`), scene `Assets/Scenes/GameplayTest.unity`
  (single-treasure TECHNICAL PROTOTYPE, not the main game scene; HUD `0 / 500` -> `10 / 500 GOLD`),
  `Assets/Data/Treasures/GoldCoin.asset`, `Treasure_GoldCoin.prefab`, `TreasureHuntPlayModeTests.cs`.

## Stage 3 (this branch, uncommitted): Rarity & collectible data
Goal: reusable data system for the four treasure types of Concept Document v1.0 (2.7):
Gold Coin (Common, 10), Gold Nugget (Uncommon, 25), Gold Bar (Rare, 50), Ancient Artifact (Epic, 100).
No new spot/controller system - the existing `TreasureSpot` / `TreasureDetector` / `TreasureHuntController` are reused.

Code (`Assets/Scripts/Treasure/`):
- NEW `TreasureRarity.cs` - enum Common/Uncommon/Rare/Epic.
- NEW `TreasureFeedbackProfile.cs` - ScriptableObject, DATA ONLY: announcement text, accent colour, effect intensity
  (0..1), optional collect sound / particle prefab (none assigned yet), `FormatCollectMessage(gold)` ->
  "+10 Gold" / "RARE! +50 Gold". The next stage's effects (floating text, particles, sounds) should read this.
- CHANGED `TreasureDefinition.cs` - added `Rarity`, `ModelPrefab`, `FeedbackProfile`, `CollectMessage`,
  `Validate(out error)`. Existing fields/values unchanged (id, name, gold, detect/dig/collect radii; all Inspector-editable).
- CHANGED `TreasureSpot.cs` - if a spot has no visual of its own it instantiates `definition.ModelPrefab` (inactive until
  DIG). Spots with their own visual (the Gold Coin prefab) are untouched. State machine unchanged.
- `TreasureHuntController` / `TreasureDetector` / `DetectorSignalIndicator` / `HudController`: NOT changed.

Assets (all new unless noted):
- `Assets/Data/Treasures/`: `GoldCoin.asset` (UPDATED in place, same GUID: rarity Common + model + profile; id, 10 gold
  and radii unchanged), `GoldNugget.asset`, `GoldBar.asset`, `AncientArtifact.asset`.
- `Assets/Data/Treasures/Feedback/`: `Feedback_Common` (no announcement, 0.25), `Feedback_Uncommon` ("UNCOMMON!", 0.5),
  `Feedback_Rare` ("RARE!", 0.75), `Feedback_Epic` ("EPIC!", 1.0). Values are placeholders for the effects stage.
- `Assets/Prefabs/Treasure/Models/Model_{GoldCoin,GoldNugget,GoldBar,AncientArtifact}_Placeholder.prefab` and
  `Assets/Materials/Treasure_{Nugget,Bar,Artifact}_Placeholder.mat` - PLACEHOLDER primitives (no colliders). NOT final
  art; final models are Sibel's responsibility - swap them by changing `TreasureDefinition.modelPrefab`.
- `Assets/Prefabs/Treasure/Treasure_{GoldNugget,GoldBar,AncientArtifact}.prefab` (spot prefabs). `Treasure_GoldCoin.prefab`
  and `GameplayTest.unity` are unchanged (scene still contains only the Gold Coin).
Tests:
- NEW `Assets/Tests/PlayMode/TreasureDataPlayModeTests.cs` (Editor-only): rarity / gold / id / collect message per type,
  validity + unique ids + feedback intensity grows with rarity, gold grows with rarity, invalid definition fails validation,
  model built from definition + state moves forward only (no double reveal/collect), spot prefabs reference the right
  definition, existing Gold Coin prefab preserved.
- CHANGED `TreasureHuntPlayModeTests.cs` - added `GameplayTestScene_AllFourTreasureTypes_UseSameLoop_GoldAccumulates_NoDoubleCollect`
  (spawns the 3 new prefabs into GameplayTest at runtime; collects all four via the HUD buttons: 10 -> 35 -> 85 -> 185 gold,
  HUD `185 / 500 GOLD`, second dig/collect adds nothing).

### Test results (real, stage 3)
- Compile: 0 errors, 0 warnings after changes.
- Full PlayMode run (one run): **26 of 27 passed, 1 failed**. All 16 treasure tests + the other 7 existing tests passed.
  FAILED: `PlayerSystemsPlayModeTests.PlayerTestScene_PlayerStaysOnGround_WhileWalkingWithKeyboard`
  ("D must move the player right": moved 1.2 m, expected > 3 m). This is an OLDER test in the PlayerTest scene; no
  Player / UI / PlayerTest file was touched in stage 3.
- One isolated re-run of that test: PASSED (1/1). The full suite is therefore NOT fully green: a single-run 27/27 was never
  observed. The cause is NOT proven, so do NOT classify it as a verified gameplay bug OR as a definite test-environment
  error (the test holds D for a fixed 1.5 s of `Time.time` and the run came right after a recompile + asset creation -
  that is only a guess). Logged in `Docs/KnownIssues.md`.
- Pre-commit check (data, no new feature): enum values Common=0..Epic=3; 4 definitions have the right rarity/gold/id,
  matching feedback profile and model prefab (valid mesh + material); spot prefabs link to their placeholder models;
  `GoldCoin.asset` GUID `e81d2e99...` unchanged vs `main` (only 3 fields added); `GameplayTest.unity`, `Treasure_GoldCoin.prefab`,
  controller / detector / indicator / HUD / Player code are byte-identical to `main`; all new assets have `.meta`.
- Console after runs: 0 errors, 0 warnings (the unrelated "Account API did not become accessible" warning appeared once earlier).

## Concept Document v1.0 check for stage 3 (sections 2.4 - 2.9)
- IMPLEMENTED (data level): four rarity tiers (2.4); collectible data = name, rarity, gold value, model, feedback profile
  (2.7); starting values exactly Gold Coin 10 / Gold Nugget 25 / Gold Bar 50 / Ancient Artifact 100, adjustable in the
  Inspector; "RARE! +50 Gold"-style message text (2.7); feedback intensity data grows with rarity (2.6); data-driven /
  reusable collectibles (2.8).
- NOT in this stage (by decision): displaying that message, floating text, particles, collect/dig sounds, dig animation,
  haptics, Main Menu, Area Selection, Green Mine map, other areas, area completion/unlocking, persistent treasury.
- Models are placeholder primitives, not final art. No contradiction with the document found.

## NOT tested / known issues
- Placeholder visuals were not looked at in a screenshot; only their data/behaviour is tested.
- Walking with the on-screen joystick / physical keyboard and real Android device testing: not done.
- Beep audibility never verified by a human. HUD title still "GOLD COIN TEST".
- Intermittent test failures (see `Docs/KnownIssues.md`): `TwoTouches_JoystickMovesPlayer...` (Editor unfocused) and
  `PlayerTestScene_PlayerStaysOnGround_WhileWalkingWithKeyboard` (once in a full run, passed in isolation).
- `HudController.SetGold` clamps to the level target (500); Rocky Canyon / Ancient Mine targets are cumulative totals, so
  a persistent treasury is needed before area 2. `GameplayTest` is not in Build Settings (only SampleScene).

## Next steps (suggested, in order)
1. User reviews stage 3; commit only when the user allows it (suggested: `feat: add treasure rarity and collectible data`).
2. Stage 4 - rarity reward effects: subscribe to `TreasureHuntController.TreasureCollected(spot, reward)`, read
   `spot.Definition.CollectMessage` / `FeedbackProfile` for floating text, particle burst, collect sound, counter animation.
   Keep it mobile-light.
3. Area Complete / New Area Unlocked + persistent treasury; then Main Menu and Area Selection.
4. Green Mine scene (Beyza) with several treasures, then Rocky Canyon / Ancient Mine; final models (Sibel).
5. Android build, add real scenes to Build Settings, device test (touch, Safe Area, performance).
