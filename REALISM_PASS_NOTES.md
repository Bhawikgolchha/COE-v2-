# BatteryTwin — Realism Pass: verification checklist

This is what was changed during the realism pass and what to do when you re-open Unity. Everything is already on disk — Unity just needs to reload and recompile.

---

## 1) Reload the project

1. Focus the Unity Editor window.
2. Wait for the **automatic asset reimport / script compile**. The bottom-right corner shows a spinner while it works.
3. Open the **Console** (`Ctrl+Shift+C`). Click `Clear`.
4. Confirm no red errors. Warnings about `Input Manager deprecation` are fine — ignore.

If the Console shows any red errors about `BatteryTwin.Bindings.*`, see *Section 5: troubleshooting*.

---

## 2) What changed on disk (read-only verification, don't re-edit)

Three binding scripts now lazy-initialize their `MaterialPropertyBlock` inside `Apply()` so the mock data source's first emission cannot fire before `Awake`:

- `Assets/Scripts/BatteryTwin/Bindings/TemperatureColorBinding.cs` — `Apply()` now checks `if (_mpb == null) { _mpb = new MaterialPropertyBlock(); _propId = Shader.PropertyToID(colorProperty); }` before `target.GetPropertyBlock(_mpb)`.
- `Assets/Scripts/BatteryTwin/Bindings/SocEmissionBinding.cs` — same pattern for `_mpb` + `_propId` (using `emissionProperty`).
- `Assets/Scripts/BatteryTwin/Bindings/IndicatorCellBinding.cs` — same pattern + lazy-init of `colorRamp` if missing.

You can `git diff` these three files to see the exact change. **No other script files were modified.**

---

## 3) What changed in the scene (`Assets/Scenes/BatteryTwin.unity`)

These were saved live via the Unity bridge while it was up. After re-opening the scene, spot-check the items below in the Inspector. If any are missing, the manual steps to restore are listed.

### 3a) Materials (in `Assets/Materials/`)

Open each and confirm in Inspector — shader is **Universal Render Pipeline/Lit** (except `M_MonitorScreen` which is **Unlit**).

| Material | Base Color | Metallic | Smoothness | Emission |
|---|---|---|---|---|
| `M_TableWood` | `#5A3B22` | 0.00 | 0.35 | off |
| `M_MonitorBezel` | `#181818` | 0.00 | 0.25 | off |
| `M_MonitorStand` | `#3A3D42` | 0.60 | 0.55 | off |
| `M_BMS` | `#0E4A1F` | 0.05 | 0.30 | off |
| `M_PouchCell` | `#C8CCD0` | 0.85 | 0.55 | **on** (idle color = black; SocEmissionBinding drives this) |
| `M_Cell18650_Green` | `(0.30, 0.90, 0.40)` | 0.00 | 0.50 | on, `(0.04, 0.20, 0.08)` |
| `M_Cell18650_Red` | `(0.95, 0.30, 0.25)` | 0.00 | 0.50 | on, `(0.20, 0.04, 0.04)` |
| `M_Cell18650_Orange` | `(0.95, 0.55, 0.20)` | 0.00 | 0.50 | on, `(0.20, 0.10, 0.04)` |

To fix manually if any value is off: select the material in the Project window → Inspector → adjust **Base Map** color, **Metallic**, **Smoothness**, and toggle **Emission**.

### 3b) `Devices/PouchCell`

In Hierarchy, select `Devices/PouchCell`. Inspector should read:

- Transform `Position = (0.73, 0.86, -0.01)`, `Scale = (1.5, 1.5, 1.5)`.
- `TemperatureColorBinding` component: `Cool Color` ≈ `(0.92, 0.93, 0.95)`, `Hot Color` ≈ `(1.00, 0.55, 0.20)`. **No blue, no red — this is the fix for the pink-pouch bug.**
- `SocEmissionBinding`: untouched defaults.

Also expand `Devices/PouchCell` and select each of `Body1`, `Body6`, `Body7` — every Mesh Renderer's material slot should be `M_PouchCell` (NOT the FBX-embedded `Steel - Satin`).

**Manual restore:** drag `Assets/Materials/M_PouchCell.mat` into the renderer's material slot for any body still showing `Steel - Satin`.

### 3c) Floating panels

| GameObject | Position | Bg Scale | TMP fonts |
|---|---|---|---|
| `FloatingPanels/Panel_CellParameters` | `(0.50, 1.35, -0.10)` | `(0.52, 0.44, 1.0)` | Title 35, Lbl/Val 29 |
| `FloatingPanels/Panel_Connection` | `(1.05, 1.76, -0.05)` | `(0.39, 0.13, 1.0)` | Title 29, Value 38 |
| `FloatingPanels/Panel_DataSource` | `(1.05, 1.59, -0.05)` | `(0.39, 0.13, 1.0)` | Title 29, Value 38 |
| `FloatingPanels/Panel_UpdateRate` | `(1.05, 1.42, -0.05)` | `(0.39, 0.13, 1.0)` | Title 29, Value 38 |
| `FloatingPanels/Panel_Time` | `(1.05, 1.25, -0.05)` | `(0.39, 0.16, 1.0)` | Title 29, TimeValue 48 |

### 3d) Gauges (`Dashboard/DashboardCamera/DashboardCanvas/Gauge_SOC|SOH|Temp`)

Each gauge GameObject must have **three children** (`Bg`, `Fill`, `Title`) and **one `GaugeBinding` component on the root**.

