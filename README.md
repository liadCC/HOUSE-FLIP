# HOUSE FLIP

> Buy a rundown house, renovate it with your friends, don't destroy it completely, and try to sell it for a profit.

Co-op multiplayer, physics-based, casual. 1–4 players online. Unity + Netcode for GameObjects.

The design document this implementation follows is in [`docs/GDD.md`](docs/GDD.md).

---

## Getting it running

1. Open the project in **Unity 6000.0.32f1** (any Unity 6 LTS should work). The first
   open pulls `com.unity.netcode.gameobjects` and the other packages from
   `Packages/manifest.json`.
2. Menu → **House Flip → Build MVP Scene**. This generates the placeholder art, the
   prefabs, the ScriptableObject catalog, the synthesised sound set and the scene at
   `Assets/_Scenes/HouseFlip_MVP.unity`, and adds it to Build Settings.
3. Press **Play**, then **HOST GAME**.
4. To test multiplayer, build a standalone player (or use ParrelSync / a second editor)
   and press **JOIN GAME** against `127.0.0.1:7777`.

*House Flip → Generate Placeholder Audio* regenerates just the sound set if you want to
re-roll it without rebuilding the scene.

The scene is a build artefact — regenerating it discards hand edits. That is deliberate
while systems are still moving; once the art pass starts, stop regenerating and edit the
saved scene directly.

## Controls

| Input | Action |
|---|---|
| `WASD` | Move |
| `Shift` | Sprint |
| `Space` | Jump |
| `Mouse` | Orbit camera |
| `E` | Interact — the single context prompt (GDD §7) |
| `1`–`5` | Hammer / Screwdriver / Wrench / Vacuum / Paint Roller |
| `0` | Put tool away (bare hands, for grabbing) |
| `F` | Furniture catalog |
| `B` | Build menu |
| `C` | Paint colour picker |
| `LMB` | Throw carried object / confirm placement |
| `G` | Drop carried object |
| `X` | Sell carried furniture back |
| `R` | Rotate placement ghost |
| `Shift`+click | Place repeatedly without leaving placement mode |
| `Esc` / `RMB` | Cancel placement |

## Architecture at a glance

```
Assets/_Scripts/
  Core/         Event bus, constants, layers, singleton bases, enums
  Player/       Controller, camera rig, carry, tools, per-player stats
  Interaction/  IInteractable + IToolGated + the Interactor that resolves [E]
  Physics/      Grabbable: mass categories, co-carry, throwing
  Demolition/   Destructible: hit points, damage states, rubble
  Building/     Grid snapping, ghost preview, placement validation, catalog
  Furniture/    Furniture data and placed-item bookkeeping
  Cleaning/     DirtSource
  Repair/       RepairableFixture
  Painting/     PaintableWall + palette
  Economy/      BudgetManager, HouseValueManager, RoomController, RoomScore
  Events/       Random event framework + the three MVP events
  Networking/   Bootstrap, transforms, the RenovationService RPC gateway
  GameFlow/     GameManager state machine, timer, lobby, inspection, awards
  UI/           HUD, prompts, catalog, inspection and awards screens
  Audio/        AudioManager driven off the event bus
  Polish/       Camera shake, debris, hit flash, scale punch
  Editor/       Scene, asset and audio generators
```

Three rules hold the whole thing together:

**One source of truth per shared value.** `SharedBudget`, `HouseValue`, the timer, room
cleanliness and event state are all server-owned `NetworkVariable`s. No client ever
computes them; clients read the replicated value and render it. Every mutation goes
through a `ServerRpc`, and the server re-derives the sender's position and equipped tool
before allowing anything (`ActorValidator`).

**Managers talk through `GameEvents`, not to each other.** Gameplay raises; UI and
managers listen. `HouseValueManager` recalculates when it hears `HouseStateDirty`, and
it has no idea who raised it. Adding a system means adding a listener.

**All item data lives in ScriptableObjects.** Furniture, building pieces and random
events are assets, not code. New content is a new asset plus a catalog entry.

### Interaction resolution

One object often carries several interactions — a wall is both paintable and smashable,
a fridge is both grabbable and sellable. `IToolGated` disambiguates: the equipped tool
picks the interaction, and with no matching tool the Interactor falls back to whichever
one still has something to say, so an empty-handed player gets the "Need Hammer" hint
rather than silence.

## Implementation status against the GDD

All 18 phases of the development order (GDD §5) are implemented.

| MVP feature (GDD §4) | Status |
|---|---|
| Online co-op, up to 4 players | Implemented — connection approval caps at 4 |
| Lobby: create / join / ready-up | Implemented — direct IP |
| Third-person controller, camera | Implemented |
| Interaction system, single `[E]` | Implemented |
| Physics grab, co-carry, throw | Implemented |
| House: 5 rooms + yard | Implemented — generated |
| Demolition | Implemented — state-based, per GDD §9 |
| Building (grid + green/red ghost) | Implemented |
| Cleaning / Repair / Painting | Implemented |
| Furniture buy + place | Implemented — 21 items |
| Shared budget, house value | Implemented — server-authoritative |
| Timer, inspection, sell, profit | Implemented |
| Player awards | Implemented — 6 individual + team |
| 3 random events | Implemented — leak, outage, inspector |
| HUD | Implemented |
| Audio | Implemented — all 11 GDD §24 triggers, synthesised placeholder clips |
| Polish (Phase 18) | Implemented — shake, debris, hit flash, HUD punch |
| Art | Toon shader, gradient sky, chamfered meshes, one palette |
| Animation | Procedural walk, carry, land, tool swing |
| Balance | Tuned against a solvable model — see below |

