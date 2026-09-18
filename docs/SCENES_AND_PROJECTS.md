# Scenes, Play, and project scripts

Core does not know what a project is. It knows `Scene`, `SceneContext`, `SceneData`, and `Level`. The IDE serializes a project folder into those types. Play and the Scene Editor consume the same objects.

```mermaid
%%{init: {'theme':'dark'}}%%
flowchart TB
  Disk["project.json + Scripts + Assets"]
  Disk --> BM[BlueprintManager / ProjectSettings]
  BM --> SD[SceneData + Level]
  SD --> CTX[SceneContext]
  CTX --> SR[SceneRegistry.ResolvePreferredSceneName]
  SR --> Create[SceneRegistry.Create]
  Create --> Hosted[EditorScene hosted preview]
  Create --> Play[Play / Foundation]
```

---

## Scene types

```mermaid
%%{init: {'theme':'dark'}}%%
classDiagram
  class Scene {
    +Initialize(w, h)
    +Update(dt)
    +Render(entities)
    +GetViewProjection()
    +RenderContent()
  }
  Scene <|-- GameScene
  Scene <|-- RuntimeGameplayScene
  Scene <|-- ModelViewerScene
  Scene <|-- TerrainScene
  Scene <|-- EditorScene
  Scene <|-- ChessScene
  Scene <|-- CheckersScene
```

| Type | Role |
|---|---|
| Scene | Frame: camera, lighting pack, world, fog, AA, overlays |
| RuntimeGameplayScene | Play / Export for terrain and entity levels |
| GameScene | Gameplay host used next to a hosted custom scene |
| ModelViewerScene | Asset and animation viewer |
| TerrainScene | Runtime terrain |
| EditorScene | IDE wrapper; hosts a child scene for live preview |
| ChessScene, CheckersScene | Project scenes registered by ScriptLoader |

---

## SceneRegistry

Core registers two names at startup:

- `Sandbox`
- `RuntimeGameplay` maps to `RuntimeGameplayScene`

Project assemblies add more names in `ScriptLoader.ActivateProjectScripts`.

```mermaid
%%{init: {'theme':'dark'}}%%
flowchart TD
  R[ResolvePreferredSceneName]
  R --> A{CustomSceneClass registered?}
  A -->|yes| UseA[use that name]
  A -->|no| B{CustomData name registered?}
  B -->|yes| UseB[use that name]
  B -->|no| C{sceneName registered?}
  C -->|yes| UseC[use sceneName]
  C -->|no| D{exactly one custom factory?}
  D -->|yes| UseD[use that factory]
  D -->|many| Warn[log set CustomSceneClass]
  D -->|none| FB[RuntimeGameplay]
  Warn --> FB
```

`EditorScene` hosts a custom scene when the resolved name is not `RuntimeGameplay`.

---

## ScriptLoader

```mermaid
%%{init: {'theme':'dark'}}%%
sequenceDiagram
  participant IDE as EditorScene / Play
  participant SL as ScriptLoader
  participant CS as project Scripts
  participant REG as SceneRegistry / GameServer

  IDE->>SL: copy SiegeEngine.dll into Scripts/Libs
  SL->>CS: write SiegeScripts.csproj HintPath
  SL->>CS: dotnet build
  SL->>SL: load SiegeScripts.dll
  SL->>SL: reflect attributes
  SL->>REG: Register type.Name for each CustomSceneEntry
  SL->>REG: AddSystem for each RegisterGameSystem
  SL->>REG: swap PlayerMovement if CustomPlayerController
```

| Attribute | Meaning |
|---|---|
| CustomSceneEntry | Scene subclass, registered under `type.Name` |
| RegisterGameSystem | Constructed with SceneContext services, added to the server |
| CustomPlayerController | Replaces PlayerMovement when ControllerTypeName is empty |
| RegisterHostedContent | HUD / content; host supplies chrome |

A `RegisterGameSystem` hook may remap `RuntimeGameplay` so Play constructs the project scene.

---

## Hosted preview and Play

```mermaid
%%{init: {'theme':'dark'}}%%
flowchart LR
  subgraph Editor
    ES[EditorScene]
    H[hosted custom scene]
    ES -->|IsHostedPreview true| H
    H -->|draw| GPU[IRenderContext]
  end
  subgraph PlayPath["Play"]
    P[SceneRegistry.Create]
    G[custom scene or RuntimeGameplay]
    P -->|IsHostedPreview false| G
    G -->|input, AI, draw| GPU
  end
```

A custom scene:

1. Uses `public SceneName(SceneContext context) : base(context)`.
2. Reads `context.IsHostedPreview`. When true it rebuilds meshes and draws; it does not install window callbacks, run AI, or consume clicks.
3. Polls `IControlContext` during Play. The host owns the window.
4. Overrides `GetViewProjection` and writes `FrameCB` / `ObjectCB` via `SetConstants`.
5. Unsubscribes EventBus handlers on dispose.

---

## Play and Export

```mermaid
%%{init: {'theme':'dark'}}%%
flowchart TB
  subgraph InProcess["Play Host"]
    PH[PlayHostPanel] --> CTX1[SceneContext in Trebuchet]
    CTX1 --> ACT1[ActivateProjectScripts]
    ACT1 --> SCN1[SceneRegistry.Create]
  end
  subgraph Isolated["Foundation process"]
    FD[Foundation.Program] --> PAY[SceneData + Level payload]
    PAY --> CTX2[SceneContext]
    CTX2 --> ACT2[ActivateProjectScripts]
    ACT2 --> SCN2[SceneRegistry.Create]
  end
  subgraph Export
    EX[ExportGameContent] --> Isolated
    Isolated --> COPY[copy Assets + scripts]
  end
```

Play Host and Foundation activate the same scripts, controller, and scene. Export is that path plus files.

---

## Project file

```json
{
  "Name": "chess",
  "Type": "2D",
  "CameraType": "AngledOrtho",
  "LastOpenedScene": "ChessMain",
  "LastContext": "Scene Editor",
  "Scenes": {
    "ChessMain": {
      "name": "ChessMain",
      "sceneType": "Gameplay",
      "customSceneClass": "ChessScene",
      "settings": { "cameraMode": "Ortho" },
      "environment": {},
      "entities": []
    }
  }
}
```

`Type`, `CameraType`, and `cameraMode` describe the project. A board game also overrides `GetViewProjection`.

`customSceneClass` is the hosted-scene name.

`entities: []` is normal for a custom scene that draws its own board.

---

## Examples

Repo: `ireakhavok/Siege_Engine_Example_Projects`.

| Project | Scene class | Runtime hook | Camera |
|---|---|---|---|
| chess | ChessScene | ChessRuntimeHook remaps RuntimeGameplay | Ortho board look-at |
| checkers | CheckersScene | CheckersRuntimeHook | Ortho board look-at |
| checkers_v2 | CheckersScene | CheckersRuntimeHook | Ortho board look-at |
| save3 | none | inventory HUD + custom controller | RuntimeGameplay + terrain |
