# The Space Between Us · Stardust (Unity VR)

A VR experience with two Virtual Subjectivenesses in one world:
the **Destroyer** breaks dead asteroids to free rock, ice and crystal; the **Builder** uses them to bring floating islands to life.
They look like opposites but depend on each other: one process, not two.

- **Engine:** Unity 6.6 (everyone must use the exact same build number, see `ProjectSettings/ProjectVersion.txt`)
- **Template:** Unity 3D template (as in class)
- **Target:** Meta Quest with controllers
- **VR SDK:** Meta XR SDK (All-in-One, or Core + Interaction SDK), set up with Meta's Project Setup Tool (Fix All) and Building Blocks

---

## 1. Getting started

1. Install Git. (Git LFS is **not** used: every file in the repo must stay under GitHub's 100 MB limit, see §9.4.)
2. Clone the repo and open the folder with **Unity Hub** (same Unity version).
3. Open `Assets/_Project/Scenes/Prototype_Core.unity` (the playable prototype, §8). `Main.unity` is the final scene.
4. If Unity asks, run **Meta XR > Project Setup Tool > Fix All**. Platform: **Android**.

**Project settings (already set, don't change):**
Edit > Project Settings > Editor > Asset Serialization = *Force Text*; Version Control = *Visible Meta Files*.

---

## 2. Folder structure

```
Assets/
  _Project/
    Scenes/        Main.unity, Prototype_Core.unity (generated, §8)
    Scripts/
      Core/        flow, perspective switch, artifact, hand velocity, hints, light hands (HandPose), HudFollow
      Destroyer/   asteroid, fragments
      Builder/     attractor, island slots, island, life growth, lotus, stream
    Editor/        scene builder, art swap, sky, palette, lotus generator, smoke test
    Shaders/       StylizedWater, FlowingWater, LightThread, GhostHand, HintText (all URP + Quest single-pass)
    Prefabs/       Prototype/: Asteroid, Fragment_Rock/Ice/Crystal, Island
    Materials/     Prototype/ (generated materials), Skybox/
    Art/           generated meshes (Stream/)
  3D_Game_Assets_Flora/  Asteroids pack/  Floreswa/  Mid Moon Studio/  SineVFX/  Nebula Skyboxes/
                   imported asset packs (see §9.3)
```

---

## 3. Collaboration rules

- **Don't edit `Main.unity` at the same time.** Build and test your part in your own test scene, as a **prefab**. Only one person merges prefabs into `Main.unity`, and says so in the chat first.
- One branch per feature: `feature/destroyer-punch`, `feature/life-growth`… Merge into `main` with a pull request.
- Pull before you start, push when a piece works. Small commits.
- No Git LFS: keep every file under 100 MB (shrink big textures first, see §9.4). Don't commit asset packs the game doesn't use: add them to `.gitignore`.

---

## 4. Work split

| Area | What | Main scripts |
|---|---|---|
| **World + switch** | XR rig, central artifact, perspective switch (scale, hands, x-ray layer), 6-step flow, fade | `PerspectiveSwitcher`, `GameFlowManager`, `CentralArtifact` |
| **Destroyer** | Asteroid layers (shell → inside → core), punch / pull apart / throw, fragments drifting to the islands, last asteroid (step 4), x-ray shader | `Asteroid`, `MaterialFragment`, `HandVelocityTracker` |
| **Builder** | Attract fragments, place rock, pour ice, plant crystal, island state | `FragmentAttractor`, `IslandSlot`, `Island` |
| **Life + art + sound** | Moss / grass spreading, lake, fish, dragonflies, lotus, fireflies, deer; skybox, sounds | `LifeGrowth` + assets |

---

## 5. Build order (MVP first)

1. **Rig + one island + one asteroid** in a test scene: punch the asteroid, fragments fly out.
2. **Builder basics:** grab a rock fragment, drop it in a slot → moss grows.
3. **Perspective switch:** touch the artifact → scale ×20, swap hands.
4. **Full island recipe:** pour ice (lake), plant crystal (lotus), island alive.
5. **6-step flow** with `GameFlowManager`; the last asteroid frees its crystal with one touch.
6. **Life details:** fish, dragonflies, fireflies following the hand, deer at the end.
7. **Polish:** sounds, haptics, x-ray shader, reveal scene.

---

## 6. Scene wiring (quick reference)

**Tags / layers to create:** tags `DestroyerHand`, `PlayerHand`; layer `XRay`.

**XR rig:** Building Block **Camera Rig** (OVRCameraRig) + **Controller Tracking**; hands live under `LeftHandAnchor` / `RightHandAnchor`.

**Destroyer hands:** a sphere collider (trigger) + `HandVelocityTracker` on each blue hand, tag `DestroyerHand`.
For floating movement: grab an asteroid and pull (simple script moving the rig opposite to the hand), or use Meta's Locomotion building blocks.

**Asteroid prefab:** trigger collider + `Asteroid`. Children: `Shell`, `Inside`, `Core`, `Broken` visuals.
Veins inside go on the `XRay` layer. Assign the three fragment prefabs and `islandsDriftTarget`.
Last asteroid: tick `carefulMode` (one touch frees the crystal; also tick `requireGentle` for the gentle-tap version). Events: `onOpened` → `GameFlowManager.AsteroidOpened()`.

**Fragment prefabs (Rock / Ice / Crystal):** Rigidbody (no gravity) + collider + `MaterialFragment` + Meta Interaction SDK **Grabbable** (use the Grab Interaction building block / Quick Actions to make an object grabbable).
Add an **Interactable Unity Event Wrapper** and wire: When Select → `OnGrabbed()`, When Unselect → `OnReleased()`.
Make them look different, not only in colour: rough heavy rock, translucent frosted ice, long glowing crystal prisms.

**Island prefab:** `Island` + `LifeGrowth` + three child slots (`IslandSlot` with trigger colliders):
Ground = Rock / Place, Lake = Ice / Pour, Lotus spot = Crystal / HoldStill (in the lake centre).
`LifeGrowth` stages named exactly: `Moss`, `Plants`, `WaterLife`, `Lotus`. Event `onAlive` → `GameFlowManager.IslandAlive()`.

**Builder hands:** `FragmentAttractor` on each hand anchor (choose `LTouch` / `RTouch`; reads the index trigger via `OVRInput`) + tag `PlayerHand`. Teleport: Meta **Teleport** building block, only onto the islands.

**Switch:** `PerspectiveSwitcher` on a manager object (`xrOrigin` = the OVRCameraRig root, `xrCamera` = CenterEyeAnchor's camera, two anchors, builder-only / destroyer-only objects, fade canvas).
`CentralArtifact` on the artifact (trigger collider), linked to `GameFlowManager`.

---

## 7. The experience in one line per step

1. **Builder:** barren islands, no materials.
2. **Destroyer:** break two asteroids; fragments drift to the islands.
3. **Builder:** rock = ground (moss), ice = lake (plants, fish, frogs, dragonflies), crystal = lotus (trees, fireflies). Two islands come alive.
4. **Destroyer:** the last crystal asteroid floats above the islands. Touch it once → the crystal drifts down to the islands.
   (Optional harder version: tick `requireGentle` on `Asteroid_Last_Careful`. Then it needs 3 gentle taps, and a hard punch **ruins** every island it hits: life withers, the slots are empty again and the island's rock / ice / crystal burst back out, so the Builder has to rebuild it in step 5.)
5. **Builder:** plant the last crystal; all three islands alive; a glowing deer comes to drink.
6. **Reveal:** the three asteroid remains drift into view above their islands, and threads of light link each island to the asteroid it came from. *"Were we ever truly separate?"*

---

## 8. Core-loop prototype (grey-box, playable now)

**Menu → Space Between Us → Build Core Prototype Scene** generates `Assets/_Project/Scenes/Prototype_Core.unity`
plus placeholder prefabs/materials (`Prefabs/Prototype`, `Materials/Prototype`), creates the tags/layer, and wires every event.
Re-running it overwrites only those generated files, so don't hand-edit them: copy them out first.

- **With a Quest:** trigger = attract, grip = grab, punch / tap with the blue hands, touch the gold artifact (low, to your right) to switch.
- **Without a headset (Editor):** `DesktopRigSimulator` takes over. RMB+mouse = look, WASD/QE = move, wheel = reach, Tab = touch artifact.
  Builder: Space = attract, LMB = grab/release, R = tilt (pour). Destroyer: LMB = punch, F = tap (both free the last crystal).
- **Automated check:** *Space Between Us → Run Core Loop Smoke Test* plays all 6 steps by itself; look for `[SmokeTest] PASSED` in the Console.
  CI / headless: `Unity -batchmode -projectPath . -executeMethod SpaceBetweenUs.EditorTools.PrototypeSmokeTest.RunBatch -logFile -` (the editor must be closed).

---

## 9. Art pass (imported assets on top of the grey-box)

### 9.1 Menus

| Menu (**Space Between Us →**) | What it does |
|---|---|
| **Build Core Prototype Scene** | Rebuilds `Prototype_Core` and then runs *Apply Imported Art* automatically. |
| **Apply Imported Art** | Dresses the generated prefabs and scene with the imported packs (see 9.2). Safe to re-run: it removes its own `Art*` children first and only hides (never deletes) the grey-box parts. Results are listed in the Console under `[ArtSwap] Done`. Open `Prototype_Core` first, or the scene-only steps (hands, hints, light threads) are skipped. |
| **Island Style → Rock Spire / Cliff Spire / Low-poly** | Shape of the island rock. We use **Rock Spire**. Cliff Spire needs `ValleyGrayCliff`, which is not committed. |
| **Sky → Nebula 02** (and Apply Color Palette) | Sets the skybox and matches the ambient light to it. We use **Nebula 02**. |
| **Reset Prototype Art** | Back to plain grey-box materials and prefabs. |

### 9.2 What it looks like now

- **Islands:** floating rock with a jagged inverted-cone underside, all in one stone material (Asteroids pack). Each island picks one of 3 variants from the number in its name (`ArtVariant`).
- **Slot markers:** soft breathing glowing rings (gold = rock, cyan = ice, violet = crystal).
- **Lake:** custom `StylizedWater` shader. Once the lake is full, a **stream** (`FlowingWater` + `StreamReveal`) runs across the island and spills over the edge as a waterfall into space.
- **Life:** Flora pack grass lawn with small flowers, bushes with ferns, mushrooms and pebbles, reeds, three trees, and a lotus with pads. Fish come from Floreswa. Plants are capped at 2.5× their height in width, so flat models can't blow up.
- **Fragments:** rock (Mid Moon), ice (Asteroid_5 shape in an ice material with snowflake sparkles), crystal (SineVFX).
- **Hands of light:** Meta hand mesh with the `GhostHand` rim-glow shader. Builder = gold; Destroyer = blue and 1.3× larger. Fingers curl with GRIP (Destroyer: GRIP or TRIGGER). If fingers bend backwards, flip **Curl Sign** on `HandPose`. Colliders and gameplay are unchanged.
- **Step 6 threads:** soft glowing curves (`LightThread`) from each island to its asteroid. At the reveal the asteroid remains glide (5 s) to the sky in the direction of their island, about 22 m away and 26° up, and keep tumbling slowly, so every thread ends where you can see it. Tune **Gather Distance / Elevation / Time** on `Reveal` (`RevealThreads`), or untick **Gather Asteroids**.
- **Hint text:** 3D `TextMesh` with the `HintText` shader (Canvas text flickered in the headset). `HudFollow` lets it float in front of you, recentre when you look away, and scale with the giant Destroyer rig. Tweak **Line Height** / **Wrap Chars** on `FlowHints` and **Dead Zone** / **Settle Angle** on `HudFollow`.
- **Sky:** Nebula 02 cubemap; ambient light sampled from the sky. The star particles twinkle (`TwinkleStar` shader, each star has its own speed; a few flare now and then), and `ShootingStars` sends a silent, straight shooting star (a 2-point line, head bright, tail fading) every 8–15 s, within 50° of where you are looking and 15–40° above the horizon (**View Spread**, **Elevation**, **Interval**, **Tail Length** on `Stars`).
- **Sound:** `SoundDirector` (scene object `Audio`) listens to the gameplay events itself:
  - ambience per role, cross-faded on the switch: calm deep-space drones (48 s seamless loop; open chord with no third, slowly beating voices, a low "solar wind", rare far-away glimmers with echoes). Builder = D, a little warmer; Destroyer = A, lower and darker;
  - switch whoosh;
  - asteroid punch and tap (violent break only with `requireGentle`); island ruined / alive;
  - attract hum, grab, rock placed, ice pouring, water filled, crystal, life growing;
  - reveal chord. Shooting stars are silent on purpose.

  The clips are **placeholders synthesised in code** (*Space Between Us → Audio → Generate Placeholder Sounds*) in `Assets/_Project/Audio`. Replace any `.wav` with a real sound of the same name: generation never overwrites an existing file (except *Regenerate All*), unless a recipe's version was bumped in `SoundSynth.Versions` (currently the two ambiences, v2), which rebuilds only those generated files.

### 9.3 Asset packs

| Pack | Used for | In git |
|---|---|---|
| `3D_Game_Assets_Flora` | grass, flowers, bushes, trees, reeds, lotus | yes |
| `Asteroids pack` | island rock, asteroids, ice shape | yes |
| `Floreswa` | fish | yes |
| `Mid Moon Studio` | rock fragment (and fallback island/trees) | yes |
| `SineVFX` | crystal meshes | yes |
| `Nebula Skyboxes` | **Nebula 02 only** (01/03/04 ignored) | 02 only |
| `BigRookGames`, `ValleyGrayCliff`, `Houidisoft technology`, `StarNestSkybox` | not used | no (`.gitignore`) |

Third-party materials are converted in place to URP Lit by *Apply Imported Art*, including HDRP materials whose shader is missing. Android textures are capped at 1024.
A teammate without the ignored packs can still run everything: the art swap skips missing packs.

### 9.4 Big files

GitHub refuses files over 100 MB and we don't use LFS.
`Nebula_02_Cubemap.exr` was 294 MB (8192×6144), so it was replaced with a 4096×3072 half-float copy (21 MB). Quest uses 1024 per face anyway.
The original is kept locally in `_Backup/` (ignored). Do the same for any new texture that is too big: shrink it before committing (e.g. `sips -z <h> <w> in.exr --out out.exr` on macOS).

### 9.5 Testing in the Meta XR Simulator

Meta XR Simulator works with Play mode: when an XR device is detected, `DesktopRigSimulator` turns itself off and the simulator's controllers drive the rig.
Without the simulator, the desktop controls from §8 apply.

### 9.6 Troubleshooting: magenta objects / Game view shows only the sky

Run **Space Between Us → Debug → Report Broken Shaders**. It writes `Logs/ShaderReport.txt` (also in the Console): the active render pipeline, quality level, graphics API, and every shader in the scene with errors.

- **`Pipeline: NONE (built-in)`** → URP is not active, so every URP material renders magenta (and nothing in XR). Cause we hit: the **Meta Quest build profile** (`Assets/Settings/Build Profiles/Meta Quest.asset`, created by Meta's setup tool) overrode the quality level with one that does not exist. It now points to **Mobile** (which uses `Mobile_RPAsset`). If it happens again: *File → Build Profiles → Meta Quest → Quality Settings* → choose **Mobile** (or remove the override), then **Switch Profile**. Commit the build profile so teammates get the fix.
- **`FallbackLoading` replacement pass error** in Play → async shader compilation's placeholder doesn't support XR single-pass stereo. *Project Settings → Editor → Shader Compilation* → untick **Asynchronous Shader Compilation** (now off in the project). The first Play stalls while the XR variants compile.
- **A shader listed as BAD** → its compile errors are printed underneath.

