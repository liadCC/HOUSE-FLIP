# HOUSE FLIP — Game Design Document (GDD)

**Version:** 2.0
**For:** Claude Code (iterative development reference)
**Engine:** Unity (preferred unless a technical blocker exists)
**Genre:** Co-op Multiplayer | Physics-Based | Casual
**Players:** 1–4 (Online Co-op)
**Target Platform:** PC / Steam
**Visual Style:** Cartoon / Stylized / Low-Poly

---

## 1. One-Line Pitch

> "Buy a rundown house, renovate it with your friends, don't destroy it completely, and try to sell it for a profit."

---

## 2. Core Design Pillars

| Pillar | What it means in practice |
|---|---|
| **Simple** | A new player understands the goal in under 60 seconds |
| **Funny** | Physics and systems naturally generate chaotic, comedic moments |
| **Co-op** | More players = faster work AND more chaos |
| **Replayable** | Each house feels different via layout, tasks, items, and random events |
| **Satisfying** | Players visually watch a ruined house become a beautiful, valuable one |

---

## 3. Core Gameplay Loop

```
Lobby → Invite Friends → Select House → Purchase House
→ Receive Renovation Budget → Enter House
→ [Demolish | Clean | Repair | Design | Place Furniture | Paint]
→ Final Inspection → Sell House → Calculate Profit
→ Receive Rewards → Start New Round
```

Every round must be completable in approximately **25 minutes**.

---

## 4. MVP Scope

The MVP is **one house, one map, up to 4 players**.
Do not build everything at once. Implement one system, test it, fix bugs, then move to the next.

### MVP Feature Checklist

- [ ] Online Co-op (up to 4 players)
- [ ] Lobby system (create, join, invite, ready-up)
- [ ] Third-person character controller (move, jump, run, interact)
- [ ] Interaction system (single unified [E] prompt)
- [ ] Physics / grab system
- [ ] One house with: Living Room, Kitchen, Bedroom, Bathroom, Hallway, Yard
- [ ] Demolition system
- [ ] Building / placement system (grid-based)
- [ ] Cleaning system
- [ ] Repair system
- [ ] Painting system
- [ ] Furniture system (buy + place)
- [ ] Shared money and budget
- [ ] House Value system (real-time updates)
- [ ] Timer (25 minutes)
- [ ] House Inspection screen
- [ ] Sell + Profit calculation
- [ ] Player Awards screen
- [ ] At least 3 random events
- [ ] Basic physics (pickup, throw, push, fall)
- [ ] Basic audio (SFX + background music)
- [ ] HUD (budget, timer, house value, interaction prompts)

---

## 5. Development Order (Phases)

Implement strictly in this order. Do not skip ahead.

| Phase | System | Done When |
|---|---|---|
| 1 | Project setup | Scene loads, no errors |
| 2 | Player Controller | WASD movement, jump, run in scene |
| 3 | Camera | Smooth third-person follow camera |
| 4 | Interaction System | Press [E] near object → callback fires |
| 5 | Physics / Grab | Pick up, carry, throw, push objects |
| 6 | Basic House | All rooms exist, player can walk through |
| 7 | Demolition | Hammer tool destroys tagged objects |
| 8 | Building | Grid placement with green/red preview |
| 9 | Furniture Placement | Buy from catalog, place in room |
| 10 | Money / Budget | Shared float, deducts on purchase |
| 11 | House Value | Recalculates on every state change |
| 12 | Timer | Counts down, triggers Inspection at 0 |
| 13 | Inspection Screen | Displays all scores and bonuses |
| 14 | Sell / Profit | Final value - total investment = profit |
| 15 | Multiplayer Sync | All above systems networked |
| 16 | UI / HUD | Full HUD visible in-game |
| 17 | Audio | SFX and music hooked up |
| 18 | Polish | Particles, camera shake, juice |

---

## 6. Player Character

Each player character has:

