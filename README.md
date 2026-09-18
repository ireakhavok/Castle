<p align="center">
  <img src="main_banner.png" alt="Project Banner" width="100%">
</p>

# RealmFoundry (repository: Castle)

SiegeEngine is the runtime. Castle is the self-hosting IDE built on that same runtime. The editor is not a second product sitting beside the engine.

## Documentation

| Doc | What it covers |
|---|---|
| [Architecture](docs/ARCHITECTURE.md) | Layers, self-hosting, frame loop, examples repo |
| [Rendering](docs/RENDERING.md) | `IRenderContext`, constant buffers, OpenGL-only backend |
| [Scenes and projects](docs/SCENES_AND_PROJECTS.md) | `SceneRegistry`, `ScriptLoader`, hosted preview, Play / Export |
| [IDE](docs/IDE.md) | Blades, docking, CastleBuilder / Keystone / ToolChest |
| [Boundaries](docs/BOUNDARIES.md) | Core vs IDE vs Server vs project scripts |

```mermaid
%%{init: {'theme':'dark'}}%%
flowchart TB
  subgraph Projects["Project layer — Siege_Engine_Example_Projects"]
    PJ["project.json + Scripts + Assets"]
  end
  subgraph IDE["IDE — Castle"]
    CB[CastleBuilder]
    TC[ToolChest]
    MR[MapRoom]
    RC[ReadingChamber]
    KS[Keystone]
  end
  subgraph Core["Core — SiegeEngine"]
    GPU["GPU / IRenderContext / CBs"]
    EB[EventBus]
    ENT[Entity + Components]
    SYS[GameSystems]
    SCN[Scenes + SceneRegistry]
    UI[HTML/CSS/JS UI]
  end
  subgraph Server["Server — Citadel"]
    GS[GameServer]
    VAL[ServerValidationSystem]
    NET[NetworkManager / Steam]
  end
  subgraph Boot["Bootstrap — Trebuchet + Foundation"]
    L[Launcher]
  end
  L --> CB
  L --> GS
  CB --> SCN
  CB --> UI
  PJ -->|"ScriptLoader + public contracts only"| SCN
  SCN --> GPU
  SCN --> EB
  GS --> EB
  SYS --> GS
```

## Project Overview

RealmFoundry is an ambitious, open-source C#-based game engine and Integrated Development Environment (IDE) hybrid, designed to democratize the creation and playing of custom 3D video games, with a strong emphasis on real-time multiplayer experiences supporting hundreds of players. Drawing inspiration from tools like Unity, Godot, RPG Maker, and multiplayer engines, it transforms game development into a rewarding "meta-game" where creators—solo hobbyists, small teams, or larger communities—can build, mod, share, and collaborate on content without facing steep learning curves, mod conflicts, or inadequate multiplayer support. The engine prioritizes intuitive workflows, seamless previews, and instant testing, making the act of creation as engaging as gameplay itself. At its core, RealmFoundry functions as a professional-grade game creator tool: modular, extensible, and secure, rather than a prototype lacking robust editor features.

### Main Goals

The primary objectives of RealmFoundry are multifaceted, focusing on accessibility, collaboration, and scalability:

* **Empower Creators**: Enable non-professional developers to craft immersive MMORPG-style games with custom events (e.g., dialogues, shops, quests) using drag-and-drop interfaces, visual scripting, and asset importers. The IDE leverages the engine's own systems (e.g., rendering pipeline, event bus) for self-modifiability, allowing users to customize the tool itself via mods.
* **Seamless Multiplayer Integration**: Support dedicated servers, P2P, and solo modes out-of-the-box, with built-in anti-cheat mechanisms to ensure fair play. This includes real-time synchronization for hundreds of players, optimized bandwidth via entity deltas, and future expansions to multi-user IDE sessions where collaborators can edit shared projects as if in a multiplayer game.
* **Modding and Sharing Ecosystem**: Facilitate easy mod creation and distribution through JSON configs, Steam Workshop integration, and modular DLLs. Mods can extend UI, add blueprints (e.g., 2D/3D starters like FPS or isometric views), or introduce new mechanics without conflicts, fostering community-driven content.
* **Cross-Purpose Foundation**: Build a unified codebase where the engine powers both runtime gameplay and IDE tools, ensuring consistency and reusability. This allows for popping out panels into independent windows, dynamic loading of modules, and using game systems (e.g., physics, lighting) within editor previews.
* **Performance and Scalability**: Optimize for large-scale worlds with spatial grids, frustum culling, and occlusion checks, while maintaining cross-platform potential (Windows primary, with abstractions for Mac/Linux via OpenGL/Vulkan).
* **Community and Governance**: Incorporate features like server rulesets (ThroneRoom), social lobbies (GuildTower), and governance tools to manage collaborative projects, promoting inclusive development.

