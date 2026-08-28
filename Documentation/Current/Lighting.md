# Lighting, shadows, and fog

SiegeEngine owns the lighting pipeline. Games place lights and pick quality
presets; the editor and Play Game share the same path.

## Defaults (no setup required)

- If a scene has no enabled directional light, a **3 o'clock sun** is used.
  World space is Z-up, so that sun shines from +X (east) downward along -Z.
- **Shadows are on** at Medium quality. Every `ModelComponent` casts and
  receives shadows unless you turn those flags off.
- **Fog is off** until you set `Environment.FogMode`. Density is already
  `0.01` so switching the mode to Exponential is enough to see it.

You do not have to press Play Game to see lighting. The editor viewport
builds `LightingFrame` and the cascaded shadow atlas on the same path Play
uses.

## Place Light button

Scene Editor toolbar \u2192 **Place Light**.

That drops a directional sun entity at the click point. Select it and the
Properties panel shows Type, Color, Intensity, Direction, Range, cones,
CastShadows, and ShadowMode.

- `Directional` \u2014 scene sun. Direction is the travel vector (from the sun
  toward the ground).
- `Point` / `Spot` \u2014 local lights. Position follows the entity transform.

If you never place a light, the 3 o'clock default sun still lights and
shadows the scene.

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
`shadowQuality`, `shadowDistance`, \u2026). Machine-local overrides in
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