### Audio

Rather than ship silent, the project synthesises its own sound set. `SfxSynth` builds
each cue from sine sweeps, filtered noise and decay envelopes, `WavWriter` encodes them
to 16-bit PCM, and the scene builder wires them into `AudioManager` by `SfxId`. There is
also a 16-second looping backing track — a walking bass under a C-major arpeggio.

They are placeholders and they sound like it. Replacing one means dropping a real `.wav`
over the generated file: the manager looks clips up by id, never by filename.

### Polish (Phase 18)

- **Camera shake** — trauma-based, decaying, Perlin-driven, with quadratic falloff by
  distance so a wall coming down across the house rumbles rather than jolts.
- **Debris** — real pooled rigidbody chunks tinted to the destroyed object's colour.
  Physical rather than particles on purpose: in a physics comedy, rubble that skitters
  under the sofa earns its cost. Purely local, never networked.
- **Hit flash** and **scale punch** on impacts, placements and changing HUD numbers.
  The house value label tints green or red by direction of change.

### Art

- **Toon shader** (`Assets/_Art/Shaders/Toon.shader`) — lighting quantised into bands
  rather than a smooth ramp, with shadows tinted cool instead of merely darkened, plus a
  rim light so silhouettes stay legible in small rooms. Falls back to Standard if it
  fails to compile, rather than turning the house magenta.
- **Gradient skybox**, distance fog, warm key light against cool ambient. That warm/cool
  contrast is what gives untextured geometry depth.
- **Chamfered geometry** (`Art/MeshFactory`) — props are generated meshes with bevelled
  edges, not Unity cubes. 44 triangles, and the bevel catches a highlight all the way
  round, which is most of what separates stylised low-poly from "box".
- **One palette** (`Art/ArtPalette`) — every colour in the game comes from it.

### Animation

Procedural, no Animator and no rig. For blocky characters this beats a thin blend tree:
it responds to real speed, never foot-slides, and squash-on-landing costs three lines.

Everything is derived from **observed motion** — how far the transform actually moved —
rather than from input. That is what makes remote players animate correctly: their
transforms arrive over the network with no input attached, and they walk, swing and land
exactly like the local one.

Walk cycle with counter-swinging arms, body bob and lean, idle breathing, volume-preserving
squash on landing, a two-handed carry pose, and an asymmetric tool swing (fast down-stroke,
slower recovery) triggered by hammer blows and paint strokes.

### What is still missing

- **Meshes are still generated boxes.** Chamfered and well lit, but a sofa is a box. The
  prefabs are structured so a mesh swap is all that's needed.
- **No facial animation or hand IK.** The character has a snout so you can tell which way
  it faces; that is the extent of the expression.

### Verification status

There is no Unity installation in the environment this was written in, so the code is
verified as far as it can be short of running the game. See [`Tools/`](Tools/README.md).

```bash
dotnet test Tools/LogicTests          # 99 tests + player-build type-check
dotnet build Tools/EditorCompileCheck # editor-configuration type-check
```

1. **Type-checked** against hand-written stubs of the UnityEngine, uGUI, Netcode and
   UnityEditor surfaces, in both configurations Unity itself builds — editor
   (`UNITY_EDITOR` defined, all scripts) and player (Editor scripts excluded). Both
   clean, zero errors and zero warnings.
2. **Tested** — 99 NUnit tests running the real sources, asserting the economy formulas
   reproduce the worked examples printed in GDD §17, §19 and §28, that the shared budget
   refuses to overdraw, that the awards allocation satisfies its one-each/no-duplicates
   properties, and that grid snapping is idempotent. The suite was mutation-checked:
   three deliberately introduced bugs were all caught.
3. **Executed** for the audio path, which has no Unity dependency: all 17 clips
   generated, header-validated, level-checked. (This found a real bug — the de-click fade
   was erasing the attack transient of percussive sounds.)

What none of that covers: Netcode's RPC source generators, Unity's analyzers, and
anything about runtime behaviour, physics, rendering or networking. **Nothing here has
been played.** Expect to fix some things on first open.

## Balance

The economy is tuned against a solvable model (`Assets/_Scripts/Balance/`) rather than by
guesswork. `HouseDefinition` and `CatalogDefinition` hold the layout and catalog as plain
data; the scene generator builds from them and `BalanceModel` reads the same tables, so
the model cannot drift from the game. It solves the shopping decision as a knapsack under
the budget.

| Round | Profit |
|---|---|
| Untouched | −$25,200 |
| Shop hard, skip the work | −$24,622 |
| Clean only | −$7,200 |
| Clean + repair, no shopping | +$14,130 |
| Best play | +$21,998 |

Repairs are the pivot from loss to profit, so a first round teaches the right lesson.
The full shopping list costs $23,935 against a $20,000 budget, which is what makes the
catalog a decision rather than a checklist.

**Known shortfall:** four players finish this house in about four minutes of a
25-minute round. The content is thin for a full team. The honest fixes are a shorter
round or a bigger house — both design calls, so a test records the fact rather than
hiding it. `TimerManager.sessionSeconds` is serialized if you want to try ~10 minutes.

## Post-MVP

The architecture in GDD §26 is accounted for: `RoomController` is self-registering so
new houses need no wiring, `PlaceableData` is open for luxury items, `RandomEventHandler`
takes new events without touching the scheduler, and `NetworkBootstrap` isolates the
transport so Steam lobbies replace one class.
