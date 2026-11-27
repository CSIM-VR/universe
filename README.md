# The Space Between Us · Stardust (Unity VR)

A VR experience with two Virtual Subjectivenesses in one world: the **Destroyer** breaks dead asteroids to free rock, ice and crystal; the **Builder** uses them to bring floating islands to life.

Unity 6.6 (exact build in `ProjectSettings/ProjectVersion.txt`) · URP · Meta XR SDK · Meta Quest 3 with controllers

---

## At a glance

**Two Virtual Subjectivenesses (VS)**

|      | **Destroyer**                                                                                                | **Builder**                                                                                  |
| ---- | ------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------- |
| Who  | A giant (×20); the game starts here                                                                          | Human size, standing among the islands                                                       |
| Sees | Asteroids within reach; the islands as a tiny model at your feet; glowing veins inside the asteroids (x-ray) | Asteroids high in the sky; the islands up close: grass, lake, creatures                      |
| Does | Punches asteroids open with **blue hands of light** to free the materials                                    | Pulls, grabs and places fragments with **gold hands of light**; pours water, plants crystals |
| Feel | Power; low, dark ambience                                                                                    | Care and detail; warmer ambience                                                             |

Both share **one space and one spot**: you switch by growing / shrinking in place in 3 s (×1 ↔ ×20), with no cut and no teleport. The role you leave stays behind as a faint glowing echo. Destruction gives the material for creation; at the end, threads of light show the two were never separate.

**Flow (3 steps)**

1. **Destroyer:** punch each of the 3 asteroids 3 times: rock → ice → crystal (9 pieces, exactly enough for 3 islands). Press A / B to shrink.
2. **Builder:** give each island rock → gold ring, ice → blue ring, crystal → violet ring.
3. **Reveal** (automatic when all 3 islands are alive): the world stands still, threads of light link each island to its asteroid. _"Were we ever truly separate?"_

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

---

## Run

1. Clone the repo and open it with **Unity Hub** (same Unity version). If Unity asks, run **Meta XR → Project Setup Tool → Fix All**.
2. Open `Assets/_Project/Scenes/Universe.unity`.
3. Press **Play**. The scene is hand-built: change it directly in the Editor (one person edits it at a time).
   - **Meta XR Simulator** or a Link headset: the controllers drive the rig.
   - **No headset:** desktop controls. Right-drag = look, WASD / QE = move, wheel = reach, Tab = touch the gem. Destroyer: left click = punch. Builder: Space = pull, left click = grab / let go, R = tilt (pour). Backspace = restart.

## Build for Quest

1. **File → Build Profiles → Meta Quest** (must be the active profile; package name, target API 34 and the scene are already set) → **Build And Run** with the headset plugged in and awake, or **Build** for an `.apk`.
2. To install an `.apk` yourself: `adb install -r universe.apk` (or drag it into Meta Quest Developer Hub). On the Quest it is under **Unknown sources**.

If the install step fails with "device not found", wake the headset (put it on) and run Build And Run again.

---

More: setup details, scene wiring, art pass, assets and troubleshooting are in [docs/DETAILS.md](docs/DETAILS.md).
