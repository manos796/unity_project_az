# AGENTS.md

## Purpose

This file is the working memory for this repository.

It exists to help future Codex instances and collaborators:

- understand the repo quickly without re-discovering the same facts
- know which files matter and which do not
- know what depends on what
- know what is safe to change and what is risky
- track current problems, likely future problems, and debugging starting points

This file lives at repo root on purpose so it does not affect Unity asset import.

## Version Control

Git is the canonical VCS for this repository.

Git workflow rules:

- The baseline Git setup intentionally excludes generated Unity/editor-local state.
- The baseline Git setup intentionally excludes redundant archive/export artifacts:
  - `Assets/Robot_stock.zip`
  - `Assets/Exported_packages/Room_custom.unitypackage`
  - `Assets/Exported_packages/Room_custom.prefab`
  - `Assets/Materials/WallTransparent.unitypackage`
- Those excluded archive/reference artifacts are intentionally ignored in `.gitignore` so future `git status` output stays clean.
- `ignore.conf` is treated as historical Plastic reference only and is intentionally not tracked by Git.
- GitHub remote transport should use SSH.
- Review `git status` before staging and before committing.
- Commit and push after every tiny meaningful change.
- This is an explicit project safety rule because the user wants very frequent restore points due to past Unity breakage.
- Do not rely on blind `git add .` unless `git status` has just been reviewed and the repo is cleanly protected by `.gitignore`.
- For Unity asset changes, `.meta` files are mandatory whenever an asset is created, moved, renamed, or otherwise paired with a new or changed asset identity.
- Exception: Unity Bridge runtime I/O files under `Assets/LLM/` are intentionally ignored and untracked even when they have `.meta` files, because they are live tooling state rather than project assets.
- Do not commit:
  - `Library/`
  - `Temp/`
  - `Logs/`
  - `UserSettings/`
  - generated solution/project files
  - `.plastic/`
  - `.vscode/` unless intentionally sharing editor config

## Maintenance Rules

These rules are mandatory for future Codex instances working in this repo:

1. Keep this file concise, high-signal, and current.
2. Update this file after any meaningful architectural or behavioral change.
3. Update this file when:
   - a new script, prefab, scene, package, or pipeline stage is added
   - scene wiring changes
   - dependencies between files change
   - a bug is found, fixed, or partially understood
   - an assumption turns out to be wrong
   - a temporary workaround is introduced
4. Do not bloat this file with raw logs, long transcripts, or low-value history.
5. Prefer stable facts, key inferences, and debugging guidance over narration.
6. Maintain the `Current Problems / Issues` section at all times.
7. When an issue is fixed, move it to `Resolved Notes` with a short reason.
8. If a file has important dependencies or hidden assumptions, document them here and, where useful, add brief code comments in that file too.
9. If a future change may clash with existing systems, record the clash here before or while implementing.
10. Before major changes, read this file first.

## Build Philosophy

The user wants this project built:

- step by step
- modularly
- with code that is explainable, debuggable, maintainable, and improvable
- with the simplest effective solution first
- without oversized or over-engineered first implementations
- with improvements deferred until they are actually needed

Implementation rules derived from that:

- Prefer small, focused scripts over large multi-purpose scripts.
- Make data flow explicit.
- Keep responsibilities separate: room, table, robot control, capture, export, etc.
- Add useful comments only where they clarify dependencies, assumptions, or non-obvious behavior.
- When a file depends on another file or scene object naming convention, say so in code comments where appropriate and record it here.
- Avoid hidden scene magic where possible.
- Prefer deterministic, inspectable behavior over cleverness.

## Communication Rules

These are user preferences and should be followed consistently:

- Be concise in verbal answers.
- Do not write walls of text unless the user explicitly asks for depth.
- It is good to have an opinion when it is grounded in facts and evidence.
- Do not be a people pleaser.
- Be critical in analyses at all times.
- Challenge weak assumptions directly and briefly.

## Repository Snapshot

This is currently a compact Unity 6 URP project focused on the simulation side of a Philips Azurion-style environment.

Current scope in repo:

- clean-room scene geometry
- surgery table geometry
- imported robot model and articulation chain
- robot stability setup for simulation baseline
- Unity Bridge editor tooling for AI-assisted Unity editing

Not yet implemented in this repo:

- Python handoff / reconstruction integration
- 3D fusion / point-cloud generation
- MuJoCo export
- path-planning integration

## High-Level Future Plan

Build order for the MVP pipeline should stay:

1. robot pose control and randomization
2. prop spawning and scene randomization
3. fixed 4-camera sensor rig with additive RGB capture
4. sample orchestration and export

Compatibility boundary:

- The Python repo in `C-ArmSimulation-main/` is the current consumer contract.
- For v1, Unity must keep these legacy outputs readable without Python changes:
  - `depth_metadata.json`
  - `robot_pose.json`
  - `cam{i}_depth.raw`
  - `cam{i}_depth_vis.png`