- **Movement:** WASD, sprint (Shift), jump (Space)
- **Camera:** Third-person, orbits around player
- **Interaction:** Single [E] key, context-sensitive
- **Carry:** One object at a time (small/medium objects)
- **Co-carry:** Two players can grab the same heavy object
- **Tools:** Hammer, screwdriver, cleaning tool, paint roller, wrench

Visual style: Cartoon, slightly exaggerated proportions. No complex animations required for MVP — basic locomotion blend tree is sufficient.

---

## 7. Interaction System

When the player looks at an interactable object within range:

- Show prompt: **[E] {ActionName}** (e.g., `[E] Smash`, `[E] Grab`, `[E] Paint`, `[E] Fix`)
- Press E → trigger the registered callback on that object

All interactable objects must implement a shared `IInteractable` interface:

```csharp
public interface IInteractable
{
    string GetPromptText();
    void OnInteract(PlayerController player);
}
```

This keeps all interaction logic modular and consistent.

---

## 8. Physics & Grab System

Objects have a `Rigidbody` and are tagged with a mass category:

| Category | Examples | Requires |
|---|---|---|
| Light | Chair, lamp, box | 1 player |
| Medium | Table, shelf, toilet | 1 player |
| Heavy | Sofa, fridge, wardrobe | 2 players |

- **1 player on heavy object:** object does not move, prompt changes to `[E] Too Heavy — Need Help`
- **2 players grab same object:** both contribute force, object moves together
- Throwing should feel exaggerated and funny — apply extra impulse on release

---

## 9. Demolition System

The player uses the **Hammer** tool on `Destructible` objects.

Each destructible has:

- `int hitPoints` (e.g., 3 hits to destroy)
- A "damaged" visual state (swap mesh or apply decal)
- A "destroyed" state (replace with rubble prefab or disable)

On destruction:

- Spawn particle effect (dust/debris)
- Play break SFX
- Apply small physics impulse to nearby objects
- Notify `HouseValueManager` of damage

**Do NOT implement voxel/per-pixel destruction in MVP.** Use mesh-swap or state-based destruction only.

Destructible objects in MVP:

- Certain walls (tagged as non-structural)
- Old cabinets
- Old furniture
- Tiles
- Doors
- Broken fixtures

---

## 10. Building System

After demolishing, the player can build replacement objects.

Placement uses a simple **grid system**:

- Ghost (preview) mesh follows the mouse/controller aim
- Ghost is **green** when placement is valid
- Ghost is **red** when blocked or invalid
- Press [E] or [LMB] to confirm placement
- Deduct cost from shared budget

MVP buildable objects:

- Wall segment
- Door
- Window
- Floor tile
- Cabinet
- Sink

Each buildable object has a `BuildingData` ScriptableObject with:

- `string itemName`
- `float cost`
- `float valueContribution`
- `GameObject prefab`

---

## 11. Furniture System

Players open a **Furniture Catalog** (radial menu or simple list UI).

Each furniture item has:

```csharp
public class FurnitureData : ScriptableObject
{
    public string itemName;
    public FurnitureCategory category;
    public float cost;
    public float houseValueBonus;
    public float designPoints;
    public Vector2Int size; // footprint on grid
    public GameObject prefab;
}
```

**Categories:**

| Room | Items |
|---|---|
| Living Room | Sofa, TV, Coffee Table, Chair, Floor Lamp |
| Bedroom | Bed, Wardrobe, Desk, Bedside Lamp |
| Kitchen | Fridge, Oven, Counter, Sink |
| Bathroom | Toilet, Shower, Sink, Mirror |
| Decoration | Plant, Painting, Rug, Clock |

Placement follows the same grid system as Building.

---

## 12. Cleaning System

Each room has a `CleanlinessLevel` float (0.0 = filthy, 1.0 = spotless).

Dirty objects in the world (trash bags, stains, dust piles, broken items):

