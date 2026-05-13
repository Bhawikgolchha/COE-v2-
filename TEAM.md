# Team workflow — BatteryTwin

This repo is the Unity 6 project for the **Lithium-Ion battery State-of-Health digital twin**.

## First-time setup

1. Install **Unity Hub** + **Unity 6.4 (6000.4.6f1)** or newer.
2. `git clone https://github.com/Bhawikgolchha/COE-v2-.git`
3. Open Unity Hub → **Add → Add project from disk** → pick the cloned folder.
4. First open takes 2–5 minutes (Unity rebuilds the `Library/` cache).
5. When prompted, **Import TMP Essentials**.
6. Menu → **BatteryTwin → Build Scene**. Press ▶ Play.

## Who owns what

| Area | Files | Owner |
|---|---|---|
| **3D scene + UI** | `Assets/Editor/BatteryTwinSceneBuilder.cs`, `Assets/Scripts/BatteryTwin/Bindings/` | UI teammate |
| **Cloud data source** | `Assets/Scripts/BatteryTwin/Data/FirebaseBatteryDataSource.cs` | Cloud teammate |
| **ESP32 firmware + integration** | (separate repo / folder) | Lead |
| **The engine — don't touch unless coordinated** | `Assets/Scripts/BatteryTwin/Twin/TwinController.cs`, `Data/BatteryReading.cs`, `Data/IBatteryDataSource.cs` | Lead |

## Architecture in one breath

```
ESP32 sensors → Firebase Realtime DB
                       │
                       ▼ FirebaseBatteryDataSource (TODO)
                       │ emits BatteryReading
                       ▼
                  TwinController  ──►  Bindings  ──►  Scene visuals
                       ▲
                       │
              MockBatteryDataSource (default, for offline dev)
```

Swap mock ↔ Firebase from the inspector — drag a different `IBatteryDataSource`
MonoBehaviour onto `TwinController.activeSourceBehaviour`.

## What the BatteryReading looks like

`BatteryReading` struct: `CellId, Voltage, Current, Temperature, SOC, SOH, Cycles, Timestamp`.

ESP32 should publish JSON in this exact shape (field names TBD with the team):

```json
{
  "cell_id": "Cell 3",
  "voltage": 3.72,
  "current": -1.25,
  "temperature": 40.2,
  "soc": 12,
  "soh": 78,
  "cycles": 142,
  "timestamp": "2026-05-13T12:45:36Z"
}
```

## Branching

- `main` — stable, always builds.
- Feature branches: `feature/ui-<short-name>` or `feature/cloud-<short-name>`.
- Open PRs into `main`; squash on merge.

## Things to NOT change without coordination

- The `IBatteryDataSource` interface.
- The fields on `BatteryReading`.
- The 3 indicator cylinders' V/I/T mapping (user spec).
- The "no System Log panel" decision (user spec).

## Open questions / TODO

- [ ] Lock in the exact Firebase path / JSON schema with ESP32 firmware author.
- [ ] Decide REST polling vs. real-time listener for the Firebase data source.
- [ ] Add a "Cycles Left" widget to the dashboard (next iteration).
- [ ] URP Render Pipeline Asset needs to be assigned in Project Settings → Graphics on first clone (until we ship a pre-assigned asset).