- New files should be additive, not breaking:
  - `cam{i}_rgb.png`
  - `rgb_metadata.json`
  - `robot_state.json`
  - `scene_objects.json`

Source-of-truth rule:

- Legacy files exist for compatibility.
- Canonical internal truth should move to richer files like `robot_state.json` and `scene_objects.json`.
- Do not let old export shape dictate robot-motion architecture.

Known issues in the old setup that should not be copied blindly:

- duplicated metadata across multiple files
- suspicious joint-orientation semantics in exported robot pose
- ad hoc axis flips and offsets on the Python side
- weak randomization reporting (`requestedProps` vs `placedProps` gives poor debugging value)

Immediate implementation priority:

- first: visually validate and tune the corrected robot joint semantics in Play Mode
- second: keep using manual `Space` stepping until generated scenes look believable
- third: validate and tighten the simple-scene capture/export outputs against the legacy Python contract

Current implementation status:

- robot pose control foundation now exists and is wired into `RobotStabilityBootstrap`
- robot joint semantics are now explicitly mapped to the simplified imported chain:
  - `Long` => rail longitudinal travel
  - `Z1Rot` => dominant FlexArm swing
  - `Z2Rot` => support rotation kept conservative to avoid hinged-arm behavior
  - `Prop` => C-arm angulation-like axis
  - `CArc` => C-arm in-plane rotation
- revolute joints are now sampled/exported in degrees rather than the earlier radian-like guesses
- `RobotJointAuditTool` now exists for one-joint-at-a-time debugging from a parked baseline
- `RobotTableAvoidance` now exists on `RobotStabilityBootstrap`
- table safety is now enforced at the final-pose acceptance layer, not just by steering:
  - `RobotTableAvoidance` now uses a simple rule:
    - if `Long` is over the table zone, force stronger `Z1Rot` side swing
    - keep `Z2Rot` conservative
    - leave `Prop` and `CArc` expressive
  - final acceptance now also checks table clearance against the upper table assembly:
    - `TableTop`
    - `Rail_Left`
    - `Rail_Right`
  - current robot clearance subset is:
    - `Sleeve`
    - `CArc`
  - current implementation uses bounds on those parts, not true collision hulls
- `RobotPoseWorkflow` has been pruned back to the active behavior:
  - Play Mode uses one sampled pose per press
  - no retry loop, diversity gate, or acceptance search remains in the active runtime path
  - edit-time/context-menu application still uses the small validation path
  - final robot-vs-table validation now waits four fixed updates after applying a sampled pose so overlap checks happen on a more settled articulation state
- pose diversity is now intentionally stronger:
  - wider ranges for all movable joints in non-parked poses
  - minimum magnitude sampling now also applies to swing-heavy poses, not just secondary joints
  - manual stepping is now intentionally simplified to `FreeRoomSnapshot` only, with stronger top-joint amplitudes, because the structured left/right profile cycle was too brittle in practice
  - `CArc` has now been clipped down from the broader `[-120, 120]` style tuning to a milder range so it does not over-spin visually
  - `Z2Rot` has been strengthened further because it was under-contributing visually
  - `Z1Rot` remains the dominant side-switch joint, but its range was narrowed slightly so it does not hit huge swings as often
  - `Prop` is again sampled independently; coupling too many joints to one side sign collapsed diversity into a small number of mirrored poses
  - table steering is now intentionally limited to a central longitudinal zone instead of the full table length, because the full tabletop footprint was collapsing most samples into the same forced side pose
  - Play Mode stepping now uses a single sampled pose per press instead of visible retry/rejection loops; the earlier retry path made one keypress look like several wrong movements and introduced noticeable lag
  - if that one sampled Play Mode pose still intersects the table at the final location, the scene is discarded and the next keypress moves on to a new seed instead of repeating the same bad one
  - rejected Play Mode poses now advance the sample index instead of retrying the same bad seed forever
  - final table rejection now also uses a simple C-arm-center distance tolerance, not just renderer-bounds overlap
  - side selection for free-style poses is now random per seed rather than simple alternation
  - `CArc` is clipped more aggressively because diversity should come from the larger transport joints, not from excessive in-place C-arm spin
  - in the current simple-scene layout, positive `Long` moves the robot toward the table; all non-parked profile corridors are therefore biased positive so poses happen near the table instead of only changing arm shape in empty space
  - runtime-first pruning removed the old retry-based acceptance scaffolding so the working motion path is easier to reason about