- Each has a `DirtValue` float
- Cleaning it increments the room's `CleanlinessLevel`
- Cleaning tool: vacuum / mop (held item, use [E] or hold [E])

`CleanlinessLevel` feeds directly into `RoomScore` and `HouseValue`.

---

## 13. Repair System

Broken fixtures start in a `Broken` state and must be repaired with the correct tool.

| Broken Object | Tool Required | Cost |
|---|---|---|
| Leaking faucet | Wrench | $200 |
| Broken light | Screwdriver | $150 |
| Broken toilet | Wrench | $300 |
| Broken window | Hammer + glass | $400 |
| Faulty outlet | Screwdriver | $250 |

Each repaired object:

- Transitions to `Fixed` visual state
- Triggers `HouseValueManager.OnRepairComplete(float bonus)`
- Costs budget

---

## 14. Painting System

Players equip the **Paint Roller** and select a color.

MVP colors:
`White`, `Black`, `Blue`, `Red`, `Green`, `Yellow`, `Pink`

Each wall has a `PaintableWall` component:

- `currentColor`
- `OnPaint(Color newColor)` — changes material, deducts cost, notifies `RoomScore`

Painting costs a flat fee per wall (e.g., $100/wall).
Some color combinations grant a small **Design Bonus** to room score.

---

## 15. Money & Budget System

The group shares a single `SharedBudget` (authoritative on Host/Server).

```
Initial Budget: $20,000
```

- All purchases (building, furniture, repairs, paint) deduct from `SharedBudget`
- If `SharedBudget <= 0`: purchases are blocked, show "No Budget!" warning
- Budget is displayed on HUD at all times
- All clients receive budget updates via network sync

---

## 16. House Value System

`HouseValueManager` maintains the **current estimated house value** in real time.

```
Base Value: $50,000
```

Value modifiers (additive):

| Factor | Effect |
|---|---|
| Room cleanliness | +Up to $5,000 per room |
| Furniture placed | +Per item's `houseValueBonus` |
| Repairs completed | +Per repair's bonus |
| Design score | +Bonus for matching styles |
| Structural damage | -Per broken wall/fixture |
| Dirt remaining | -Per dirty zone |
| Broken items | -Per unrepaired broken object |

House Value is recalculated every time any state changes.
It is displayed in the top-right of the HUD.

---

## 17. Room Score System

Each room produces a `RoomScore` (0–100) from four sub-scores:

```
RoomScore = (Cleanliness * 0.25) + (Furniture * 0.30) + (Design * 0.20) + (Condition * 0.25)
```

Example output:

```
LIVING ROOM
  Cleanliness:  90
  Furniture:    80
  Design:       70
  Condition:   100
  ─────────────────
  ROOM SCORE:   86
```

Final house score = weighted average of all room scores.

---

## 18. Timer System

```
Session Time: 25:00
```

- Timer counts down and is displayed in top-center HUD (large, visible)
- At `00:00`:
  - All player inputs are disabled
  - `GameManager.TriggerInspection()` is called
  - Transition to Inspection Screen

Timer is authoritative on Host. All clients receive sync ticks.

---

## 19. Inspection Screen

Displayed when the timer reaches zero.

```
═══════════════════════════════
        HOUSE INSPECTION
═══════════════════════════════
  Original Value:      $50,000
  Renovation Bonus:   +$18,000
  Design Bonus:       +$12,000
  Cleanliness Bonus:   +$5,000
  Damage Penalty:      -$3,000
  ───────────────────────────
  FINAL VALUE:         $82,000
═══════════════════════════════
```

Then calculate profit:

```
  House Purchase:     $50,000
  Renovation Spent:   $18,000
  Furniture Spent:     $7,000
  ───────────────────────────
  TOTAL INVESTMENT:   $75,000
  FINAL VALUE:        $82,000
  ───────────────────────────
  PROFIT:             +$7,000
```

---