By achieving these goals, RealmFoundry aims to bridge the gap between simple game makers and full engines, creating a "Citadel" of creativity where users build virtual realms collaboratively and securely.

## Key Concepts

### Security

Security is a foundational pillar in RealmFoundry, especially given its multiplayer focus and moddable nature. The engine employs an authoritative server model (via GameServer.cs) to prevent cheating:

* **Validation Mechanisms**: All client actions (e.g., movement, inventory changes, combat) are validated server-side using ServerValidationSystem.cs. This includes speed/distance checks (e.g., maxSpeed=20f, maxDistance=20f), frustum-based visibility to limit data exposure, and occlusion checks to simulate realistic line-of-sight, reducing exploits like wall-hacks.
* **Protected Events**: EventBus.cs uses \[ProtectedEventAttribute] to restrict publishing of sensitive events to internal callers (e.g., Citadel namespace), preventing modders or clients from injecting unauthorized actions.
* **Networked Event Sync**: Only non-protected events are networked via SteamEngine, with serialization/deserialization ensuring data integrity. Input events (MouseInputEvent, KeyInputEvent) are validated before publishing.
* **Mod Security**: ModManager.cs whitelists hooks and scans mods for safe loading; future plans include sandboxing DLLs to prevent malicious code. Workshop items are fetched via Steam SDK, leveraging Valve's moderation.
* **Anti-Cheat Optimizations**: Spatial grids and delta tracking minimize unnecessary data transmission, while raytracing for sounds/physics adds layers of server-side simulation to detect anomalies.

This approach ensures a secure environment for multiplayer games and collaborative editing, minimizing risks in shared IDE sessions.

### Modularity

RealmFoundry's architecture is highly modular to support extensibility and avoid monolithic code:

* **DLL-Based Modules**: Specialized features (e.g., MapRoom.dll for level editing, ScriptChamber.dll for VS Code integration, QuestHall.dll for node-based quests) are loadable DLLs. ModManager.cs can be extended with Assembly.LoadFrom for dynamic resolution, allowing mods to add or override panels.
* **Abstractions and interfaces**: `IGameServer`, `IRenderContext` (OpenGL implemented; DirectX/Vulkan are future backends against the same interface, not present), `IControlContext`, `IScene`, `IHostedContent`. Dockable windows are `BasePanel` with Init/Update/Render/Dispose and a dock state.
* **Event-Driven Design**: EventBus.cs decouples systems via strongly-typed IEvents, supporting networked sync and mod injections (e.g., custom events from mods).
* **UI Extensibility**: MenuSystem.cs parses HTML/CSS for menus/panels, with data-hooks invoking namespace-qualified methods (e.g., "SiegeEngine.AssetParsing.FBXParser.Load"). Mods can override HTML files (e.g., DevMenu.html) for custom layouts.
* **Asset and Project Modularity**: ModManager scans for assets (FBX, textures, Unity prefabs) and projects (as JSON blueprints). Projects load as self-contained modules, rendering in panels using shared engine systems.
* **Panel management**: `PanelManager` + `IDEDockingStrategy` handle docking, resizing, float, and per-context layout save (`layout.Scene Editor.json`). Panels pop out as extra host surfaces, not a second renderer.

This modularity ensures the engine/IDE can evolve through community contributions, with clean separation to prevent circular dependencies.

### Multi-User and Collaboration

While currently client-side focused, the engine is designed for P2P expansion: GameServer can sync IDE states (e.g., entity placements in MapRoom) via events, turning the IDE into a multiplayer "game" for real-time co-editing. ThroneRoom.dll will govern rules/configs for collaborative sessions.