- prop randomization foundation now exists and is wired into `SceneRandomizationBootstrap`
- overlap safety foundation now exists and is wired into both robot and prop systems:
  - `RobotCollisionRig` builds trigger-only mesh colliders for the moving robot links
  - `RobotOverlapDetector` performs final robot-vs-table and robot-vs-scene overlap checks using collider overlap queries
  - `SceneObstacleRegistry` is now the shared scene truth source for table, room, and spawned-prop colliders
  - `ColliderOverlapUtility` centralizes collider collection, bounds aggregation, and `Physics.ComputePenetration` checks
- prop spawning now uses real candidate objects with real colliders before acceptance:
  - `SpawnValidator` validates actual candidate colliders against room bounds, table, robot, and already spawned props
  - `PropSpawner` no longer validates fake placeholder bounds
  - `PropSpawner.spawnOnStart` is intentionally disabled; `ManualSceneCycleController` is the scene owner
  - `ManualSceneCycleController.generateInitialSceneOnStart` should stay disabled unless explicitly testing auto-start generation
  - `ClearSpawnedProps()` now disables prop colliders before destruction so same-frame respawn validation does not see ghost obstacles from the previous scene
- curated prop archetypes now exist in `PropCatalog`:
  - short rectangular block
  - tall rectangular block
  - tabletop block
  - human blob
  - ceiling light
- an important prop-placement bug was fixed:
  - moved candidates must have physics transforms synced before collider-bounds validation
  - without this, every prop was falsely rejected as `outside_room_bounds`
- fixed 4-camera rig and sample metadata foundation now exist and are wired into `SensorRig`
- `ManualSceneCycleController` now exists on `SensorRig`; in Play Mode, pressing `Space` should generate one new runtime scene:
  - new robot pose
  - new props
  - rebuilt in-memory sample metadata
  - rejected scenes also advance the sample index so bad seeds are skipped instead of repeated forever
  - rejected robot poses now still refresh props if the current robot state is table-safe, so visible prop layouts can change on every `Space` press without changing sample-acceptance rules
  - `FreeRoomSnapshot` is the single Play Mode profile now; the old profile-cycle scaffolding was removed
  - scene summaries are intentionally concise
- a separate simple-safety prototype now exists:
  - new folder: `Assets/SimulationSimple/`
  - new scene copy: `Assets/Scenes/SampleScene_Simple.unity`
  - the prototype reuses the current robot motion components but replaces overlap/spawn logic with:
    - `SimpleRobotProxyRig`
    - `SimpleForbiddenZones`
    - `SimplePropLibrary`
    - `SimplePropSpawner`
    - `SimpleSceneCycleController`
  - `SampleScene.unity` remains the current working path; the prototype is isolated in the copied scene
  - `SimpleForbiddenZones` must generate its table forbidden boxes under `SurgeryTable`, not under the bootstrap root, so table moves in the scene keep the forbidden volumes aligned
  - `SimpleSceneCycleController` now skips bad robot seeds inside a single `Space` press and keeps trying the next seeds until it finds the first table-safe pose, so one bad sampled pose does not force a visible reject in the prototype
  - `SimpleSceneCycleController` now also aligns the robot root to a small profile-based X offset relative to the table before sampling, because profile shape changes alone were not enough to place the robot near the table in the simple scene
  - for the simple prototype's `AroundTableLeft` / `AroundTableRight` modes, `SimpleSceneCycleController` now pins `Long` to a modest local target (`0.65m`) and enforces a stronger `Z1Rot` minimum after sampling; this keeps around-table placement simple and local to the prototype instead of pushing more scene-specific logic into the shared randomizer
  - in the current simple scene, the around-table `Z1Rot` adjustment should be treated as a roughly 90-degree backoff from the sampled top swing, not as a pure side-sign flip
  - in the simple prototype, `AroundTableLeft` / `AroundTableRight` now also override `Z2Rot` to `±105` after the shared table-avoidance step, but with the opposite sign from the main around-table swing, because that matches the actual scene motion better
  - `SimpleForbiddenZones` now builds `TableTopForbidden` from the actual tabletop slab center and dimensions, scaled by `1.15`, instead of the earlier tall padded safety slab
  - `SampleScene_Simple.unity` has now been cleaned of its unused legacy runtime scene components; the simple scene still deliberately reuses only these shared scripts from the old path:
    - `RobotStabilitySetup`
    - `RobotPoseController`
    - `RobotPoseRandomizer`
    - `RobotPoseValidator`
    - `RobotTableAvoidance`
    - `CleanRoomBuilder`
    - `SurgeryTableBuilder`
- simple-scene capture/export foundation now exists under `Assets/SimulationSimple/`

## Active Project Entry Points

Primary active scene:

- `Assets/Scenes/SampleScene_Simple.unity`

Reference / legacy scene:

- `Assets/Scenes/SampleScene.unity`

Primary runtime scripts:

- `Assets/RoomSpawner.cs`
- `Assets/Robot_stock/SurgeryTableBuilder.cs`
- `Assets/Robot_stock/RobotStabilitySetup.cs`
- `Assets/Simulation/Robot/*`
- `Assets/Simulation/Props/*`
- `Assets/Simulation/Capture/*`
- `Assets/SimulationSimple/*`

