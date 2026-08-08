# Tools — verification without Unity

This project was written in an environment with no Unity installation, so these two
projects exist to catch as much as possible short of running the game. They are ordinary
.NET projects and live outside `Assets/`, so Unity ignores them entirely.

Both compile **the real sources** from `Assets/_Scripts` — nothing is copied or
duplicated — against hand-written stubs of the Unity API in `LogicTests/Stubs/`.

```bash
# Logic tests, and the player-build configuration type-check
dotnet test Tools/LogicTests

# The editor configuration type-check
dotnet build Tools/EditorCompileCheck
```

## What each one covers

| Project | Configuration | Purpose |
|---|---|---|
| `LogicTests` | Player build — no `UNITY_EDITOR`, `Editor/` excluded | NUnit tests over the game's pure logic |
| `EditorCompileCheck` | Editor — `UNITY_EDITOR` defined, everything included | Type-checks the asset and scene generators |

Between them, both configurations Unity itself builds are type-checked.

## The stubs

`LogicTests/Stubs/` reimplements only the members the game touches, across
`UnityEngine`, `UnityEngine.UI`, `Unity.Netcode` and `UnityEditor`.

Most of it is inert — a `Rigidbody` that stores values and does nothing. But anything the
tests actually depend on is **implemented faithfully**, because a stub that lies makes a
green test worthless:

- `Mathf.Round` uses banker's rounding, matching `Math.Round`, because grid snapping
  depends on the tie-breaking behaviour.
- `Vector3` and `Color` do real arithmetic.
- `Quaternion` does real quaternion multiplication. Its `eulerAngles` is a yaw/pitch/roll
  extraction — exact for the single-axis rotations this project builds, but *not* a
  general ZXY decomposition. Don't write tests that decompose a composed rotation.

### Limits, stated plainly

The stubs are a reconstruction of Unity's API from memory. Where a remembered signature
is wrong, the stub and the call site can be wrong together and this harness will happily
pass. It also does not run:

- Netcode's RPC source generators, so RPC parameter-type rules are unverified
- Unity's own analyzers
- anything at all about runtime behaviour, physics, rendering or networking

It is a net for typos, wrong signatures, bad types and broken arithmetic. It is not a
substitute for opening the project and playing it.

## What the tests assert

71 tests. The GDD gives exact formulas and worked examples; the tests check the
implementation reproduces them.

- **Economy values** — the room score formula and its worked example from GDD §17, the
  inspection breakdown from §19, the profit calculation from §19, and the loss from the
  chaos scenario in §28. Plus the invariant that the four room-score weights sum to 1,
  which is what makes a perfect room score exactly 100.
- **Economy systems** — the live `BudgetManager`, `RoomController` and
  `HouseValueManager` with `IsServer` forced on. Covers the GDD §15 rule that purchases
  are blocked rather than overdrawn (including the exact-budget boundary), that spending
  is split into the renovation and furniture lines §19 prints separately, that a room
  derives its four sub-scores correctly, and that only the server can move shared state
  (GDD §22).
- **Awards** — the GDD §20 table, and the two properties the screen depends on: every
  player receives exactly one award, and no award is handed out twice. Also that
  client-supplied names and huge stat values can't overflow the `FixedString` fields.
- **Placement and paint** — grid snapping (including that it's idempotent, so a held
  ghost doesn't drift), the seven-colour palette, harmony rules, and carry weights.

### These tests have teeth

A suite that passes because it never reaches the code is worse than none. Three
deliberate mutations were introduced and every one was caught:

| Mutation | Tests failed |
|---|---|
| Room score weight `0.30` → `0.35` | 2 |
| Budget check `<` → `<=` (off-by-one on an exact-budget purchase) | 2 |
| Grid snap footprint offset dropped | 1 |

Worth repeating after adding tests, since it is the only way to tell a real assertion
from a vacuous one.