## Technical Structure

### Core engine (SiegeEngine)

* **Events**: `EventBus` pub/sub with optional Steam networking. `[ProtectedEvent]` cannot be published by mods or clients. Subscribe in init, unsubscribe on dispose.
* **Rendering**: `IRenderContext` (OpenGL backend only). World cameras and lighting upload **constant buffers** (`FrameCB`, `ObjectCB`, `LightCB`, `ShadowCB`, `PostCB`) via `SetConstants`. `ShaderProgram.SetMatrix4("uView")` remains as a fallback that patches the cached CB. Terrain editor GLSL is still named uniforms — that is an allowed floor. Settings may list DirectX 11/12; those backends are not implemented.
* **Asset parsing**: FBX pipeline for meshes, skeletons, animations, materials, textures; `ModelManager` + `TextureLoader`.
* **Entities**: `Entity` + components, parenting, delta tracking. `Level` is the in-memory source of truth.
* **Systems**: `GameSystem` base — physics, audio (with GPU occlusion), animation, lighting pack, client prediction, project-registered systems.
* **Managers**: `ModManager`, `ScriptLoader`, `SceneManager`, `PanelManager`, `ModelManager`, `UISettingsManager`, `WorkshopManager`.
* **UI**: production HTML/CSS/JS with `data-hook` attributes. Same stack for main menu, IDE chrome, and game HUD.

### Server (Citadel)

* `GameServer` — entities, systems, spatial grid, occlusion, ray traces.
* `ServerValidationSystem` — movement / inventory / combat checks.
* `EntityDeltaTracker` + `NetworkManager` — Steam P2P or dedicated.

### Bootstrap (Trebuchet / Foundation / GameHost)

* `Trebuchet.Launcher` — Steam, window, local Citadel, IDE loop.
* `Foundation` — isolated Play / Export process. Same `SceneContext` activation as in-process Play.
* `GameHost` — additional host entry.

### Scenes

* `Scene` (abstract) → `RuntimeGameplayScene`, `GameScene`, `ModelViewerScene`, `TerrainScene`, `EditorScene`.
* Project assemblies add `[CustomSceneEntry]` types (`ChessScene`, `CheckersScene`, …) discovered by `ScriptLoader`.
* `SandboxScene` in older diagrams is historical. It is not the primary path.

### IDE (CastleBuilder + satellites)

* `CastleBuilder` — `EditorScene`, project load/save, Scene Editor, Script Editor, Play Host.
* `Keystone` — `ProjectSettings`, layouts, outliner, undo.
* `MapRoom` — terrain / 2D creators.
* `ReadingChamber` — file picker, animation viewer.
* `ToolChest` — browser, properties, tree, brushes, timeline, blend, console, lights, skybox, post-process.

Docking (`IDEDockingStrategy`) and `BasePanel` are implemented. They are not stubs.

### UI elements

HTML/CSS/JS parsed into `HtmlElement` trees. Clicks with `data-hook` resolve to fully qualified methods and/or EventBus events.

## Development status and roadmap

* **Floors** (do not reopen): point shadows, non-gray scene, model-viewer mesh + Play deformation, original viewer background, blend panel one node per add, OpenGL world path through `SetConstants`, chess/checkers board camera via `GetViewProjection` + CBs, volumetric fog fragment defines `CascadeVPAt`.
* **Open**: leftover named-uniform writes on some engine paths (fallback still patches CBs); first-class 2D/iso camera mode so a project does not have to subclass `Scene`; DirectX backend against the existing `IRenderContext` — not started.
* **Dependencies**: Silk.NET OpenGL, Steamworks. Windows is the production target.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the live map.
## Folder structure

Git root is `Castle/`. The solution is `Castle/Castle.sln`. Example games live in the separate `Siege_Engine_Example_Projects` repo.