Primary robot definition:

- `Assets/Robot_stock/FlexArmStudents.urdf`

Primary active materials/settings:

- `Assets/Materials/Floor_hospital.mat`
- `Assets/Materials/WallTransparent.mat`
- `Assets/Robot_stock/Materials/shell.mat`
- `Assets/Settings/PC_RPAsset.asset`
- `Assets/Settings/SampleSceneProfile.asset`

## What Is Active vs Inactive

Active and relevant:

- `Assets/Scenes/SampleScene_Simple.unity`
- `Assets/RoomSpawner.cs`
- `Assets/Robot_stock/*`
- `Assets/Simulation/Robot/*`
- `Assets/Simulation/Props/*`
- `Assets/Simulation/Capture/*`
- `Assets/SimulationSimple/*`
- `Assets/Materials/*`
- `Assets/Settings/*`
- `Packages/manifest.json`
- `ProjectSettings/*`

Editor-only tooling:

- `Assets/Editor/BridgeScratch.cs`
- `Assets/Editor/README.ai`
- `unity-cmd.ps1`
- `unity-cmd.py`

Documentation / presentation artifacts:

- `Presentations/*`
- These files are non-runtime communication assets and should not be mistaken for simulation or export pipeline code.

AI tooling note:

- The project already includes `com.cziberpv.unity-bridge` in `Packages/manifest.json`.
- This is the current installed AI-to-Unity bridge.
- On this machine, the preferred bridge wrapper is `python3 unity-cmd.py`.
- `unity-cmd.ps1` remains useful for Windows/PowerShell environments.
- Unity Bridge runtime files are intentionally ignored/untracked:
  - `Assets/LLM/Bridge/request.json`
  - `Assets/LLM/Bridge/response.md`
  - `Assets/LLM/Bridge/Screenshots/`
  - `Assets/LLM/texture-catalog.json`
- First smoke test should stay read-only: `help`, `scene`, then optionally `selection`.
- Do not treat `screenshot` as a harmless read command. It enters Play Mode and can affect runtime state, so it is excluded from first-pass validation.
- `unity-mcp` is a possible future alternative or addition, but it is not installed in this repo.
- Future evaluation should account for overlap and possible duplication between Unity Bridge and `unity-mcp`.

Present but currently inactive / archival / template leftovers:

- `Assets/Exported_packages/Room_custom.prefab`
- `Assets/Exported_packages/Room_custom.unitypackage`
- `Assets/Robot_stock.zip`
- `Assets/IDK/*` (URP template/tutorial/readme content)

## High-Level Mental Model

The project currently relies more on serialized Unity scene state than on a large codebase.

Important consequence:

- many real behaviors and dependencies live inside `SampleScene.unity`
- changing scene object names or hierarchy can break scripts even if code compiles

The simulation currently consists of three major domains:

1. Room geometry
2. Table geometry
3. Robot articulation + visuals + baseline stabilization

These are separate enough that they should remain separate as the project grows.

## Scene Mental Model

Current root objects in `SampleScene.unity`:

- `Main Camera`
- `Directional Light`
- `Global Volume`
- `RobotStabilityBootstrap`
- `FlexArm`
- `Room_clean`
- `SurgeryTable`
- `SceneRandomizationBootstrap`
- `SensorRig`

### Main Camera

- Standard perspective camera
- Positioned at `(0, 1, -10)`
- URP additional camera data enabled
- Post-processing enabled

Implication:

- `Main Camera` is only for human viewing; dataset capture should use `SensorRig`

### Global Volume

- Uses `Assets/Settings/SampleSceneProfile.asset`
- Light post-processing only

Implication:

- current scene visuals are presentation-oriented, not yet calibrated for sensor realism

### Room_clean

- Generated room root
- Backed by `CleanRoomBuilder` in `Assets/RoomSpawner.cs`
- Contains floor, ceiling, and four walls

### SurgeryTable

- Generated table root
- Backed by `SurgeryTableBuilder`
- Contains tabletop, side rails, pedestal, and base

### FlexArm

- Imported URDF robot root
- Tagged `robot`
- Uses Unity URDF Importer components and `ArticulationBody`

### RobotStabilityBootstrap

- Separate bootstrap object
- Holds:
  - `RobotStabilitySetup`
  - `RobotPoseController`
  - `RobotPoseRandomizer`
  - `RobotPoseValidator`
  - `RobotPoseWorkflow`
  - `RobotJointAuditTool`
  - `RobotTableAvoidance`
- References `FlexArm` as `robotRoot`

### SceneRandomizationBootstrap

- Holds:
  - `PropCatalog`
  - `SpawnValidator`
  - `PropSpawner`
