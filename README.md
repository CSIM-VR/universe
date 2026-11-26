# The Space Between Us · Stardust (Unity VR)

A VR experience with two Virtual Subjectivenesses in one world:
the **Destroyer** breaks dead asteroids to free rock, ice and crystal; the **Builder** uses them to bring floating islands to life.
They look like opposites but depend on each other: one process, not two.

- **Engine:** Unity 6.6 (everyone must use the exact same build number, see `ProjectSettings/ProjectVersion.txt`)
- **Template:** Unity 3D template (as in class)
- **Target:** Meta Quest with controllers
- **VR SDK:** Meta XR SDK (All-in-One, or Core + Interaction SDK), set up with Meta's Project Setup Tool (Fix All) and Building Blocks

---

## At a glance (for slides)

**Two Virtual Subjectivenesses (VS)**

| | **Destroyer** | **Builder** |
|---|---|---|
| Who | A giant (×20); the game starts here | Human size, standing among the islands |
| Sees | Asteroids within reach; the islands as a tiny model at your feet; glowing veins inside the asteroids (x-ray) | Asteroids high in the sky; the islands up close: grass, lake, creatures |
| Does | Punches asteroids open with **blue hands of light** to free the materials | Pulls, grabs and places fragments with **gold hands of light**; pours water, plants crystals |
| Feel | Power; low, dark ambience | Care and detail; warmer ambience |

Both share **one space and one spot**: you switch by growing / shrinking in place in 3 s (×1 ↔ ×20), with no cut and no teleport. The role you leave stays behind as a faint glowing echo. Destruction gives the material for creation; at the end, threads of light show the two were never separate.

**Flow (3 steps)**
1. **Destroyer:** punch each of the 3 asteroids 3 times: rock → ice → crystal (9 pieces, exactly enough for 3 islands). Press A / B to shrink.
2. **Builder:** give each island rock → gold ring, ice → blue ring, crystal → violet ring.
3. **Reveal** (automatic when all 3 islands are alive): the world stands still, threads of light link each island to its asteroid. *"Were we ever truly separate?"*

**What grows on each island**
- **Rock:** soil, grass, small flowers
- **Ice:** lake, reeds, bushes, mushrooms, butterflies → stream and waterfall → fish and dragonflies
- **Crystal:** lotus, lily pads, trees, fireflies
- **All 3 islands alive:** a deer of light that leaps between the islands

**Look and sound**
- Nebula sky with twinkling stars and silent shooting stars.
- A space ambience for each role that cross-fades on the switch, plus sounds for punching, pulling, placing, pouring, growing, an island coming alive and the ending.

**Controls:** trigger = pull / punch · grip = grab · A / B = switch · hold MENU 2 s = restart

**On-screen hints**
- A small panel floats in front of you and follows when you look away. It has the same layout in every step: **role + step** → **GOAL** → **HOW** → **DONE** (progress) → **KEYS**.
- Destroyer: one dot per opened asteroid (●●○ 2 / 3). When all are open, a gold **► NEXT: press A / B to become the Builder** appears.
- Builder: three dots per island in the material colours (rock gold, ice blue, crystal violet; grey ring = still missing); an island's name turns gold when it is alive.
- While switching it shows **GROWING ×n / SHRINKING ×n**. At the end only the closing line remains.

Details: §7 (step by step, what grows when, background and sound) and §8 (how it is built).

---

## 1. Getting started

