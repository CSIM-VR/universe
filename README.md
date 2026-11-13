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

1. Install Git and Git LFS, then run once: `git lfs install`
2. Clone the repo and open the folder with **Unity Hub** (same Unity version).
3. Open `Assets/_Project/Scenes/Main.unity`.
4. If Unity asks, run **Meta XR > Project Setup Tool > Fix All**. Platform: **Android**.

**Project settings (already set, don't change):**
Edit > Project Settings > Editor > Asset Serialization = *Force Text*; Version Control = *Visible Meta Files*.

---

## 2. Folder structure

```
Assets/
  _Project/
    Scenes/        Main.unity (+ one test scene per person, e.g. Test_Destroyer.unity)
    Scripts/
      Core/        flow, perspective switch, artifact, hand velocity
      Destroyer/   asteroid, fragments
      Builder/     attractor, island slots, island, life growth
    Prefabs/       Asteroid, Fragment_Rock/Ice/Crystal, Island, Artifact, XR Rig
    Materials/     Shaders/   Audio/   VFX/
  ThirdParty/      imported Asset Store packages (don't edit)
```

---

## 3. Collaboration rules

- **Don't edit `Main.unity` at the same time.** Build and test your part in your own test scene, as a **prefab**. Only one person merges prefabs into `Main.unity`, and says so in the chat first.
- One branch per feature: `feature/destroyer-punch`, `feature/life-growth`… Merge into `main` with a pull request.
- Pull before you start, push when a piece works. Small commits.
- Large files (models, textures, audio) go through Git LFS automatically (`.gitattributes`).

---

## 4. Work split

| Area | What | Main scripts |
|---|---|---|
| **World + switch** | XR rig, central artifact, perspective switch (scale, hands, x-ray layer), 6-step flow, fade | `PerspectiveSwitcher`, `GameFlowManager`, `CentralArtifact` |
| **Destroyer** | Asteroid layers (shell → inside → core), punch / pull apart / throw, fragments drifting to the islands, careful mode (step 4), x-ray shader | `Asteroid`, `MaterialFragment`, `HandVelocityTracker` |
| **Builder** | Attract fragments, place rock, pour ice, plant crystal, island state | `FragmentAttractor`, `IslandSlot`, `Island` |
| **Life + art + sound** | Moss / grass spreading, lake, fish, dragonflies, lotus, fireflies, deer; skybox, sounds | `LifeGrowth` + assets |

---

## 5. Build order (MVP first)

1. **Rig + one island + one asteroid** in a test scene: punch the asteroid, fragments fly out.
2. **Builder basics:** grab a rock fragment, drop it in a slot → moss grows.
3. **Perspective switch:** touch the artifact → scale ×20, swap hands.
4. **Full island recipe:** pour ice (lake), plant crystal (lotus), island alive.
5. **6-step flow** with `GameFlowManager`; careful mode on the last asteroid.
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
Last asteroid: tick `carefulMode`. Events: `onOpened` → `GameFlowManager.AsteroidOpened()`.

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
4. **Destroyer:** the last crystal asteroid sits next to the islands. Punch → fragments smash them; tap gently ×3 → the crystal drifts out safely.
5. **Builder:** plant the last crystal; all three islands alive; a glowing deer comes to drink.
6. **Reveal:** threads of light link each island to the asteroid it came from. *"Were we ever truly separate?"*
