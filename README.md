# CheckOut LookOut

CheckOut LookOut is a first-person supermarket-security game created in Unity for the
Year 2.1 Integrated Studio Project. The player works as a new security guard,
monitors a living supermarket through CCTV cameras, identifies suspicious
customers, and attempts to catch shoplifters before too many escape.

The project combines an animated 3D environment, autonomous NPC behaviour,
Unity navigation, physics-based interactions, visual feedback, dialogue, audio,
and a multi-day difficulty system.

## Gameplay

At the beginning of the first day, the player receives a briefing from the
cashier and travels to the security workstation. Activating the workstation
opens the CCTV system and begins the shift.

NPCs have weighted traits that affect their age, movement speed, spending type,
urgency, browsing behaviour, shopping route, checkout behaviour, and dialogue.
Some customers pay normally, while shoplifters attempt to leave without paying.
The player must use visible and spoken tells to decide whom to report.

Reporting an innocent shopper returns a negative result. Correctly reporting a
shoplifter removes that NPC and releases any shelf, queue, cooking, seating, or
door occupancy they held. Allowing too many shoplifters to escape ends the run
and displays the number of days survived.

## Main features

- First-person movement and raycast-based interaction
- Living supermarket populated by shoppers, shoplifters, passers-by, and cars
- Trait-driven NPC variation
- NavMesh-based movement and dynamic obstacle avoidance
- Shopping, browsing, product-grabbing, checkout, cooking, seating, and exit states
- Cashier dialogue and contextual NPC speech bubbles
- Interactive CCTV reporting system with multiple cameras
- Day progression with increasing customer and shoplifter counts
- Success, failure, and days-survived feedback
- Styled title, settings, and pause menus
- Persistent Master, BGM, and Sound Effects volume controls
- Developer console and optional NPC/interaction debug views

## Controls

| Input | Action |
| --- | --- |
| WASD | Move |
| Mouse | Look and navigate menus/CCTV |
| E | Interact |
| Left Mouse Button | Advance dialogue or report an NPC in CCTV |
| A / D or Left / Right Arrow | Change CCTV camera |
| Escape | Pause, go back, or continue |
| F6 | Toggle the developer console |

Jumping is intentionally disabled.

## Project requirements

- Unity Editor `6000.3.13f1`
- Universal Render Pipeline `17.3.0`
- Input System `1.19.0`
- AI Navigation `2.0.12`
- ProBuilder `6.0.9`

Use the exact Unity editor version where possible to avoid unintended scene,
prefab, lighting, or package upgrades.

## Opening the project

1. Clone the repository with GitHub Desktop or Git.
2. Open Unity Hub and add the cloned project folder.
3. Open the project with Unity `6000.3.13f1`.
4. Allow Unity to import the assets and restore packages.
5. Open `Assets/Scenes/Home_Screen.unity`.
6. Enter Play Mode.

The Build Settings scene order should be:

1. `Assets/Scenes/Home_Screen.unity`
2. `Assets/Scenes/Main_Scene.unity`

## Technical overview

The main systems are stored under `Assets/Scripts`:

- `PlayerController` and `PlayerInteraction`: first-person control and raycasting
- `NpcTraits`: weighted, data-only NPC trait selection
- `NpcNavigation`: NPC state flow and NavMesh movement
- `NpcSpawningPad` and `NpcDespawningPad`: paired NPC entry and exit routes
- `ShelfStation`, `OpenFridge`, and `IceCreamMachine`: products and occupancy
- `CheckoutStation` and `CashierInteractable`: payment and dialogue sequences
- `CookingStation` and `NPCSitting`: optional post-purchase behaviour
- `CctvSystem`: cameras, highlighting, reporting, and shoplifter removal
- `DayNightCycle`: day timing, quotas, progression, and failure state
- `StartMenuController` and `PauseMenuController`: menus and audio settings
- `DeveloperConsole`: debug commands and visualization toggles

All gameplay scripts use project file headers. Public systems and key entry
points should continue to receive XML documentation as development continues.

## Integrated Project brief evidence

### I3E

- NPCs react to traits, product availability, occupancy, queues, other NPCs,
  player reports, and the current day state.
- NPC and vehicle movement uses Unity's baked Navigation system.
- Player and CCTV targeting use raycasting.
- Character collision, trigger interactions, doors, despawning pads, and
  obstacle avoidance use Unity physics features.
- The NPC journey is implemented as a sequence of readable behavioural states.
- The project is managed with Git and hosted on GitHub.

### STLD and 3RT

- The project contains a modular store and surrounding streetscape.
- Unity Terrain, URP lighting, post-processing, and baked navigation are used.
- Texture atlases and trim-sheet textures are stored under `Assets/Textures`.

Evidence of individual asset authorship, required VFX counts, mixed-light baking,
optimization, testing, and the required `3RT_` submission naming convention must
also be prepared by the team outside the code repository.

## Audio credits

The following third-party audio is included in the project. All music and sound
effects remain the property of their respective creators and rights holders.

### Background music

- [Undertale Hotel](https://www.youtube.com/watch?v=CgvMoz2LnWA)
- [Bensound - Elevator Bossa Nova](https://youtu.be/cw11meDvtLw?si=GMFs-MfkEecDX05R)
- [nico's nextbots OST - Shop](https://www.youtube.com/watch?v=zNHC_efucwo)
- [Undertale Shop](https://www.youtube.com/watch?v=R0uNPIa-I9c)
- [Fashion Deep House by Infraction / Minimize](https://www.youtube.com/watch?v=-mDaDr7_ias&list=PL7pkSK1xbGD6HelZql4Ilv2QAWZdR1V3u)

### Sound effects

- [Cash Register (Kaching) Sound Effect - Free Download & No Copyright](https://www.youtube.com/watch?v=iExbe5qXq8Q)
- [Security Camera Move Loop (Fortnite Sound) - Sound Effect for Editing](https://www.youtube.com/watch?v=z7yYPRs_gQg)
- [FNAF - 6 AM Sound](https://www.youtube.com/watch?v=6UvimAzSkZY)
- [Short Police Siren Sound Effect Download](https://www.youtube.com/watch?v=I2eDuM286oI)

These links document where the project obtained the audio. They do not replace
the original creators' licence terms. Before publicly distributing or selling a
build, verify that every track permits the intended use and redistribution.

## Other credits and dependencies

- Unity Technologies: Unity Engine, URP, AI Navigation, Input System, and ProBuilder
- Quick Outline: object highlighting used for interactable objects and CCTV NPCs
- Mixamo: character animation workflow used for NPC animations
- OpenAI Codex: development assistance; include this usage in the required GenAI declaration

Add the team members, individual roles, original asset responsibilities, and any
additional external models, textures, fonts, references, or plugins here before
submission.

## Submission checklist

- Tutor approval for the selected theme or Big Idea
- Three-person team and individual FSM contribution evidence
- Jira/Scrum board with regular weekly updates and assigned tasks
- Git/GitHub history demonstrating work by each teammate
- Figma companion prototype with at least five functions and Google Sheets data
- Persona, journey, user flows, sitemap, hardware research, and usability tests
- Game/level design documentation and visual research
- Original modular asset, texture-atlas, lighting, post-processing, and VFX evidence
- Complete third-party credits and verified licences
- GenAI usage reflection/declaration
- Final presentation, trailer/screenshots, major features, and unique selling point
- Standalone build test covering title, settings, gameplay, pause, CCTV, day end,
  failure, return to title, and desktop exit

## Repository

[GitHub repository](https://github.com/pikapokaboo/WAWD-IP)