- Runtime-spawned props appear under child `RandomizedProps`

### SensorRig

- Holds:
  - `FixedSensorRig`
  - `SampleCapturePipeline`
  - `ManualSceneCycleController`
- Child cameras are the fixed legacy-compatible 4-camera rig:
  - `DepthCam_BL`
  - `DepthCam_BR`
  - `DepthCam_FL`
  - `DepthCam_FR`

## Runtime Scripts

### `Assets/RoomSpawner.cs`

Class: `CleanRoomBuilder`

Role:

- builds a simple rectangular clean-room from Unity primitives

Behavior:

- deletes all children under its transform
- resets root transform to identity
- creates:
  - floor as `Plane`
  - ceiling as inverted `Plane`
  - four walls as `Cube`

Key parameters:

- `lengthX`
- `widthZ`
- `heightY`
- `wallThickness`

Important dependency:

- scene currently contains generated room children that were later given specific materials in the serialized scene

Important risk:

- `BuildCleanRoom()` does not reassign `Floor_hospital` or `WallTransparent`
- rebuilding the room from the context menu will likely keep geometry but lose the current visual styling

Future implication:

- if room generation remains procedural, material assignment should eventually move into code or a config/prefab-driven layer

### `Assets/Robot_stock/SurgeryTableBuilder.cs`

Role:

- builds a parameterized interventional surgery table from primitives

Behavior:

- optional auto-build on start
- validates dimensions in `OnValidate()`
- clears children when rebuilding
- creates tabletop, rails, pedestal, base
- creates runtime materials internally with `Shader.Find`

Important dependency:

- table shape is controlled entirely by this script and the serialized inspector values in `SampleScene.unity`

Important strengths:

- self-contained
- easier to rebuild safely than the room

Important risks:

- generated materials live in the scene, not as reusable assets
- future visual consistency may drift if other table-related objects use separate materials
- if later physics or interaction is added, table geometry and semantics are still tightly coupled in one builder script

### `Assets/Robot_stock/RobotStabilitySetup.cs`

Role:

- baseline robot stabilization only
- not a motion controller

Behavior on `Start()`:

- finds all `ArticulationBody` objects under `robotRoot`
- disables likely conflicting URDF helper scripts if found
- validates expected link names
- finds articulation root
- sets root `immovable = true`
- disables gravity on all articulation bodies
- sets high stiffness/damping/force limit on selected joints
- sets each configured joint drive target to `0`

Behavior on `LateUpdate()`:

- warns if articulation root drifts beyond tolerance

Important dependencies:

- depends on exact imported link names:
  - `Room`
  - `Rail`
  - `Carriage`
  - `HorBeam`
  - `VerBeam`
  - `Sleeve`
  - `CArc`
- depends on the URDF-imported hierarchy being present in scene

Important risks:

- any future controller that drives these joints can clash with this script
- because this script forces hold drives toward zero target, it may resist motion commands unless updated
- if URDF link names change, this script will silently stop configuring some joints except for warnings

Future debugging rule:

- when robot movement behaves strangely, check `RobotStabilitySetup` before blaming physics

## Robot Model Mental Model

Robot source:

- `Assets/Robot_stock/FlexArmStudents.urdf`

URDF chain:

- `Room` -> `Rail` -> `Carriage` -> `HorBeam` -> `VerBeam` -> `Sleeve` -> `CArc`

Joint mapping:

- `RailMount`: fixed joint
- `Long`: prismatic
- `Z1Rot`: continuous
- `Z2Rot`: continuous
- `Prop`: continuous
- `CArc`: continuous

Imported robot visuals:

- STL assets under `Assets/Robot_stock/STL/`
- white metallic material from `Assets/Robot_stock/Materials/shell.mat`

### Crucial Current State of Collisions

The robot currently has:

- articulation bodies
- visuals
- URDF `Collisions` container objects
- generated trigger-only overlap colliders under the moving links via `RobotCollisionRig`

Important limitation:

- the files under `Assets/Robot_stock/hulls/` are still not imported as Unity mesh assets in this repo
- because of that, `RobotCollisionRig` currently derives overlap colliders from the live visual meshes under each link's `Visuals` subtree instead of the hull STL assets

Future implication:

- if tighter or faster collision geometry is needed later, the hull assets should be imported properly and swapped in

## Known Scene / Asset Dependencies

These are important hidden dependencies:

- `SampleScene.unity` depends on script GUIDs and scene hierarchy
- `RobotStabilitySetup` depends on imported robot link names
- room visuals currently depend on scene-serialized material assignments, not builder code
- table visuals depend on scene-generated materials
- the project depends on:
  - `com.unity.render-pipelines.universal`
  - `com.unity.robotics.urdf-importer`
  - `com.cziberpv.unity-bridge`

## Packages and Tooling

From `Packages/manifest.json`:

