# Dash: 3D Track Running Game

A multiplayer 3D track running game built in Unity for **COMP602 (2026, Semester 2)**.

Players gather in a shared lobby, customise their runner, warm up on a practice sprint, then race up to eight runners (other players or AI bots) by alternating two pedal keys in rhythm. Pedal too fast or hit the same key twice and your runner stumbles. Each race brings its own weather and puddles to deal with.

> **All rights reserved.** This repository is shared for assessment and portfolio viewing only. Please read the [License](#license) section before using anything here.

---

## Contributors

- Om Patel
- Brayden Johns
- Mugisha Gwassa
- Fredrick Dela Cruz
- David Kothapally

---

## Features

- **Online multiplayer** with host-authoritative networking (Photon Fusion 2): lobby, race, results and podium phases.
- **Two-key sprint racing:** alternate the pedals at the right rhythm to build speed; mashing or repeating a pedal causes a stumble.
- **AI bots** fill empty slots up to 8 racers, with four difficulty levels (Easy, Medium, Hard, Expert) and random outfits.
- **Cosmetics shop:** head items, body colours and shoes, each with stats (Top Speed, Acceleration, Boost, Grip, Recovery). Loadouts are saved between sessions.
- **Weather and hazards:** each race has 0 to 3 slip patches (puddles) at random spots along the track. More puddles bring overcast skies, rain and slipperier ground; rain-ready shoes and Grip help.
- **Audio:** lobby and race music, countdown voice lines, crowd, footsteps and finish sounds.

---

## Getting started

### Requirements

- **Unity 6000.3.8f1** (Unity 6.3). Other versions may upgrade or break the project.
- **Git LFS:** large binary assets are stored with Git LFS. Install it before cloning:
  ```
  git lfs install
  git clone <repository-url>
  ```
- A **Photon Fusion App ID** (free from the [Photon dashboard](https://dashboard.photonengine.com/)), entered in *Photon Fusion > Realtime Settings* (App Id Fusion).

### Running the game

1. Open the project folder in Unity Hub with Unity 6000.3.8f1 and let it import.
2. Open **`Assets/Scenes/Main_Menu.unity`**. Play mode must start from the Main Menu scene.
3. Press Play, then host or join a game.

### Controls

| Action | Key |
|---|---|
| Move in the lobby | WASD |
| Sprint (lobby) | Left Shift |
| Race pedals | Left Arrow and Right Arrow, alternating (rebindable in Settings) |
| Pause menu | Esc |

### Debug keys (host only)

| Key | Effect |
|---|---|
| O | Start a race without using the race console |
| B | Open player management (ban / unban players) |

---

## Project structure

| Folder | Contents |
|---|---|
| `Assets/Scripts` | Game code: race flow (`RaceManager`, `RaceLogic`), players (`Player`), bots (`BotSpawner`, `AIRacer`, `BotColorizer`), cosmetics, weather and hazards (`SlipPatchLayout`, `WeatherVisuals`), audio (`AudioManager`) and UI |
| `Assets/Scripts/Core` | Engine-independent rules (weather and slip maths) with EditMode unit tests in `Assets/Scripts/Tests` |
| `Assets/Scenes` | `Main_Menu`, `Gathering_Lobby` (lobby and race track), `Race_Event1`, `PodiumScene` |
| `Assets/Cosmetics` | Cosmetic item data (head, colour, feet) and the cosmetic database |
| `Assets/Prefabs` | Player, slip patch, UI and other prefabs |
| `Assets/UserInterface` | UI artwork made by the team |
| `Assets/Audio` | Music and sound effects |

Running tests: *Window > General > Test Runner > EditMode > Run All*.

---

## Third-party assets and dependencies

This project uses the free assets and packages below. **They are not covered by this project's license** and remain the property of their creators, under their own terms (for Unity Asset Store assets, the [Unity Asset Store EULA](https://unity.com/legal/as-terms)). Thank you to all of these creators.

### Free Unity Asset Store assets

| Asset | Creator | Used for | Folder |
|---|---|---|---|
| [AllSky Free: 10 Sky / Skybox Set](https://assetstore.unity.com/packages/2d/textures-materials/sky/allsky-free-10-sky-skybox-set-146014) | rpgwhitelock | Skyboxes | `Assets/map shit/AllSkyFree` |
| [Grass Flowers Pack Free](https://assetstore.unity.com/packages/2d/textures-materials/nature/grass-flowers-pack-free-138810) | ALP | Grass and flower details | `Assets/map shit/ALP_Assets` |
| PBR Grass Textures |  | Ground and grass materials | `Assets/map shit/PBR_Grass_Textures` |
| Pro Nature Biome: Starter Pack | Finotti Games | Nature models and environment | `Assets/FinottiGames` |
| Foliage Free | Jake  | Trees and foliage | `Assets/Foliage Free` |
| PolyMate Starter (https://assetstore.unity.com/packages/3d/characters/city-folks-polymate-low-poly-modular-character-379848)| ALSTRA INFINITE | Low-poly characters (City Folks, Urban Man) | `Assets/ALSTRA INFINITE` |
| NPC Casual Set  (https://assetstore.unity.com/packages/3d/characters/humanoids/humans/npc-casual-set-00-326131)| Chepatack | Shoe models for cosmetics | `Assets/npc_casual_set_00` |

### Other third-party content

| Content | Creator | Notes |
|---|---|---|
| [Mixamo](https://www.mixamo.com/) character (Y Bot) and animations | Adobe | Jogging, sprinting, falling, getting up, idle, victory, cheering, clapping and dancing animations in `Assets/CharacterAnimations` |
| Music and sound effects | Andrey Sitkov & Q Up Arts | Race music (`Assets/Audio/RaceMusic`), lobby music and racing music |

### Packages and SDKs

| Dependency | Version | Creator |
|---|---|---|
| [Photon Fusion 2](https://www.photonengine.com/fusion) (with Fusion Physics add-on and Photon Realtime) | 2.1.2 | Exit Games |
| Universal Render Pipeline | 17.3.0 | Unity Technologies |
| Input System | 1.18.0 | Unity Technologies |
| Timeline | 1.8.10 | Unity Technologies |
| uGUI and TextMesh Pro | 2.0.0 | Unity Technologies |
| Test Framework | 1.6.0 | Unity Technologies |

Full package list: `Packages/manifest.json`.

---

## License

Copyright (c) 2026 Om Patel, Braydon Johns, Mugisha Gwassa, Fredrick Dela Cruz and David Kothapally. **All rights reserved.**

The original code, art, designs and other content created by the contributors for this project are **not open source**. No licence is granted to copy, modify, publish, distribute, sublicense or sell this work, or any part of it, in source or compiled form, without prior written permission from all of the contributors. Viewing this repository on GitHub, and assessment by COMP602 teaching staff, are permitted.

Third-party assets and dependencies listed above are excluded from this notice and are governed by their own licences. They may not be redistributed from this repository.

See [`LICENSE`](LICENSE) for the full terms.
