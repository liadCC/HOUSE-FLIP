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
   prefabs, the ScriptableObject catalog and the scene at
   `Assets/_Scenes/HouseFlip_MVP.unity`, and adds it to Build Settings.
3. Press **Play**, then **HOST GAME**.
4. To test multiplayer, build a standalone player (or use ParrelSync / a second editor)
   and press **JOIN GAME** against `127.0.0.1:7777`.

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
  Editor/       Scene and asset generators
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

Phases 1–17 of the development order (GDD §5) are implemented. Phase 18 (polish) is not.

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
| Audio | Wired, **no clips shipped** — see below |

### What is deliberately missing

- **Audio clips.** `AudioManager` is fully wired to the event bus and every trigger in
  GDD §24 fires, but the project ships no `.wav` files, so it runs silent. Drop clips
  into the `sfx` list on the `UI` object and they play.
- **Art.** Everything is coloured primitives. The prefabs are structured so a mesh swap
  is all that's needed — no gameplay data lives in the models.
- **Animation.** No locomotion blend tree; characters slide. GDD §6 calls this
  sufficient for MVP.
- **Polish (Phase 18).** No particles, camera shake or juice. Hook points exist
  (`Destructible.debrisEffect`, `DirtSource.cleanEffect`, `BurstPipe.sprayEffect`).

### Not yet verified

The C# is syntax-checked but **has not been compiled against Unity's assemblies or run** —
there is no Unity installation in the environment this was written in. Expect to fix
compile errors on first open, most likely around Netcode API drift between versions.
Nothing here has been playtested, so the balance numbers in `GameConstants` are a
first pass, not tuned values.

## Post-MVP

The architecture in GDD §26 is accounted for: `RoomController` is self-registering so
new houses need no wiring, `PlaceableData` is open for luxury items, `RandomEventHandler`
takes new events without touching the scheduler, and `NetworkBootstrap` isolates the
transport so Steam lobbies replace one class.