- URP is active
- Unity URDF Importer is active
- Unity Bridge package is active
- Input System package is now used by `Assets/Simulation/Capture/ManualSceneCycleController.cs`

Unity Bridge local files:

- `unity-cmd.ps1`
- `Assets/Editor/README.ai`
- `Assets/Editor/BridgeScratch.cs`
- `Assets/LLM/Bridge/request.json`

Purpose:

- editor automation / scene querying / one-off editor-side AI actions

Not runtime pipeline code.

## Current Problems / Issues

Keep this list current.

### Open

1. Simple-scene capture/export now exists, but it still needs runtime validation.
   - Impact: the main simple-scene exporter is now the active full-sample path, but the new default depth backend still needs direct runtime validation against Python reconstruction.
   - Current reality: `Assets/SimulationSimple/` now owns accepted-scene export into `GeneratedSamples/DepthCaptures/`.
   - Important note: `SimpleSampleExporter` now defaults to the working material-override depth backend; the URP depth-texture path is kept only as a temporary fallback toggle for A/B checks.

2. Robot semantics are corrected in code but still need human visual validation in Unity.
   - Impact: the mapping is now much better grounded, but the final proof is whether the C-arm visibly rotates in place and the chain behaves plausibly from multiple viewpoints.
   - Cause: imported Unity articulation behavior is the source of truth, and some semantics were still inferred from the simplified chain plus Philips docs.

3. Room rebuild loses current styling.
   - Impact: running `Build Clean Room` can remove the current transparent wall / hospital floor look.
   - Cause: `CleanRoomBuilder` rebuilds primitives but never reapplies scene materials.

4. `RobotStabilitySetup` may clash with future motion control.
   - Impact: future robot driving can appear damped, stuck, or overwritten.
   - Cause: it sets high hold drives and targets zero on movable joints.

5. Final table and prop safety now depends on real overlap checks, but only for final landed poses.
   - Impact: final-state safety is much stronger than the earlier bounds-only gate.
   - Remaining limitation: no path-planning or mid-motion clearance exists; only the final landed pose is audited.

6. No sensor realism or capture calibration layer exists.
   - Impact: future depth export may be visually available but physically wrong.
   - Missing pieces: camera intrinsics policy, clipping policy, depth encoding, frame conventions, noise model if needed.

7. Too much critical behavior lives in `SampleScene.unity`.
   - Impact: easy to break behavior by scene edits or renames.
   - Cause: compact codebase, high scene serialization reliance.

8. Collision hull STL assets still exist but are not the active overlap source.
   - Impact: easy for future agents to assume they are live when they are not.
   - Current reality: `RobotCollisionRig` uses visual meshes because the hull STL files are not imported as Unity meshes here.
   - Location: `Assets/Robot_stock/hulls/`

9. Unity Bridge requires the Unity Editor to be open on this project for live validation.
   - Impact: local wrapper tests can still time out even when the tooling files are correct.
   - Cause: the bridge is file-based and only Unity processes requests.

10. Current prop validation is collider-based, but room placement still uses room interior bounds rather than wall-collider penetration tests.
   - Impact: table/robot/prop overlap checks are now real collider queries, while wall/floor/ceiling placement still uses explicit room bounds.
   - Cause: room placement only needs a simple interior volume for now; robot-vs-room collision is still out of scope.

11. The active exporter is simple-scene only.
   - Impact: `SampleScene_Simple.unity` is now the path for unattended capture work; the old `SampleScene.unity` capture stack should be treated as legacy/reference only.
   - Cause: the old runtime path became too complex, so export was added on the simpler parallel path instead.

12. Manual scene stepping still needs direct user validation in the Unity editor.
   - Impact: bridge-based compile/smoke validation works, but the bridge does not expose enough runtime rejection detail to prove accepted poses from automation alone.
   - Cause: Unity Bridge can enter Play Mode and step time, but it does not synthesize keyboard input and gives limited visibility into custom runtime logs/fields.

13. Keep the robot-motion layer simple.
   - Impact: adding too many validators, logs, or coupling rules can make motion feel static without improving dataset quality.
   - Current rule: prefer wider practical sampling plus one hard final-pose safety gate over layered cleverness.

14. If side coverage looks biased, check `RobotPoseRandomizer` before adding more table logic.
   - Impact: side coverage can fail even when table safety is correct if free-style swing sampling clusters on one sign.
   - Current mitigation: free-style poses alternate side every sample, but only the key top joints stay side-coupled; over-coupling creates fake diversity.

15. If stepping feels laggy, check serialized scene values before changing architecture.
   - Impact: this project can feel slow even when the code is simple if `SampleScene.unity` still has an old `validationTimeoutSeconds` or large retry count serialized.
   - Current practical settings are short validation waits and fewer attempts; the older `3.5s` timeout was a major source of lag.