```text
Castle/
  README.md
  docs/
  Prompt.txt
  BlenderScenes/
  Libraries/                     Steam redistributables
  Castle/
    Castle.sln
    Assets/                      shipped engine/IDE assets
    SiegeEngine/
      Core/                      GPU, Events, Managers, UI, AssetParsing, …
      Scenes/                    Scene, SceneRegistry, RuntimeGameplay, ModelViewer, Terrain
      Systems/                   GameSystem, Audio, Animation, Lighting, Prediction
      PlayerSystem/              Player, PlayerMovement, FlyCamera, AngledOrthoCamera
    IDE/
      CastleBuilder/             EditorScene, BlueprintManager, Scene Editor
      Keystone/                  ProjectSettings, layouts, outliner
      MapRoom/                   Terrain + 2D creators
      ReadingChamber/            file picker, animation viewer
      ToolChest/                 browser, properties, brushes, timeline, console
    Launcher/
      Trebuchet/                 window + Steam + loop
      Foundation/                isolated Play / Export
      GameHost/
    Server/Citadel/              authoritative GameServer
```

```mermaid
%%{init: {'theme':'dark'}}%%
flowchart TB
  ROOT[Castle git root]
  ROOT --> DOCS[docs]
  ROOT --> SLN[Castle/Castle.sln]
  SLN --> SE[SiegeEngine]
  SLN --> IDE[IDE]
  SLN --> LAU[Launcher]
  SLN --> CIT[Server/Citadel]
  SE --> CORE[Core/GPU Events Managers UI]
  SE --> SCN[Scenes]
  SE --> SYS[Systems]
  SE --> PLY[PlayerSystem]
  IDE --> CB[CastleBuilder]
  IDE --> KS[Keystone]
  IDE --> MR[MapRoom]
  IDE --> RC[ReadingChamber]
  IDE --> TC[ToolChest]
  LAU --> TR[Trebuchet]
  LAU --> FD[Foundation]
  CIT --> GS[GameServer]
```

`MapRoom`, `ReadingChamber`, and `ToolChest` are real assemblies, not stubs. `QuestHall` / `ThroneRoom` / `GuildTower` / `ScriptChamber` names in older diagrams are product ideas, not current projects.

## Core class diagram

`SandboxScene` below is historical. Current gameplay types are `RuntimeGameplayScene`, `EditorScene`, and project `[CustomSceneEntry]` scenes. Interfaces are still accurate.


```mermaid
classDiagram
    class IGameServer {
        <<interface>>
        +AddEntity(Entity entity)
        +RemoveEntity(int id)
        +GetEntities() IReadOnlyList~Entity~
        +Update(float deltaTime)
        +ValidateAndUpdateMovement(int entityId, Vector2 pos, Quaternion rot, ulong steamId) bool
        +Publish~T~(T eventData, bool networkSync)
        +RequestRayTrace - Vector3 start, Vector3 dir, float maxDist -  RayTraceResult
    }

    class GameServer {
        -List~Entity~ _entities
        -List~GameSystem~ _systems
        -EventBus _eventBus
        -NetworkManager _networkManager
        -EntityDeltaTracker _deltaTracker
        -Dictionary~(int,int), List~Entity~~ _spatialGrid
        +GameServer(EventBus eventBus, NetworkManager networkManager)
        +AddEntity(Entity entity)
        +RemoveEntity(int id)
        +Update(float deltaTime)
        +ValidateAndUpdateMovement(..) bool
        +Publish~T~(T eventData, bool networkSync)
        +RequestRayTrace(..) RayTraceResult
    }
    GameServer ..|> IGameServer
    GameServer --> Entity : manages
    GameServer --> GameSystem : adds
    GameServer --> EventBus : subscribes/publishes
    GameServer --> ServerValidationSystem : uses

    class GameSystem {
        <<abstract>>
        +GameSystem(IGameServer server)
        +Update(float deltaTime)
    }
    GameSystem <|-- ServerValidationSystem
    GameSystem <|-- LightingSystem
    GameSystem <|-- AudioSystem
    GameSystem <|-- MenuSystem
    GameSystem <|-- PhysicsSystem

    class EventBus {
        -Dictionary~Type, List~object~~ _subscribers
        -SteamEngine _steamEngine
        +EventBus(SteamEngine steamEngine)
        +Subscribe~T~(Action~T~ handler)
        +Publish~T~(T eventData, bool networkSync)
        +ProcessNetworkMessage(byte[] data)
    }
    EventBus --> IEvent : publishes
    EventBus --> SteamEngine : networks

    class IEvent {
        <<interface>>
        +string Type
        +byte[] Serialize()
        +void Deserialize(byte[] data)
    }
    IEvent <|-- MouseInputEvent
    IEvent <|-- KeyInputEvent
    IEvent <|-- EntityPlacedEvent

    class ModManager {
        -string _modsDirectory
        -List~ModInfo~ _loadedMods
        +ModManager(string modsDirectory, ISteamEngine steamEngine)
        +LoadModels(ModelManager loader)
        +ResolvePath(string relativePath) string
        +GetMenuConfigPath() string
    }
    ModManager --> UnityAssetScanner : uses for prefabs

    class Scene {
        <<abstract>>
        -IRenderContext _renderContext
        -IControlContext _controlContext
        -IGameServer _server
        -EventBus _eventBus
        +Scene(..)
        +Initialize(int width, int height)
        +Update(float deltaTime)
        +Render(IReadOnlyList~Entity~ entities)
        +Dispose()
    }
    Scene <|-- SandboxScene

    class SandboxScene {
        -Player _player
        -ShaderProgram _modelShader
        -ShaderProgram _gridShader
        +SandboxScene(..)
        +Initialize(int width, int height)
        +Update(float deltaTime)
        +Render(IReadOnlyList~Entity~ entities)
        +Dispose()
    }
    SandboxScene --> ModelManager : uses for models
    SandboxScene --> LightingSystem : adds

```
## Startup sequence

