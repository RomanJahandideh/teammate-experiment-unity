# Multiplayer + physiological-cue system

This adds the piece that was missing from this project: connecting players together
over the network and replicating each player's classified HR/HRV state to their
teammates, so the Icon Cue / Numeric Cue / No Cue halo system from "Reading a Remote
Teammate's Body" actually runs live instead of existing only as the paper's static
mockup renders (`Paper_Render_TopDown.unity`).

It builds on the same UDP-bridge pattern already used for the Apple Watch connection in
the BioTerrarium project (`806UnityProject/Assets/Script/UDPHeartRateReceiver.cs`), and
logs to the exact CSV column layout already used in
`study_analysis/study2/Physio_System_Log.csv`, so a live session's log drops straight
into the existing analysis pipeline.

## What's included

- **`Physio/`** — per-player, local-only pipeline: Apple Watch UDP bridge →
  baseline calibration → HR1-4/V1-4 classification (paper's exact thresholds) →
  debounce/persistence (`StateDebouncer`, ~3 consecutive 1 Hz samples).
- **`Networking/`** — Netcode for GameObjects (NGO) layer: `TeammateNetworkBootstrap`
  (host/join a LAN session, round-robin Prep/Cook/Runner role assignment),
  `TeammatePhysioNetworkPlayer` (replicates only the two classified state enums, never
  raw bpm/RMSSD), `PlayerMotor` (top-down WASD movement + an Interact action, the
  "position and action data" the paper says the cue rides alongside).
- **`Interface/`** — `ConditionManager` (server-authoritative No Cue / Numeric Cue /
  Icon Cue switch, keys 1/2/3), `TeammateHaloRenderer` (pulse+glow from HR state, ring
  coherence from HRV state), `TeammateStatusWidget` (binds one teammate's panel entry to
  a spawned player and the active condition).
- **`Gameplay/`** — the cooking loop: `IngredientSource` → `ChoppingStation` →
  `CookPot` (can burn) → `PlatingCounter` → `DeliveryCounter`, all server-authoritative
  (`TimedStationBase` is the shared place/wait/collect logic behind chopping and
  cooking), plus `OrderQueue` (spawns/expires orders, scores deliveries) and
  `PlayerStationInteractor` (client-side: finds the nearest station and fires it from
  `PlayerMotor`'s Interact key).
- **`Mission/`** — `MissionController` (7-minute mission timer, starts/stops the order
  board, aggregates everything into one behavior-log row), `PressureEventScheduler`
  (fires Ingredient Bottleneck / Pot Overload / Service Rush at the paper's ~2:00 / ~4:30
  / late-phase offsets and measures support response + recovery), `IdleTracker` /
  `CollisionTracker` (secondary behavioral measures).
- **`Logging/`** — `PhysioSessionLogger` (per-player, matches `Physio_System_Log.csv`),
  `MissionBehaviorLogger` (per mission, matches `Mission_Behavior_Log.csv`),
  `PressureEventLogger` (per pressure event, matches `Pressure_Event_Log.csv`) — all
  three write the exact column layout already used by
  `study_analysis/study2/*.csv`, so a live session's logs drop straight into the existing
  analysis pipeline.

## What's deliberately NOT included

- **The three kitchens' physical layout.** Placing `IngredientSource` / `ChoppingStation`
  / two `CookPot`s / `PlatingCounter` / `DeliveryCounter` props around the
  `AssetHunts! GameDev Starter Kit - Cooking` art for the Shared Congestion, Divided
  Pass, and Bottleneck Route kitchens is level-building in the Editor, not something a
  script can do. The station components are ready to drop onto any prop; only the
  arrangement is left.
- **Fine-grained action classification.** The original study's `support_action_type`
  ("Reprioritize order", "Plate dish", etc.), `verbal_help_request_before_support`, and
  `physio_cue_referenced` columns were coded by a researcher reviewing video/audio.
  `PressureEventLogger` writes everything objectively measurable from telemetry
  (timestamps, response time, recovery time, burned/failed, which player responded) and
  leaves those three columns blank for manual review, same as the existing CSVs leave
  `researcher_initials`/free-text `notes` blank for hand entry.

## Setup in the Editor

1. **Packages** — `Packages/manifest.json` now lists `com.unity.netcode.gameobjects`
   and `com.unity.transport`. Open the project so Package Manager resolves them (or add
   them manually via Window → Package Manager → "+" → *Install package by name* if the
   pinned versions don't resolve — grab the latest 2.x of each). If any `TMPro` script
   errors on first open, Unity will prompt to import TMP Essentials — accept it.

2. **NetworkManager** — create an empty GameObject `NetworkManager` in the scene, add
   `NetworkManager` and `Unity Transport` components, then add `TeammateNetworkBootstrap`
   next to it and assign its `Player Prefab` field once you've built the prefab below.

3. **Player prefab** — new GameObject with: `NetworkObject`, `CharacterController`,
   `NetworkTransform` (set its authority mode to owner-authoritative in the Inspector —
   NGO 2.x exposes this directly on the component; older samples call the same role
   `ClientNetworkTransform`), `PlayerMotor`, `PlayerCarrier`, `PlayerStationInteractor`,
   `AppleWatchPhysioReceiver`, `BaselineCalibrator`, `LocalPhysioController` (assign its
   `Baseline Calibrator` field), `TeammatePhysioNetworkPlayer`, `RoleSwitchInput`,
   `IdleTracker`, `CollisionTracker`, `PhysioSessionLogger`.
   Give each participant's build/machine a distinct `AppleWatchPhysioReceiver.port` if
   more than one client ever runs on the same machine/NIC.

3b. **Kitchen stations** — for each prop from the cooking asset pack that should act as a
   station, add a `NetworkObject`, a trigger `Collider` sized to the interact radius, and
   the matching script: `IngredientSource` (set `suppliedType`), `ChoppingStation`,
   `CookPot` (add two per kitchen — Pot Overload needs a pair), `PlatingCounter` (assign
   `availableRecipes`), `DeliveryCounter` (assign its `orderQueue`), or `TrashCan`. Put
   them on a dedicated layer and point `PlayerStationInteractor.stationLayerMask` at it.

3c. **Recipes & order board** — create one or more `DishRecipe` assets (Assets → Create →
   Teammate → Dish Recipe), assign the same list to `OrderQueue.recipePool` and every
   `PlatingCounter.availableRecipes`. Add a single `OrderQueue` (with `NetworkObject`) to
   the scene per kitchen layout.

3d. **Mission control** — add one `MissionController` (`NetworkObject`) wired to that
   `OrderQueue`, a `PressureEventScheduler` (wired to the same `OrderQueue` and the
   kitchen's two `CookPot`s as `potA`/`potB`), and one `MissionBehaviorLogger` +
   `PressureEventLogger` (plain `MonoBehaviour`s, host machine only). Drive a mission from
   an experimenter script/button with, e.g.:
   `missionController.BeginMission(1, DisplayCondition.NoCue, "L1", "Shared Congestion Kitchen");`
   — matching that block's row in your Latin-square running-order sheet
   (`Mission_Assignments.csv`). `AbortMission()` ends it early if a session needs to stop.

4. **Team status bar UI** — one `TeammateStatusWidget` per teammate slot, each with a
   No Cue view (empty/hidden), a Numeric Cue view (TMP text), and an Icon Cue view
   (halo: one core glow `Image` + a ring — either hand-place segment `Image`s under a
   ring root, or assign `segmentPrefab`/`ringRoot` and let `TeammateHaloRenderer`
   auto-build the ring). Call `widget.Bind(player)` when a `TeammatePhysioNetworkPlayer`
   spawns for that slot (e.g. from `NetworkManager.OnClientConnectedCallback`).

5. **Condition control** — add a GameObject with `NetworkObject` + `ConditionManager` to
   the scene (a network-visible scene object; only the host/server actually needs to act
   on it, but every client needs one to read `CurrentCondition` from). During a session,
   press 1/2/3 on the host machine to switch the whole team between No Cue / Numeric Cue
   / Icon Cue between mission blocks.

6. **Running a 3-player session** — host machine: press *Host* in the on-screen panel
   (`TeammateNetworkBootstrap`'s `OnGUI`). Both other machines: type the host's LAN IP
   (shown on the host's panel once hosting starts) and press *Join*. All three must be on
   the same LAN/Wi-Fi.

## Privacy note carried over from the paper

`TeammatePhysioNetworkPlayer` only ever puts `HRState`/`HRVState` enums on the network
by default. The raw bpm/ms `NumericHrBpmIfDisclosed`/`NumericHrvMsIfDisclosed` fields
only get a non-zero value when you explicitly call
`PublishNumericIfDisclosed(true, hr, hrv)` — wire that call to fire only while
`ConditionManager.CurrentCondition == NumericCue`, so the numeric arm is the one
condition that discloses exact values, by design, not by default.