- `Bg` (Image): `Source Image = UISprite`, `Image Type = Simple`, color `(0.180, 0.180, 0.220, 1)`.
- `Fill` (Image): `Source Image = UISprite`, `Image Type = Filled`, `Fill Method = Vertical`, `Fill Origin = Bottom`, `Fill Amount = 0`.
- `GaugeBinding` (on the gauge root):
  - `Gauge_SOC`: `Field = SOC`, `Min = 0`, `Max = 100`, `Drive Color = ✓`, gradient red→yellow→green.
  - `Gauge_SOH`: `Field = SOH`, `Min = 0`, `Max = 100`, `Drive Color = ✓`, gradient red→yellow→green.
  - `Gauge_Temp`: `Field = Temperature`, `Min = 20`, `Max = 55`, `Drive Color = ✓`, gradient blue→yellow→red.
  - `Fill Image` slot must reference the gauge's own `Fill` child.

**Manual restore (per gauge), if `GaugeBinding` is missing:**
1. Select the gauge GameObject (e.g. `Gauge_SOC`).
2. Inspector → `Add Component` → `Gauge Binding`.
3. Drag the gauge's `Fill` child into the **Fill Image** slot.
4. Set `Field` and `Min`/`Max` per the table above.
5. Click `Fill Gradient` → set 3 color stops as described.

**Source Image fix (if any gauge child has `Source Image = None`):**
1. Select the Image component.
2. Click the small circle next to **Source Image** → in the picker, type `UISprite` → pick the built-in `UISprite`.

### 3e) `_TwinController.gaugeBindings` list

Select `_TwinController` in Hierarchy. Inspector → `Gauge Bindings` list must have **3 entries** referencing `Gauge_SOC`, `Gauge_SOH`, `Gauge_Temp`. If empty, drag the three gauge GameObjects into the list (or click the `+` button and pick them).

### 3f) Main Camera

Select `Main Camera`. Inspector should read:

- Transform `Position = (0.25, 1.45, -1.55)`, `Rotation = (17, 0, 0)`.
- Camera `Field of View = 50`.

---

## 4) Press Play and verify

1. Hit **Play** in Unity.
2. **Console:** should be clean. Specifically **no** `ArgumentNullException: Value cannot be null. Parameter name: dest`. The lazy-init script fix prevents it.
3. **Main game view:** should show
   - Walnut desk in foreground.
   - Monitor on the left with "BATTERY MONITORING SYSTEM" header and readable parameter rows.
   - Three indicator cylinders (red = current, orange = temperature, green = voltage).
   - Silvery pouch cell on the right (no pink). A faint warm tint at higher mock temperatures is intended.
   - Floating `CELL PARAMETERS` panel above the desk with readable values: Cell ID, Voltage, Temperature, SOC, SOH, Current, Power, Status.
   - Right-side stack: `DATA SOURCE = Mock`, `UPDATE RATE = 1.0 s`, `TIME = (current time)`.
4. **Gauges on the monitor:** start as empty rounded dark backplates labeled SOC / SOH / Temp. They fill vertically from the bottom as the mock data source ramps SOC/SOH/Temperature.
5. **Stop Play.** Scene should be unchanged; no save-prompt needed.

---

## 5) Troubleshooting

**Compilation errors on reload.** If the Console shows red errors in any of the three binding scripts after Unity reloads, the lazy-init patch didn't merge cleanly. Open the file and confirm `Apply()` starts with:

```csharp
public void Apply(BatteryReading reading)
{
    if (target == null) return;
    if (_mpb == null)
    {
        _mpb = new MaterialPropertyBlock();
        _propId = Shader.PropertyToID(/* colorProperty or emissionProperty */);
    }
    // ... rest of the method
```

If it doesn't, restore from the git index (if you have one) or paste the block in by hand right after the existing `target == null` early-return.

**Gauges still show as solid red rects.** Means `Source Image` on `Bg` or `Fill` is still `None`. Fix via step 3d above (assign `UISprite`).

**Monitor screen renders black.** The `DashboardCamera` is not rendering. Check:
1. `Dashboard/DashboardCamera` is active.
2. Its `Output Texture` slot points to `Assets/RenderTextures/RT_Dashboard.renderTexture` (or whatever the project's dashboard RT is — there should be only one in `Assets/RenderTextures/`).
3. `Assets/Materials/M_MonitorScreen.mat` → `Base Map` = the same `RT_Dashboard`.

**Pouch turns pink again.** `TemperatureColorBinding` on `Devices/PouchCell` has had its cool/hot colors reverted. Reset to `Cool = (0.92, 0.93, 0.95)`, `Hot = (1.00, 0.55, 0.20)` in the Inspector.

**Floating panel text overlaps.** Re-check Section 3c — Y positions and Bg scales were re-stacked.

---

## 6) Known follow-ups (not done yet)

- **Lighting + URP Volume pass:** scene currently relies on a single Directional Light. Adding fill light, soft shadows, SSAO, bloom, tonemapping in `Global Volume` is the next planned slice.
- **Mock data source values appear pinned at start.** `Assets/Scripts/BatteryTwin/Data/MockBatteryDataSource.cs` may need a sweep tweak so SOC/SOH actually ramp visibly during a session. Not investigated yet.
- **Environment / backdrop:** scene floats in the URP default skybox. A studio backdrop or room walls would help with reflections and grounding.