Current bootstrap still starts Steam → EventBus → mods → window → UI, but the first authored surface is the **IDE docking tree** (or Foundation Play), not only `SandboxScene`. See [ARCHITECTURE](docs/ARCHITECTURE.md#frame-loop).

```mermaid
%%{init: {'theme':'dark'}}%%
sequenceDiagram
    participant User
    participant Launcher
    participant SteamEngine
    participant EventBus
    participant ModManager
    participant ContextManager
    participant MenuSystem
    participant SandboxScene
    participant GameServer

    User->>Launcher: Start("OpenGL")
    Launcher->>SteamEngine: Initialize()
    Launcher->>EventBus: new EventBus(SteamEngine)
    Launcher->>ModManager: new ModManager(..)
    ModManager->>ModManager: LoadLocalMods() / LoadWorkshopMods()
    Launcher->>ContextManager: Initialize(width, height, title)
    Launcher->>MenuSystem: new MenuSystem(..)
    MenuSystem->>MenuSystem: Initialize() / LoadHtml("MainMenu.html")
    loop Main Loop
        Launcher->>SteamEngine: RunCallbacks()
        Launcher->>ContextManager: PollEvents()
        Launcher->>MenuSystem: Update(deltaTime)
        Launcher->>MenuSystem: Render()
        Launcher->>ContextManager: SwapBuffers()
    end
    Note over MenuSystem: User clicks "Test Sandbox" (data-hook)
    MenuSystem->>EventBus: Publish(LaunchSandboxEvent)
    EventBus->>Launcher: Handler instantiates SandboxScene
    Launcher->>SandboxScene: Initialize()
    SandboxScene->>GameServer: GetEntities() / Interactions
    loop Sandbox Loop (if transitioned)
        SandboxScene->>SandboxScene: Update(deltaTime)
        SandboxScene->>SandboxScene: Render(entities)
    end
```
## Component diagram for modularity

Still a valid EventBus / GameServer / Launcher picture. IDE chrome is `PanelManager` + `IDEDockingStrategy`, not MenuSystem alone. Specialized modules are real projects under `Castle/IDE/`.

```mermaid
%%{init: {'theme':'dark'}}%%
graph LR
    subgraph CoreEngineSiegeEngine
        EB[EventBus] --> IE[IEvent]
        MM[ModManager] --> UAS[UnityAssetScanner]
        MS[MenuSystem] --> HP[HtmlParser]
        MS --> CP[CssParser]
        SS[SandboxScene] --> SP[ShaderProgram]
        SS --> VB[VertexBuffer]
        LS[LightingSystem] --> SS
    end
    subgraph ServerCitadel
        GS[GameServer] --> EB
        GS --> SVS[ServerValidationSystem]
        GS --> EDT[EntityDeltaTracker]
        NM[NetworkManager] --> SE[SteamEngine]
    end
    subgraph LauncherTrebuchet
        L[Launcher] --> SE
        L --> EB
        L --> MM
        L --> CM[ContextManager]
        L --> MS
    end
    subgraph StubsDLLs
        MR[MapRoomStub]
        QH[QuestHallStub]
        SC[ScriptChamberStub]
    end
    L -. "Launches on Event" .-> SS
    MS -. "Publishes Hooks" .-> EB
    MM -. "Scans Loads" .-> AM[AssetsMods]
    GS -. "Networks Events" .-> NM
```
## Rendering system class diagram

Add `SetConstants<T>(ConstantSlot, T)` and `TryGetConstants<T>` to the mental model of `IRenderContext`. Named `SetMatrix4` still exists as a CB fallback. Full contract: [RENDERING](docs/RENDERING.md).

```mermaid
%%{init: {'theme':'dark'}}%%
classDiagram
    class IRenderContext {
        <<interface>>
        +Clear(Enum bufferBits)
        +Viewport(int x, int y, uint width, uint height)
        +BindTexture(Enum target, uint texture)
        +DrawElements(Enum mode, int count, Enum type, void* indices)
        +GetError() Enum
        +ActiveTexture(Enum texture)
    }

    class ShaderProgram {
        +ShaderProgram(IRenderContext context, string vertexSrc, string fragmentSrc)
        +Use()
        +SetMatrix4(string name, Matrix4x4 value)
        +SetUniform(string name, float x, float y, float z, float w)
        +Dispose()
    }

    class VertexBuffer {
        <<IDisposable>>
        +VertexBuffer(IRenderContext context)
        +Update(List~Entity~ entities)
        +Bind()
        +Dispose()
    }

    class TextRenderer {
        +TextRenderer(IRenderContext context, IntPtr window)
        +Initialize(ShaderProgram shader)
        +RenderText(string text, float x, float y, float vw, float vh, float fs, Vector4 color, string font)
        +Dispose()
    }

    class UIQuadRenderer {
        +UIQuadRenderer(IRenderContext context)
        +Initialize()
        +DrawQuad(float posX, float posY, float sizeX, float sizeY, Vector4 color, float vw, float vh)
    }

    class SandboxScene {
        -IRenderContext _renderContext
        -ShaderProgram _modelShader
        -ShaderProgram _gridShader
        +Initialize(int width, int height)
        +Render(IReadOnlyList~Entity~ entities)
        +Dispose()
    }

    SandboxScene --> IRenderContext : uses
    SandboxScene --> ShaderProgram : creates/uses
    SandboxScene --> VertexBuffer : binds/draws
    SandboxScene --> TextRenderer : optional for overlays
    SandboxScene --> UIQuadRenderer : optional for UI
```
## UI/Menu System Flow Diagram
```mermaid
%%{init: {'theme':'dark'}}%%
sequenceDiagram
    participant Launcher
    participant MenuSystem
    participant HtmlParser
    participant CssParser
    participant EventBus
    participant UserInput

    Launcher->>MenuSystem: Initialize() / SwitchMenu("MainMenu")
    MenuSystem->>HtmlParser: Parse(html)
    HtmlParser-->>MenuSystem: HtmlElement tree
    MenuSystem->>CssParser: Apply(cssBlocks) / ApplyAll(tree)
    CssParser-->>MenuSystem: Styled elements
    MenuSystem->>MenuSystem: ComputeLayout(vw, vh)
    MenuSystem->>MenuSystem: CollectClickables()

    loop Update Loop
        UserInput->>MenuSystem: Mouse position / clicks (via IControlContext)
        MenuSystem->>MenuSystem: Check hovers/clicks on _clickables
        alt Click on Element
            MenuSystem->>MenuSystem: HandleClickableClick(elem)
            alt data-hook present
                MenuSystem->>EventBus: Publish(GenericEvent or SwitchSceneEvent)
            end
        end
    end

    MenuSystem->>TextRenderer: RenderText(..)
    MenuSystem->>UIQuadRenderer: DrawQuad(..)
```
## Event System Class Diagram
```mermaid
%%{init: {'theme':'dark'}}%%
classDiagram
    class IEvent {
        <<interface>>
        +string Type
        +byte[] Serialize()
        +void Deserialize(byte[] data)
    }

    class EventBus {
        -Dictionary~Type, List~object~~ _subscribers
        -SteamEngine _steamEngine
        +Subscribe~T~(Action~T~ handler)
        +Publish~T~(T eventData, bool networkSync)
        +ProcessNetworkMessage(byte[] data)
    }

    class MouseInputEvent {
        +Vector2 Position
        +MouseButton Button
        +InputAction Action
        +ulong SteamId
        +Serialize() byte[]
        +Deserialize(byte[] data)
    }

    class KeyInputEvent {
        +Key Key
        +InputAction Action
        +ulong SteamId
        +Serialize() byte[]
        +Deserialize(byte[] data)
    }

    class EntityMovedEvent {
        +int EntityId
        +Vector3 Position
        +Serialize() byte[]
        +Deserialize(byte[] data)
    }

    IEvent <|-- MouseInputEvent
    IEvent <|-- KeyInputEvent
    IEvent <|-- EntityMovedEvent

    EventBus --> IEvent : publishes/processes
    EventBus --> SteamEngine : sends via P2P
    GameServer --> EventBus : subscribes/publishes (e.g., OnEntityPlaced)
    MenuSystem --> EventBus : publishes on clicks/hooks
    SandboxScene --> EventBus : implicit via systems
```
## Server Validation System Flow Diagram
```mermaid
%%{init: {'theme':'dark'}}%%
flowchart TD
    A[Client Sends Action - e.g., Movement/Input] --> B[GameServer Receives via NetworkManager/EventBus]
    B --> C[QueueNetworkEvent - IEvent]
    C --> D[Update - deltaTime: Dequeue and Publish]
    D --> E[ServerValidationSystem.Validate* - e.g., Movement/Inventory/Input]
    E -->|Valid| F[Update Entity/State, Publish Event - networkSync=true]
    E -->|Invalid| G[Log Rejection, Discard]
    F --> H[DeltaTracker.Update, Serialize Visible Deltas]
    H --> I[SendToAll via NetworkManager]
    subgraph GameServer
    B
    C
    D
    H
    I
    end
    subgraph ServerValidationSystem
    E
    end
```
## Audio system

Implemented: `AudioSystem` with a dedicated worker, bank load, AutoPlay entities, and GPU occlusion infrastructure. Ray-trace filtering is still the model; treat quality as evolving, not missing.

```mermaid
%%{init: {'theme':'dark'}}%%
flowchart TD
    A[SoundEmissionEvent] --> B[AudioSystem.OnSoundEmission]
    B --> C[ValidateSoundSource - via ISoundValidator]
    C -->|Valid| D[RayTraceSound - RequestRayTrace from GameServer]
    D --> E[RandomScatterDirection / Simulate Rays]
    E --> F[PlaySound with Filter - e.g., LowPass for Occlusion]
    C -->|Invalid| G[Discard/Log]
    subgraph AudioSystem
    B
    C
    D
    E
    F
    end
    subgraph GameServer
    D --> H[RequestRayTrace - AABB Intersect]
    end
```
## Entity Management subsystem
```mermaid
%%{init: {'theme':'dark'}}%%
sequenceDiagram
    participant Client
    participant GameServer
    participant EventBus
    participant Systems

    Client->>EventBus: Publish(EntityPlacedEvent)
    EventBus->>GameServer: OnEntityPlaced
    GameServer->>GameServer: AddEntity (With Components)
    GameServer->>GameServer: UpdateSpatialGrid
    GameServer->>EventBus: Publish(Event, networkSync=true)
    loop Update
        GameServer->>Systems: Update(deltaTime) e.g., Physics/Validation
        Systems->>GameServer: Validate/Update Entities
        GameServer->>GameServer: Frustum/Occlusion Checks
        GameServer->>GameServer: DeltaTracker.Update
    end
    alt Remove Entity
        Client->>EventBus: Publish(RemoveEvent)
        EventBus->>GameServer: RemoveEntity(id)
        GameServer->>GameServer: RemoveFromSpatialGrid
    end
```
## Lighting subsystem

World path: `LightingFrame.ApplyConstants` → `LightCB` / `ShadowCB`. Terrain still calls `ApplyTo` named uniforms (floor). Fog modes: Off, Exponential, Height (forward), Volumetric (`FogPass`, fragment must define `CascadeVPAt`).

```mermaid
%%{init: {'theme':'dark'}}%%
classDiagram
    class LightingSystem {
        <<GameSystem>>
        -List~LightComponent~ _lights
        +AddLight(LightComponent light)
        +RemoveLight(LightComponent light)
        +Update(float deltaTime)
        +GetShaderUniforms() LightUniformData?
    }

    class LightComponent {
        +LightType Type
        +Vector3 Color
        +float Intensity
        +Vector3 Direction/Position
    }

    LightingSystem --> LightComponent : manages
    SandboxScene --> LightingSystem : adds/removes lights
    ShaderProgram --> LightingSystem : gets uniforms (uLightDir/Color/Intensity)
```
```mermaid
%%{init: {'theme':'dark'}}%%
flowchart TD
    A[SandboxScene Init] --> B[Create LightingSystem]
    B --> C[AddLight - Directional Sun]
    D[Update] --> E[LightingSystem.Update - Process Lights]
    F[Render] --> G[GetShaderUniforms]
    G --> H[SetUniforms in ModelShader - Dir/Color/Intensity]
    H --> I[Render Model with Lighting]
```
## Event Subsystem (Class + Flow)
```mermaid
%%{init: {'theme':'dark'}}%%
classDiagram
    EventBus --> IEvent : Publishes/Processes
    EventBus --> SteamEngine : Networks Non-Protected
    IEvent <|-- MouseInputEvent
    IEvent <|-- KeyInputEvent
    IEvent <|-- LobbyCreatedEvent
    GameServer --> EventBus : Subscribes (e.g., OnEntityPlaced)
    MenuSystem --> EventBus : Publishes on Clicks
```
```mermaid
%%{init: {'theme':'dark'}}%%
flowchart TD
    A[Caller Publishes Event<T>] --> B[Check ProtectedAttribute]
    B -->|Protected & Unauthorized| C[Reject/Log]
    B -->|Allowed| D[Invoke Subscribers - Handlers]
    D --> E[If NetworkSync & Not Protected: Serialize]
    E --> F[Send via SteamEngine P2P]
    G[Receive Network Message] --> H[Deserialize to Type]
    H -->|Valid & Not Protected| I[Invoke Subscribers]
    H -->|Invalid/Protected| J[Reject/Log]
```
## Rendering subsystem

Replace "Set View/Projection Matrices" in the flowchart with `SetConstants(FrameCB / ObjectCB)`. `SandboxScene.Render` in the diagram is any `Scene.Render`. OpenGL only.

```mermaid
%%{init: {'theme':'dark'}}%%
classDiagram
    IRenderContext <|-- OpenGLRenderContext
    ShaderProgram --> IRenderContext : uses for compile/use
    VertexBuffer --> IRenderContext : uses for bind/bufferData
    TextRenderer --> ShaderProgram : initializes with
    UIQuadRenderer --> IRenderContext : draws quads
    SandboxScene --> ShaderProgram : _modelShader / _gridShader
    SandboxScene --> VertexBuffer : binds for grid/model
    SandboxScene --> IRenderContext : clear/viewport/draw
```
```mermaid
%%{init: {'theme':'dark'}}%%
flowchart TD
    A[SandboxScene.Render] --> B[Clear Buffers]
    B --> C[Set View/Projection Matrices]
    C --> D[Render Grid - Lines, No Depth]
    D --> E[Enable Depth Test]
    E --> F[Use Model Shader, Set Uniforms - Light/ViewPos]
    F --> G[For Each Mesh: Bind Textures - Albedo/Normal/Metallic]
    G --> H[Bind VAO, DrawElements - Triangles]
    H --> I[Debug Passes - Material/Texture-Only]
    I --> J[Log Errors, End Render]
```