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

### What is still missing

- **Art.** Everything is coloured primitives. The prefabs are structured so a mesh swap
  is all that's needed — no gameplay data lives in the models.
- **Animation.** No locomotion blend tree; characters slide. GDD §6 calls this
  sufficient for MVP.
- **Tuning.** The balance numbers in `GameConstants` are a first pass, not playtested
  values.

### Verification status

There is no Unity installation in the environment this was written in, so the code was
verified two ways short of running it:

1. **Compiled** against hand-written stubs of the UnityEngine, uGUI, Netcode and
   UnityEditor surfaces it uses, in both configurations Unity itself builds — editor
   (`UNITY_EDITOR` defined, all scripts) and player (Editor scripts excluded). Both are
   clean, zero errors and zero warnings. This catches typos, wrong signatures, missing
   usings and bad types, but the stubs are a reconstruction of Unity's API — where a
   remembered signature is wrong, the stub and the call site can be wrong together.
2. **Executed** for the audio path, which has no Unity dependency: all 17 clips are
   generated, header-validated and checked for level and NaNs. (This found and fixed a
   real bug — the de-click fade was erasing the attack transient of percussive sounds.)

What that does **not** cover: Netcode's RPC source generators, Unity's own analyzers, and
anything about runtime behaviour. **Nothing here has been played.** Expect to fix some
things on first open.

## Post-MVP

The architecture in GDD §26 is accounted for: `RoomController` is self-registering so
new houses need no wiring, `PlaceableData` is open for luxury items, `RandomEventHandler`
takes new events without touching the scheduler, and `NetworkBootstrap` isolates the
transport so Steam lobbies replace one class.