1. Install Git. (Git LFS is **not** used: every file in the repo must stay under GitHub's 100 MB limit, see §9.4.)
2. Clone the repo and open the folder with **Unity Hub** (same Unity version).
3. Open `Assets/_Project/Scenes/Prototype_Core.unity` (the playable prototype, §8). A hand-built `Main.unity` is planned but doesn't exist yet.
4. If Unity asks, run **Meta XR > Project Setup Tool > Fix All**. Platform: **Android**.

**Project settings (already set, don't change):**
Edit > Project Settings > Editor > Asset Serialization = *Force Text*; Version Control = *Visible Meta Files*.

---

## 2. Folder structure

```
Assets/
  _Project/
    Scenes/        Prototype_Core.unity (generated, §8)
    Scripts/
      Core/        flow, perspective switch, role echo, artifact, hand velocity, hints, light hands (HandPose),
                   HudFollow, reveal threads, shooting stars, sound director
      Destroyer/   asteroid, fragments
      Builder/     attractor, island slots, island, life growth, critters, glow deer, lotus, stream
      Tests/       core-loop smoke test
    Editor/        scene builder, art swap, sky, palette, lotus generator, smoke test, sound synth,
                   Quest build setup + Gradle compileSdk fix, shader report
    Shaders/       StylizedWater, FlowingWater, LightThread, GhostHand, HintText, TwinkleStar, CritterWing
                   (all URP + Quest single-pass)
    Prefabs/       Prototype/: Asteroid, Fragment_Rock/Ice/Crystal, Island
    Materials/     Prototype/ (generated materials), Skybox/
    Audio/         generated placeholder sounds (.wav)
    Art/           generated meshes (Stream/)
  3D_Game_Assets_Flora/  Asteroids pack/  Floreswa/  Mid Moon Studio/  SineVFX/  Nebula Skyboxes/
                   imported asset packs (see §9.3)
```

---

## 3. Collaboration rules

- **Don't edit the same scene at the same time.** Build and test your part in your own test scene, as a **prefab**. Only one person merges prefabs into the shared scene, and says so in the chat first. `Prototype_Core` is generated: change `PrototypeSceneBuilder` / `PrototypeArtSwap`, not the scene by hand.
- One branch per feature: `feature/destroyer-punch`, `feature/life-growth`… Merge into `main` with a pull request.
- Pull before you start, push when a piece works. Small commits.
- No Git LFS: keep every file under 100 MB (shrink big textures first, see §9.4). Don't commit asset packs the game doesn't use: add them to `.gitignore`.

---

## 4. Work split

| Area | What | Main scripts |
|---|---|---|
| **World + switch** | XR rig, central artifact, perspective switch (grow / shrink ×20 in one shared space, hands, x-ray layer), role echo, 3-step flow | `PerspectiveSwitcher`, `GameFlowManager`, `CentralArtifact` |
| **Destroyer** | Asteroid layers (shell → inside → core), punch / pull apart / throw, fragments falling to the islands below (exactly one of each material per asteroid), x-ray shader | `Asteroid`, `MaterialFragment`, `HandVelocityTracker` |
| **Builder** | Attract fragments, place rock, pour ice, plant crystal, island state | `FragmentAttractor`, `IslandSlot`, `Island` |
| **Life + art + sound** | Moss / grass spreading, lake + stream, lotus, fireflies; swimming fish, butterflies, dragonflies, deer of light; sky, shooting stars, sounds | `LifeGrowth`, `Critters` + assets |

---

## 5. Build order (MVP first)

1. **Rig + one island + one asteroid** in a test scene: punch the asteroid, fragments fly out.
2. **Builder basics:** grab a rock fragment, drop it in a slot → moss grows.
3. **Perspective switch:** touch the artifact → scale ×20, swap hands.
4. **Full island recipe:** pour ice (lake), plant crystal (lotus), island alive.
5. **3-step flow** with `GameFlowManager`: Destroyer opens 3 asteroids → Builder brings 3 islands to life → reveal.
6. **Life details:** swimming fish, butterflies, dragonflies, fireflies, glowing deer (done). Idea left: fireflies following the hand.
7. **Polish:** sounds, haptics, x-ray shader, reveal scene.

---

## 6. Scene wiring (quick reference)

**Tags / layers to create:** tags `DestroyerHand`, `PlayerHand`; layer `XRay`.

**XR rig:** Building Block **Camera Rig** (OVRCameraRig) + **Controller Tracking**; hands live under `LeftHandAnchor` / `RightHandAnchor`.

**Destroyer hands:** a sphere collider (trigger) + `HandVelocityTracker` on each blue hand, tag `DestroyerHand`.
For floating movement: grab an asteroid and pull (simple script moving the rig opposite to the hand), or use Meta's Locomotion building blocks.

**Asteroid prefab:** trigger collider + `Asteroid`. Children: `Shell`, `Inside`, `Core`, `Broken` visuals.
Veins inside go on the `XRay` layer. Assign the three fragment prefabs and `islandsDriftTarget`.
`fragmentsPerLayer` = 1 (set by the scene builder): one rock, one ice, one crystal per asteroid. `carefulMode` is an optional variant not used by the current flow. Events: `onOpened` → `GameFlowManager.AsteroidOpened()`.

**Fragment prefabs (Rock / Ice / Crystal):** Rigidbody (no gravity) + collider + `MaterialFragment` + Meta Interaction SDK **Grabbable** (use the Grab Interaction building block / Quick Actions to make an object grabbable).
Add an **Interactable Unity Event Wrapper** and wire: When Select → `OnGrabbed()`, When Unselect → `OnReleased()`.
Make them look different, not only in colour: rough heavy rock, translucent frosted ice, long glowing crystal prisms.

**Island prefab:** `Island` + `LifeGrowth` + three child slots (`IslandSlot` with trigger colliders):
Ground = Rock / Place, Lake = Ice / Pour, Lotus spot = Crystal / HoldStill (in the lake centre).
`LifeGrowth` stages named exactly: `Moss`, `Plants`, `WaterLife`, `Lotus`. Event `onAlive` → `GameFlowManager.IslandAlive()`.

**Builder hands:** `FragmentAttractor` on each hand anchor (choose `LTouch` / `RTouch`; reads the index trigger via `OVRInput`) + tag `PlayerHand`. Teleport: Meta **Teleport** building block, only onto the islands.

**Switch:** `PerspectiveSwitcher` on a manager object (`xrOrigin` = the OVRCameraRig root, `xrCamera` = CenterEyeAnchor's camera, builder / destroyer anchors (same spot: the Destroyer grows where the Builder stands), builder-only / destroyer-only objects, fade canvas for the optional plain fade).
`CentralArtifact` on the artifact (trigger collider), linked to `GameFlowManager`.

---

## 7. The experience in one line per step

1. **Destroyer (you start as the giant):** three dead asteroids float in front of you. Punch each one 3 times: shell (rock) → inside (ice) → core (crystal). Each asteroid gives exactly **1 rock, 1 ice, 1 crystal**, one island's worth, so there are no spare materials. When all three are open, press **A / B** (or touch the gold gem) to become the Builder.
2. **Builder:** shrink down among the islands, where the 9 fragments have drifted. Rock = ground (moss), ice = lake (plants, swimming fish, dragonflies; then a stream and waterfall), crystal = lotus (trees, fireflies); butterflies come with the plants. When the third island comes alive, a deer made of light appears and leaps between the islands.
3. **Reveal (starts by itself when the third island is alive):** everything stands still; threads of light link each island to the asteroid it came from. *"Were we ever truly separate?"*

**What grows when (on each island, during step 2)**

| You place… | Life stage | What appears |
|---|---|---|
| **Rock** in the gold ring | `Moss` (at once) | Soil and a lawn: grass clusters with small flowers (purple, yellow, pink, chamomile, mint) |
| **Ice** in the blue ring | `Plants` (at once) | The lake fills; reeds around the lake; bushes (bush, lilac, fern) with ferns, mushrooms and pebbles; **butterflies** start fluttering over the island |
| | (lake full) | A **stream** runs over the island edge and falls into space as a **waterfall** |
| | `WaterLife` (3 s later) | **3 fish** swim round the lake; **dragonflies** hover and dart over the water |
| **Crystal** in the violet ring (appears once there is a lake) | `Lotus` | A **lotus** flower and lily pads on the lake; **trees** (one big, two small); **fireflies** |
| **All three islands alive** | – | A **deer of light** fades in on island 3, grazes, drinks, looks at you and leaps between the islands. At the same moment the reveal starts (the creatures keep living during it) |

The order is fixed: rock → ice → crystal. A ruined island (hit by a fragment thrown hard) loses its life and creatures and gets them back when rebuilt.

**What you see and hear (background and sound)**

- **Sky:** deep space all around you: a coloured nebula (Nebula 02 skybox, which also lights the scene), **twinkling stars** (each at its own pace, a few flare now and then) and a **shooting star** every 8–15 s, somewhere near where you are looking, above the horizon. Shooting stars are silent on purpose.
- **Asteroids:** dead grey rock; only the Destroyer sees the **glowing veins** inside (x-ray).
- **Hands of light:** gold hands for the Builder, larger blue hands for the Destroyer. The role you just left stays behind as a faint glowing echo.
- **Reveal:** the world stands still and soft **threads of light** grow from each island up to the asteroid it came from.

| Moment | Sound |
|---|---|
| Background, as the **Destroyer** | Calm deep-space drone, lower and darker (in A); a low "solar wind" and rare far-away glimmers |
| Background, as the **Builder** | The same kind of drone, a little warmer (in D). The two cross-fade when you switch |
| Switching roles | Whoosh |
| Punching an asteroid open | Punch hit for each layer |
| Pulling fragments (trigger) | Soft hum while pulling |
| Grabbing / placing rock | Grab click / rock settling into the ground |
| Ice | Pouring water, then a fill sound when the lake is full |
| Crystal | Crystal chime |
| Life growing | Soft growth sound for each stage |
| An island comes alive | Island-alive chime |
| Reveal | Closing chord; the background drone gets quieter |

The sounds are placeholders made in code (*Space Between Us → Audio → Generate Placeholder Sounds*); drop in a real `.wav` with the same name to replace one (see §8 "Hints and sound").

Because there are no spare materials, `FragmentKeeper` (added at Play) looks after them during step 2: a fragment that stays more than 2.5 m from the islands for 6 s is brought back, and if a material is missing (fewer fragments than empty slots need) a new one appears at the islands.

---

## 8. Core-loop prototype (grey-box, playable now)

**Menu → Space Between Us → Build Core Prototype Scene** generates `Assets/_Project/Scenes/Prototype_Core.unity`
plus placeholder prefabs/materials (`Prefabs/Prototype`, `Materials/Prototype`), creates the tags/layer, and wires every event.
Re-running it overwrites only those generated files, so don't hand-edit them: copy them out first.

- **With a Quest:** trigger = attract, grip = grab, punch / tap with the blue hands, touch the gold artifact (low, to your right) or press **A / B / X / Y** to switch (the buttons work once the current step's goal is done, like the artifact). Buttons, trigger, grip and vibration go through both OVRInput and Unity XR input, and `ControllerAnchorFallback` (added at Play) places a hand anchor from the Unity XR controller pose whenever OVRInput reports no controller, so the hands stay on the controllers (Quest 3 Touch Plus). `DesktopRigSimulator` never turns on in a headset build. Check a device with `adb logcat -s Unity | grep "\[Rig\]"`. **Restart:** hold the menu button (left controller, ☰) for 2 s to reload the scene and start again from step 1 as the Destroyer (`RestartButton`, added at Play; Backspace in the Editor without a headset). A press logs `[SwitchButton]`; a refused switch logs `[Flow] Switch refused` (see with `adb logcat -s Unity`).
- **Without a headset (Editor):** `DesktopRigSimulator` takes over. RMB+mouse = look, WASD/QE = move, wheel = reach, Tab = touch artifact.
  Builder: Space = attract, LMB = grab/release, R = tilt (pour). Destroyer: LMB = punch (aim at an asteroid), F = gentle tap.
- **Automated check:** *Space Between Us → Run Core Loop Smoke Test* plays all 3 steps by itself; look for `[SmokeTest] PASSED` in the Console.
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
| **Audio → Generate / Regenerate Placeholder Sounds** | Synthesises the `.wav` files in `Assets/_Project/Audio` (see Sound below). |
| **Run Core Loop Smoke Test** | Plays all 3 steps by itself (§8). |
| **Build → Prepare Quest Build** | Package name, scene list and target API for the Quest build (§9.6). |
| **Debug → Report Broken Shaders** | Finds what renders magenta (§9.7). |

### 9.2 What it looks like now

**World and switch**

- **One shared space, switch you can feel (×20):** the Destroyer is the Builder grown ×20 **on the same spot, facing the same way**. No cut, no teleport:
  - The asteroids float high above the islands (the Builder looks UP to see them), at the giant's arm's reach.
  - Becoming the Destroyer: your hands turn blue and you **grow in place** (3 s, ×1 → ×20 around your feet). You rise straight up past the islands, which stay at your feet as a tiny model, and arrive right at the asteroids. The fragments you knock out fall down onto the islands below you.
  - Going back is the exact reverse: you **sink back down** from the asteroids and land among the islands.
  - The game starts as the giant (`PerspectiveSwitcher.startRole` = Destroyer), with the three asteroids in a row in front of you.
  - Island size: `IslandScale` (2) at the top of `PrototypeSceneBuilder`. The islands are scaled as a whole, so plants, fish, butterflies and the glowing deer grow with them; the ring they sit on grows too so they don't overlap. Rebuild the scene after changing it.
  - The hint panel shows **GROWING ×n / SHRINKING ×n** during the move.
  - Comfort: the scale follows a log curve (steady doubling); if you moved while in one role, the rig glides back to the anchor during the same motion.
  - Tune **Grow Time** on `PerspectiveSwitcher` (GameSystems), or untick **Grow Transition** for the old fade. Positions live in `PrototypeSceneBuilder` (`DestroyerAnchorPos`, `AsteroidLocal*`): **re-run Build Core Prototype Scene** after changing them.
  - The Meta XR Simulator shows one eye only. On a real Quest the distance between your eyes is scaled ×20 too, so the islands really look like a miniature.
- **Echo of your other self (`RoleEcho`, added automatically):** when you switch, a faint glowing copy of the self you leave stays behind in the pose you had. As the giant, look down: your golden Builder (head, body, hands) stands among the islands, the size of your thumb. Back as the Builder, look up: the giant's blue hands (hands only, no head) still hang by the asteroids. **Strength** sets how bright the echo is.
- **Hands of light:** Meta hand mesh with the `GhostHand` rim-glow shader. Builder = gold; Destroyer = blue and 1.3× larger. Fingers curl with GRIP (Destroyer: GRIP or TRIGGER). If fingers bend backwards, flip **Curl Sign** on `HandPose`. Colliders and gameplay are unchanged.

**Islands and life**

- **Islands:** floating rock with a jagged inverted-cone underside, all in one stone material (Asteroids pack). Each island picks one of 3 variants from the number in its name (`ArtVariant`).
- **Slot markers:** soft breathing glowing rings (gold = rock, cyan = ice, violet = crystal).
- **Lake:** custom `StylizedWater` shader. Once the lake is full, a **stream** (`FlowingWater` + `StreamReveal`) runs across the island and spills over the edge as a waterfall into space.
- **Life:** Flora pack grass lawn with small flowers, bushes with ferns, mushrooms and pebbles, reeds, three trees, and a lotus with pads. Fish come from Floreswa. Plants are capped at 2.5× their height in width, so flat models can't blow up.
- **Moving life (`Critters`, on every island, added automatically):** the fish swim round the lake with a little wiggle (tick **Fish Reverse** if one swims backwards), butterflies (different colours per island) flutter over the island and rest now and then once the plants have grown, dragonflies hover and dart over the lake once the water life is there. All procedural, drawn with the `CritterWing` shader (no textures); *Apply Imported Art* creates `CritterWing.mat` and assigns it (without it only the fish move). They vanish with a ruined island and come back when it is rebuilt.
- **Deer of light (`GlowDeer`, added automatically):** once all three islands are alive, a deer built from simple shapes and drawn with the glowing `GhostHand` shader (strong inner fill, soft gold, gold motes drifting off it) fades in on `Island_3`. It then lives between the islands: on each one it grazes the lawn, drinks at the lake, looks up at you, trots to the edge and **leaps across the gap** to a neighbouring island. Ears flick, the tail swishes. If an island is ruined it fades away and returns when all live again. About 11 cm tall (back) at the default **Size** 2.4. Options: **Start Island**, **Colour**, **Inner Fill**, **Motes**, **Size**, **Walk Speed**, **Leap Height / Time**, **Wait For All Islands**.
- **Fragments:** rock (Mid Moon), ice (Asteroid_5 shape in an ice material with snowflake sparkles), crystal (SineVFX).

**Sky and the reveal**

- **Sky:** Nebula 02 cubemap; ambient light sampled from the sky. The star particles twinkle (`TwinkleStar` shader, each star has its own speed; a few flare now and then), and `ShootingStars` sends a silent, straight shooting star (a 2-point line, head bright, tail fading) every 8–15 s, within 50° of where you are looking and 15–40° above the horizon (**View Spread**, **Elevation**, **Interval**, **Tail Length** on `Stars`).
- **Reveal threads:** soft glowing curves (`LightThread`) from each island to its asteroid. At the reveal **the world stands still**: loose fragments freeze where they are and the islands and asteroids don't move, only the threads grow and glow (the little creatures keep living). In the shared space the three asteroids hang in front of you above the islands, so every thread ends where you can see it by looking up. `RevealThreads` options: **Freeze Fragments** (on), **Gather Asteroids** (off; glides the asteroids closer into view instead).

**Hints and sound**

- **Hint text:** 3D `TextMesh` with the `HintText` shader (Canvas text flickered in the headset). `HudFollow` lets it float in front of you, recentre when you look away, and scale with the giant Destroyer rig. Tweak **Line Height** / **Wrap Chars** on `FlowHints` and **Dead Zone** / **Settle Angle** on `HudFollow`.
  - Layout, the same for every step: title (role + "step n of 2") → **GOAL** → **HOW** → **DONE** (progress) → **KEYS**. Once the Destroyer has opened all three asteroids a gold **► NEXT** line tells you to press A / B.
  - Progress dots: Destroyer = one dot per opened asteroid; Builder = three dots per island (rock gold, ice blue, crystal violet; a grey ring = still missing), and an island's name turns gold when it is alive.
  - `HudFollow` reads its resting size in `Awake`, before the rig is scaled, so the panel is the right size even though the game starts as the ×20 Destroyer.
- **Sound:** `SoundDirector` (scene object `Audio`) listens to the gameplay events itself:
  - ambience per role, cross-faded on the switch: calm deep-space drones (48 s seamless loop; open chord with no third, slowly beating voices, a low "solar wind", rare far-away glimmers with echoes). Builder = D, a little warmer; Destroyer = A, lower and darker;
  - switch whoosh;
  - asteroid punch and tap (violent break only with `requireGentle`); island ruined / alive;
  - attract hum, grab, rock placed, ice pouring, water filled, crystal, life growing;
  - reveal chord. Shooting stars are silent on purpose.

  The clips are **placeholders synthesised in code** (*Space Between Us → Audio → Generate Placeholder Sounds*) in `Assets/_Project/Audio`. Replace any `.wav` with a real sound of the same name: generation never overwrites an existing file (except *Regenerate All*), unless a recipe's version was bumped in `SoundSynth.Versions` (currently the two ambiences, v2), which rebuilds only those generated files.

**Added in Play mode, not visible in the scene before:** `Critters` (every island), `GlowDeer` and `RoleEcho` add themselves when the scene starts (`RuntimeInitializeOnLoadMethod`), so they work without rebuilding the scene. To tune their options, change them in Play mode to try, then copy the values into the script defaults (or add the component to the scene yourself: an existing one is never duplicated).

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

### 9.6 Building for Quest

1. **Space Between Us → Build → Prepare Quest Build** (once): package name `com.csimvr.spacebetweenus`, only `Prototype_Core` in the build, target API 34 (what Meta's Project Setup Tool asks for). The Meta XR SDK needs compileSdk 35+, so `GradleCompileSdkFix.cs` raises only compileSdk in the generated Gradle project to the newest installed platform (36). Without it Gradle fails in `checkReleaseAarMetadata`.
2. *File → Build Profiles → **Meta Quest*** (active; Android, IL2CPP, ARM64, min API 32, OpenXR) → **Build** for an `.apk`, or **Build And Run** with the headset plugged in.
3. Install on a Quest in developer mode: drag the `.apk` into Meta Quest Developer Hub, or `adb install -r universe.apk`. It shows under *Unknown sources* in the app library.

### 9.7 Troubleshooting: magenta objects / Game view shows only the sky

Run **Space Between Us → Debug → Report Broken Shaders**. It writes `Logs/ShaderReport.txt` (also in the Console): the active render pipeline, quality level, graphics API, and every shader in the scene with errors.

- **`Pipeline: NONE (built-in)`** → URP is not active, so every URP material renders magenta (and nothing in XR). Cause we hit: the **Meta Quest build profile** (`Assets/Settings/Build Profiles/Meta Quest.asset`, created by Meta's setup tool) overrode the quality level with one that does not exist. It now points to **Mobile** (which uses `Mobile_RPAsset`). If it happens again: *File → Build Profiles → Meta Quest → Quality Settings* → choose **Mobile** (or remove the override), then **Switch Profile**. Commit the build profile so teammates get the fix.
- **`FallbackLoading` replacement pass error** in Play → async shader compilation's placeholder doesn't support XR single-pass stereo. *Project Settings → Editor → Shader Compilation* → untick **Asynchronous Shader Compilation** (now off in the project). The first Play stalls while the XR variants compile.
- **A shader listed as BAD** → its compile errors are printed underneath.

### 9.8 Still to check on a real Quest

- Frame rate with everything alive: three islands of plants, fish, butterflies, dragonflies, the deer and its motes.
- Comfort of growing / shrinking ×20 in place (3 s). If it feels too strong, raise **Grow Time** or untick **Grow Transition**.
- How big the ×20 change feels with real stereo (the simulator shows one eye only).
- Hand finger curl direction (`HandPose` **Curl Sign**) and hint text readability in the headset.
- Sound levels on the headset speakers.
