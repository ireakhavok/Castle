# RealmFoundry Project (Repository: Castle) - README.md

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
* **Abstractions and Interfaces**: Core components use interfaces like IGameServer (for server logic), IRenderContext (for rendering pipelines, currently OpenGL), IControlContext (for inputs), and planned IPanel (for dockable IDE windows with Init/Update/Render/Dispose and DockState enum). This enables swapping backends (e.g., Vulkan) or extending without recompilation.
* **Event-Driven Design**: EventBus.cs decouples systems via strongly-typed IEvents, supporting networked sync and mod injections (e.g., custom events from mods).
* **UI Extensibility**: MenuSystem.cs parses HTML/CSS for menus/panels, with data-hooks invoking namespace-qualified methods (e.g., "SiegeEngine.AssetParsing.FBXParser.Load"). Mods can override HTML files (e.g., DevMenu.html) for custom layouts.
* **Asset and Project Modularity**: ModManager scans for assets (FBX, textures, Unity prefabs) and projects (as JSON blueprints). Projects load as self-contained modules, rendering in panels using shared engine systems.
* **Panel Management**: Future PanelManager.cs will handle docking, resizing, and layout saving, with panels popping out into new windows via additional ContextManager instances for independent rendering loops.

This modularity ensures the engine/IDE can evolve through community contributions, with clean separation to prevent circular dependencies.

### Multi-User and Collaboration

While currently client-side focused, the engine is designed for P2P expansion: GameServer can sync IDE states (e.g., entity placements in MapRoom) via events, turning the IDE into a multiplayer "game" for real-time co-editing. ThroneRoom.dll will govern rules/configs for collaborative sessions.

## Technical Structure

### Core Engine (SiegeEngine)

* **Events**: EventBus.cs manages pub/sub with networking; examples include MouseInputEvent, KeyInputEvent.
* **Rendering**: OpenGL abstractions; ShaderProgram for custom shaders (e.g., model with PBR); VertexBuffer for entity data; TextRenderer/UIQuadRenderer for UI.
* **Asset Parsing**: FBXParser.cs and helpers for models/animations; UnityAssetLoader for prefabs/GUIDs.
* **Systems**: GameSystem base; includes Lighting (uniforms), Audio (raytracing), Physics, MenuSystem (HTML UI), ClientPrediction.
* **Managers**: ModManager (mods/assets), UISettingsManager (resolutions/fullscreen).

### Server (Citadel)

* **GameServer.cs**: Entity/system management, spatial optimization, validation, raytracing.
* **NetworkManager.cs**: P2P messaging via Steam.

### Launcher (Trebuchet)

* **Launcher.cs**: Initializes Steam/server, contexts, mods, menu; main loop.

### Scenes

* **SandboxScene.cs**: 3D demo with player, grid, lighting; renders models with multi-textures.

### UI Elements

* HTML-parsed with CSS support; elements like ButtonElement, SelectElement handle interactions.

## Development Status and Roadmap

* Stable vertical slice: 3D sandbox, HTML menu, server validation.
* Next: Dev menu template, DLL stubs (IPanel), dynamic loading, project modules, P2P IDE sync.
* Dependencies: Silk.NET, Steam SDK; no internet/pip.
## Folder Structure
```mermaid
graph LR
    A[Castle Repository] --> B[Citadel]
    A --> C[SiegeEngine]
    A --> D[Foundation]
    A --> E[Trebuchet]
    A --> F[Specialized Modules - Stubs]
    A --> G[Assets]
    A --> H[Mods]

    subgraph Citadel Files
    B --> B1[Network/NetworkManager.cs]
    B --> B2[Server/GameServer.cs]
    B --> B3[Server/ServerValidationSystem.cs]
    B --> B4[Server/EntityDeltaTracker.cs]
    B --> B5[Server/ServerProgram.cs]
    end

    subgraph SiegeEngine Files
    C --> C1[Scenes/SandboxScene.cs]
    C --> C2[Systems/MenuSystem.cs]
    C --> C3[Events/EventBus.cs]
    C --> C4[Managers/ModManager.cs]
    C --> C5[ContextManagement/IRenderContext]
    C --> C6[AssetParsing/FBXParser.cs]
    C --> C7[Rendering/ShaderProgram.cs]
    C --> C8[Rendering/VertexBuffer.cs]
    C --> C9[Systems/LightingSystem.cs]
    C --> C10[Systems/AudioSystem.cs]
    C --> C11[UI/HtmlElement.cs]
    end

    D --> D1[Program.cs]

    E --> E1[Launcher.cs]

    subgraph Stubs
    F --> F1[MapRoom/Class1.cs - Stub]
    F --> F2[QuestHall/Class1.cs - Stub]
    F --> F3[ScriptChamber/Class1.cs - Stub]
    F --> F4[ThroneRoom/Class1.cs - Stub]
    F --> F5[GuildTower/Class1.cs - Stub]
    F --> F6[ReadingChamber/Class1.cs - Stub]
    end

    G --> G1[Models - FBX files]
    G --> G2[Textures]
    G --> G3[Configs/MainMenu.html]

    H --> H1[mod.json - Example Mods]
```
## Core Class Diagram

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
        +RequestRayTrace(Vector3 start, Vector3 dir, float maxDist) RayTraceResult
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
## Startup Sequence
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
## Component Diagram for Modularity
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
## Rendering System Class Diagram
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
    A[Client Sends Action (e.g., Movement/Input)] --> B[GameServer Receives via NetworkManager/EventBus]
    B --> C[QueueNetworkEvent(IEvent)]
    C --> D[Update(deltaTime): Dequeue and Publish]
    D --> E[ServerValidationSystem.Validate* (e.g., Movement/Inventory/Input)]
    E -->|Valid| F[Update Entity/State, Publish Event (networkSync=true)]
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
```mermaid

```
```mermaid

```
```mermaid

```
