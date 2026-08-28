# Lighting, shadows, and fog

SiegeEngine owns the lighting pipeline. Games place lights and pick quality
presets; the editor and Play Game share the same path.

## Defaults

- **Play Game / runtime:** if a scene has no enabled directional light, a
  **3 o'clock sun** is used. World space is Z-up, so that sun shines from
  +X (east) downward along -Z.
- **Editor viewport:** no implicit sun. Place a Light entity with
  **Add Light**. Until you do, the editor is ambient-only.
- **Shadows are on** at Medium quality once a shadow-casting directional
  (or the runtime fallback sun) exists. Every `ModelComponent` casts and
  receives shadows unless you turn those flags off.
- **Fog is Volumetric** by default (forward exponential plus the shaft
  pass). Set Fog Mode to Off in the Level properties for a clear day.

## Add Light button

Scene Editor toolbar → **Add Light**.

That drops a directional sun entity at the click point. Select it and the
Properties panel shows Type, Color, Intensity, Direction, Range, cones,
CastShadows, and ShadowMode.

- `Directional` — scene sun. Direction is the travel vector (from the sun
  toward the ground).
- `Point` / `Spot` — local lights. Position follows the entity transform.

If you never place a light, Play Game still uses the 3 o'clock sun.
The editor does not.

## Environment toggles

Select the Level in the hierarchy (or open scene settings). Properties
exposes:

| Field | What it does |
| --- | --- |
| Fog Mode | `Off`, `Exponential`, `Height`, `Volumetric` |
| Fog Quality | `Off` disables fog even if Mode is set. Medium is the default. |
| Fog Density | `0.01` is visible. Higher = thicker. |
| Shadow Quality | `Off`, `Low`, `Medium`, `High`, `Ultra` |
| Shadow Distance | World units covered by the sun cascades. Default `400` so overhead editor / play cameras still see contact shadows. |

These live on `EnvironmentSettings` in the scene payload (`fogMode`,
`shadowQuality`, `shadowDistance`, …). Machine-local overrides in
`UISettingsManager` win when set.

## How lighting is defined

1. `Scene.PrepareLightingFrame` binds authored environment settings.
2. `LightingFrame.Build` packs the first enabled directional light (or the
   3 o'clock default) plus up to 4 point and 2 spot lights.
3. `ShadowMapRenderer` draws casters into a CSM atlas when quality is not Off.
4. `LightingFrame.ApplyTo` uploads sun, local lights, fog, and the atlas to
   `ModelShader`, `AnimationShader`, and the terrain shader.

Per-entity flags: `ModelComponent.CastShadows` / `ReceiveShadows` (both
default true). `LightComponent.CastShadows` and `ShadowMode` (`Off`,
`ShadowMap`, `RayTraced`, `Auto`) control the sun. `Auto` uses a shadow map
today and is reserved for a future RT path.

## Shader notes

FBX / DirectX normal maps store +Y up. The model and animation shaders flip
the green channel and skip the map when the tangent basis is degenerate so
spherical heads no longer show a bright / black seam.