16. If one `Space` press visibly makes the robot try several poses, the Play Mode path is too complicated.
   - Impact: visible retries look wrong and make the generator feel broken even if the final pose is acceptable.
   - Current rule: in Play Mode, one keypress should produce one sampled movement.

18. The old retry-based robot workflow has been intentionally removed.
   - Impact: future agents should not reintroduce multi-attempt acceptance logic unless there is a very clear benefit.
   - Current rule: keep the active path easy to trace first; only add complexity if it solves a measured problem.

### Deferred / Watch

1. Generated table materials live in-scene, not as reusable assets.
2. Camera setup is generic and not yet structured for dataset generation.
3. The stock URP template content in `Assets/IDK/` can distract future agents and should not be mistaken for simulation logic.

## Likely Future Clashes

These are predictions meant to speed up future debugging:

### Motion Control vs Stability Layer

If a future controller writes articulation drive targets or velocities:

- it may conflict with `RobotStabilitySetup`
- symptoms may include:
  - joints snapping back
  - reduced motion range
  - apparent sluggishness
  - unexplained stability warnings

### Procedural Rebuild vs Scene Styling

If someone rebuilds the room or table procedurally:

- geometry may regenerate correctly
- styling, collider details, or extra scene-attached components may be lost

### Visual Meshes vs Physics / Planning Needs

If collision checking, clearance analysis, or export validation is added:

- the active overlap path now depends on `RobotCollisionRig`, `RobotOverlapDetector`, and `SceneObstacleRegistry`
- the robot is currently checked with generated trigger colliders derived from visual meshes, not the separate hull STL assets

### Capture Pipeline vs Post-Processing

If image/depth export is added using the existing scene camera:

- URP post-processing and generic scene presentation settings may contaminate capture behavior
- likely need separate capture camera(s) and explicit render configuration

### Naming Changes vs Script Behavior

If robot link names or scene object names are changed for readability:

- `RobotStabilitySetup` can break quietly except for warnings
- future control scripts may also break if they key off names

## Debugging Starting Points

When something breaks, start here:

### Robot does not move or moves incorrectly

Check in order:

1. `RobotStabilitySetup.cs`
2. `RobotPoseController.cs` joint semantics and unit assumptions
3. `RobotPoseRandomizer.cs` conservative ranges and coupling rules
4. `RobotTableAvoidance.cs` if the issue appears only near the table footprint
5. `RobotJointAuditTool.cs` one-joint-at-a-time behavior from parked pose
6. imported `ArticulationBody` settings in `SampleScene.unity`
7. link names still matching expected names
8. whether future controllers are writing to the same drives

### Robot passes through environment

Check in order:

1. whether `RobotCollisionRig` is present on `RobotStabilityBootstrap`
2. whether the generated link colliders exist under each robot `Collisions` node
3. whether `RobotOverlapDetector` points at `SceneObstacleRegistry`
4. whether `Assets/Robot_stock/hulls/` are still unused and visual-mesh colliders are the active source
5. physics layer / trigger settings if collider queries behave unexpectedly

### Room looks wrong after rebuild

Check:

1. `BuildCleanRoom()` was likely run
2. wall and floor materials need reassignment

### Dataset export seems visually wrong

Check:

1. whether capture uses main camera instead of dedicated sensor camera
2. URP post-processing
3. clipping planes
4. render scale / depth texture settings

## Resolved Notes

- Git is now the canonical VCS for the repo with a reproducible baseline commit and a Unity-safe `.gitignore`.
- Archive/reference artifacts are intentionally excluded from Git tracking to keep the live project history focused.
- The project has an installed Unity Bridge package (`com.cziberpv.unity-bridge`) that can be used as the first AI-to-Unity integration layer before evaluating `unity-mcp`.
- The stock Unity Bridge wrapper was PowerShell-only; repo root now also contains `unity-cmd.py` for macOS/Linux use without touching simulation code.
- Robot pose control now has a dedicated foundation under `Assets/Simulation/Robot/`.
- Robot articulation sampling now uses explicit semantic roles and degree-based revolute targets instead of earlier radian-like guesses.
- Table-aware pose adjustment now exists as a separate heuristic layer instead of being baked into the fundamental joint model.
- Manual scene stepping no longer re-checks the validator's strict `isValid` flag after `RobotPoseWorkflow` has already accepted a pose; this mismatch was blocking usable scene advancement.
- `main` was restored to the last known good overlap-safety baseline with a normal recovery commit instead of rewriting branch history.
- The old retry-based acceptance path and oversized per-scene joint summary were pruned; the active runtime path is now intentionally one-shot and minimal.
- Prop randomization now has a dedicated foundation under `Assets/Simulation/Props/`.
- Final overlap safety now has a dedicated foundation:
  - `RobotCollisionRig`
  - `RobotOverlapDetector`
  - `SceneObstacleRegistry`
  - `ColliderOverlapUtility`