## 20. Player Awards

At the end of the game, each player receives one humorous award based on tracked stats.

| Award | Icon | Condition |
|---|---|---|
| WRECKING BALL | 🔨 | Most objects destroyed |
| INTERIOR DESIGNER | 🎨 | Highest design score contribution |
| MONEY SAVER | 💰 | Least budget spent |
| DISASTER | 💀 | Most damage caused |
| CLEAN FREAK | 🧹 | Most cleaning done |
| MVP | 🏆 | Highest overall house value contribution |
| TEAM DISASTER | 🤡 | Awarded to the whole team if profit is negative |

Track per-player stats throughout the session using a `PlayerStatsTracker` component.

---

## 21. Random Events

At least **3 random events** must be implemented in MVP.
Events trigger at randomized intervals during the session.

### Event 1 — WATER LEAK

- A pipe bursts in a random room
- Room begins flooding (visual water level rises)
- Players must find and interact with the burst pipe
- Fix with wrench → stops flood
- If not fixed: permanent cleanliness penalty to that room

### Event 2 — POWER OUTAGE

- All lights go off (darken scene, reduce ambient light)
- Players must find the fuse box and interact with it
- Fix with screwdriver → power restored
- While dark: all work is slower / harder to see

### Event 3 — INSPECTION WARNING

- Pop-up appears: **"⚠️ INSPECTOR ARRIVING IN 3 MINUTES"**
- Timer is unchanged but pressure increases
- At 3-minute mark, a visual "inspector" NPC walks through the house
- Inspector checks room states and applies small score modifiers based on what they see

Each event is a `GameEvent` ScriptableObject with:

- `string eventName`
- `string popupMessage`
- `float triggerWindow` (earliest and latest time it can trigger)
- `UnityEvent onEventStart`
- `UnityEvent onEventResolved`

---

## 22. Multiplayer Architecture

**Use Unity Netcode for GameObjects (NGO)** or Mirror — choose one and stay consistent.

### Authority Model

- **Host** = server authority for all shared state:
  - `SharedBudget`
  - `HouseValue`
  - `Timer`
  - `RoomScores`
  - `RandomEvents`
- **Clients** = authoritative over their own character position only

### What must be networked (synchronized)

| System | Sync Method |
|---|---|
| Player position / rotation | `NetworkTransform` |
| Player interactions | `ServerRpc` → `ClientRpc` |
| Object pickup / throw | `NetworkTransform` on carried objects |
| Demolition | `ServerRpc`, then broadcast destroyed state |
| Building | `ServerRpc`, spawn on all clients |
| Furniture placement | `ServerRpc`, spawn + position sync |
| Budget | `NetworkVariable<float>` |
| House Value | `NetworkVariable<float>` |
| Timer | `NetworkVariable<float>` (host drives) |
| Room cleanliness | `NetworkVariable<float>` per room |
| Random events | `ServerRpc` trigger, `ClientRpc` broadcast |

**There must be exactly one source of truth per shared variable.**
Never let clients independently calculate `HouseValue` or `Budget` — always read from the server-authoritative `NetworkVariable`.

---

## 23. UI Layout

```
┌─────────────────────────────────────────────────────────┐
│  [Players + Budget]        [TIMER]        [House Value]  │
│                                                          │
│                                                          │
│                      (game world)                        │
│                                                          │
│                   [E] Interaction Prompt                 │
└─────────────────────────────────────────────────────────┘
```

- **Top Left:** Player list with colored indicators + Shared Budget
- **Top Center:** Countdown timer (large, prominent)
- **Top Right:** Current House Value (updates in real time)
- **Center Screen:** Context-sensitive interaction prompt (`[E] {action}`)
- **All UI elements:** Cartoon style, readable at a glance

---

## 24. Audio Requirements

All audio should feel **Cartoon-exaggerated**, not realistic.