- Props are now validated from real candidate colliders rather than fake placeholder bounds.
- `PropSpawner.spawnOnStart` is intentionally disabled so `ManualSceneCycleController` remains the only scene owner.
- The `outside_room_bounds` false-rejection bug was fixed by syncing transforms before reading candidate collider bounds.
- Fixed sensor rig and sample metadata aggregation now exist under `Assets/Simulation/Capture/`.
- `ManualSceneCycleController` is now attached to `SensorRig`, and Play Mode startup already exercised the runtime scene-generation path.
- Rejected Play Mode robot poses can now still trigger a fresh prop spawn when the current robot state is table-clear, which decouples visible prop rerandomization from accepted sample generation.
- An isolated prototype path now exists in `Assets/SimulationSimple/` with its own copied scene `Assets/Scenes/SampleScene_Simple.unity`, so simple-safety experiments can proceed without touching the current working scene.
- `SampleScene_Simple.unity` is now the active working scene; disabled legacy runtime components were removed from that scene so it only carries the active simple path plus shared robot helpers it still genuinely uses.
- `SimpleSceneCycleController` now retries prop layouts up to `15` times on an already accepted robot pose before rejecting the sample for `too_few_props` / `missing_ceiling_prop`, so unattended capture does not burn hundreds of whole-scene attempts on one unlucky robot pose.
- Simple unattended capture/export now exists on the simple path:
  - `FixedSensorRig` reused on `SensorRig`
  - `SimpleSampleExporter`
  - `SimpleAutoCaptureRunner`
  - one fixed side RGB camera `SideRgbCam`
- `FixedSensorRig` now owns the 4 legacy camera transforms as well as their intrinsics:
  - camera mount height is now `y = 1.35`
  - yaw stays on the old opposite-quadrant aim
  - pitch was reduced so the lower-mounted cameras keep the same aim point
  - capture resolution is now controlled from a small inspector dropdown on `FixedSensorRig`:
    - `FullHD`
    - `UHD4K`
    - `Custom`
- Accepted simple-scene samples now export to `GeneratedSamples/DepthCaptures/sample_####/` at repo root with legacy-compatible depth/robot files plus additive RGB, side RGB, scene-object, robot-state, and voxel outputs.
- `SimpleAutoCaptureRunner` now waits `18` `FixedUpdate` steps after an accepted scene before export, and `SimpleSampleExporter` calls `Physics.SyncTransforms()` immediately before capture so camera/depth export sees the fully-settled scene state.
- The main simple-scene exporter now defaults to the working material-override true-depth backend:
  - `SimpleSampleExporter.DepthCaptureBackend.MaterialOverridePrototype` is the trusted path
  - it temporarily overrides active scene renderer materials with `Hidden/SimpleTrueDepthPrototype`, captures all 4 fixed rig depth raws/previews, then restores materials before any RGB capture
  - the older URP depth-texture path remains in `SimpleSampleExporter` only as a temporary fallback toggle for A/B comparison
  - raw depth values are clamped to `[nearClip, farClip]`, and the exporter now also rejects all-far/no-geometry captures so silent black-depth folders are not accepted
  - depth preview PNGs stay intentionally simple: find per-image `maxDepth` below far clip, then map intensity as `1 - depth/maxDepth`
- A separate depth-only prototype now exists for isolated testing:
  - `Assets/SimulationSimple/SimpleTrueDepthPrototypeExporter.cs`
  - `Assets/SimulationSimple/Shaders/SimpleTrueDepthPrototype.shader`
  - it does **not** use the current exporter path; instead it temporarily overrides all scene renderer materials with a URP unlit depth material, renders the fixed rig cameras, and writes a separate depth-only export under `GeneratedSamples/DepthCapturesPrototype/`
- Simple-scene capture now defaults to 4K output (`3840x2160`) for the 4 fixed cameras as well as RGB/side RGB, while keeping the current mid-wall fixed rig positions.
- `SideRgbCam` is now a closer downward presentation shot instead of the earlier far flat overview.

## Quick Orientation for Future Codex

If you are a future Codex instance:

1. Read this file first.
2. Then inspect `Assets/Scenes/SampleScene_Simple.unity`.
3. Then inspect:
   - `Assets/RoomSpawner.cs`
   - `Assets/Robot_stock/SurgeryTableBuilder.cs`
   - `Assets/Robot_stock/RobotStabilitySetup.cs`
   - `Assets/Robot_stock/FlexArmStudents.urdf`
4. Treat `Assets/IDK/` as template noise unless proven otherwise.
5. Do not assume the old `SampleScene.unity` runtime path is the one to extend; prefer the simple scene first.
6. Before adding robot motion, decide whether `RobotStabilitySetup` should stay, change, or be bypassed.