| Sound | Trigger |
|---|---|
| Background music | Looping, lighthearted, upbeat |
| Hammer hit | Each hammer swing |
| Object break | On destruction complete |
| Build complete | On successful placement |
| Object pickup / drop | On grab and release |
| Water gurgle | During water leak event |
| Electrical spark | During power outage event |
| Cash register | On purchase |
| UI button click | On all UI interactions |
| Success fanfare | Profit > 0 on sell screen |
| Fail sound | Profit < 0 on sell screen |

---

## 25. Architecture Guidelines

Build for modularity. All future features listed in Section 26 must be addable **without refactoring core systems.**

### Key principles

- Use **ScriptableObjects** for all item data (furniture, building objects, events, tools)
- Use **events / delegates** (`UnityEvent` or C# `Action`) to communicate between systems — avoid direct references between managers
- `HouseValueManager`, `BudgetManager`, `TimerManager`, `GameManager` are **singletons with network authority on Host**
- All destructible, interactable, and placeable objects are **prefab-based** with a consistent component interface
- Room data is encapsulated in a `RoomController` component, not hardcoded into `GameManager`

### Folder structure (suggested)

```
Assets/
  _Scripts/
    Player/
    Interaction/
    Physics/
    Demolition/
    Building/
    Furniture/
    Cleaning/
    Repair/
    Painting/
    Economy/        ← Budget, HouseValue, RoomScore
    Events/         ← Random events
    Networking/
    UI/
    Audio/
    GameFlow/       ← GameManager, Lobby, Inspection, Results
  _Prefabs/
  _ScriptableObjects/
  _Scenes/
  _Audio/
  _Art/
```

---

## 26. Future Features (Post-MVP — Plan Architecture for These Now)

Do not implement. Do design the architecture so these can be added cleanly.

| Feature | Notes |
|---|---|
| Multiple Houses | Different sizes, prices, difficulty |
| Residents / NPCs | Behavioral agents that roam the house |
| Pets | Physics-driven chaos agents |
| Dynamic Events | More complex cascading events |
| Shop | In-game equipment store |
| Progression System | Unlock tools and upgrades between rounds |
| Luxury Items | High-cost, high-value furniture |
| Achievements | Funny Steam achievements |
| Voice Chat | In-game VOIP |
| Challenge Houses | Special condition houses (haunted, underwater, etc.) |
| Steam Integration | Lobby, friends, cloud saves, stats, achievements |

---

## 27. MVP Definition of Done

The MVP is complete when **4 players can do all of the following in a single session:**

1. Create a lobby
2. Join the same game
3. Enter the house
4. Walk through all rooms
5. Pick up objects
6. Move objects
7. Destroy destructible objects
8. Build replacement objects
9. Clean dirty areas
10. Repair broken fixtures
11. Paint walls
12. Buy furniture from the catalog
13. Place furniture in rooms
14. See a shared budget that updates in real time
15. See house value change in real time
16. Experience at least one random event
17. Have the timer run out
18. See the Inspection screen with correct values
19. See the house sold
20. See Profit (or Loss) calculated correctly
21. See Player Awards
22. Return to lobby and start a new round

---

## 28. Example Chaos Scenario (Design Reference)

This scenario illustrates the intended experience. Systems should make this possible naturally:

> Player 1 swings hammer at a wall →
> Wall clips a pipe behind it →
> **[RANDOM EVENT: WATER LEAK]** triggers →
> Player 2 runs to fix the pipe but hits the fuse box by accident →
> **[RANDOM EVENT: POWER OUTAGE]** triggers →
> House goes dark →
> Player 3 tries to carry the fridge but can't see →
> Fridge falls through window →
> **Timer: 0:00** →
>
> ```
> INSPECTION RESULT
> House Value:  $43,000
> Investment:   $55,000
> PROFIT:       -$12,000
>
> 🤡 TEAM DISASTER
> ```
>
> This is the intended experience. These moments are the game.
