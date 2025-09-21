\# Castle Engine Event System Design Summary (Sept 14, 2025)



\## Overview

\- \*\*Core Philosophy\*\*: Strongly-typed for safety/performance; modular for mods. Protect server (Citadel EXE) via interfaces/abstractions. Users extend without recompiling core.

\- \*\*Components\*\*: EventBus handles pub/sub; ModManager validates/loads; IGameServer abstracts server access.



\## Event Categories

1\. \*\*Strongly Typed Events\*\*: Public core events (e.g., EntityMovedEvent). Subscribable anywhere.

2\. \*\*Protected Strongly Typed\*\*: Internal/sensitive (e.g., ValidateMovementEvent). Defined via \[ProtectedEvent] attribute; Publish checks caller auth (SteamID/token).

3\. \*\*Custom Events (Wildcard Args)\*\*: User-defined flexible events. Use CustomEvent : IEvent with Dictionary<string, object> payload (named args for better typing).

4\. \*\*Custom Strongly Typed\*\*: User-created MyModEvent : IEvent. Subscribable; validated by ModManager.

5\. \*\*Custom Protected Strongly Typed\*\*: Like custom strongly typed, but with \[ProtectedEvent] for server extensions (e.g., custom validation).



\## Protection Mechanisms

\- \*\*Definition\*\*: \[ProtectedEvent] attribute on class (compile-time, extensible). Alternatives: Namespace prefix or mod.json config.

\- \*\*Registry\*\*: EventBus.Dictionary<Type, EventMetadata> (IsProtected, etc.). RegisterProtected<T>() in server init.

\- \*\*Publish Checks\*\*: If protected, verify caller (SteamID/internal). Reject unvalidated.

\- \*\*Subscribe/Publish for Customs\*\*: ModManager.IsWhitelistedHook/Dll before allowing.

\- \*\*Networking\*\*: Only sync non-protected/validated events (in Publish, check before SendToAll).

\- \*\*Wildcard Calls\*\*: Safe invoker wrapper: Void methods only, param validation, try-catch, \[SafeInvoke] required.



\## Networking/IGameServer

\- User Actions: Custom P2P via QueueNetworkEvent(IEvent e) – queues for server validation.

\- No direct NetworkManager access; route through IGameServer.



\## Citadel EXE Integration

\- \*\*Type\*\*: Console app (compiles to EXE).

\- \*\*Client Connect\*\*: Auto-spawn subprocess on startup (Process.Start("Citadel.exe")). Localhost connect; future manual IP.

\- \*\*Args\*\*: Support --port, --mods-dir, --config for modded servers.



\## ModEventRegistry (in ModManager)

\- Pre-validate custom events at load (from mod.json/DLL reflection).

\- Register types, flag protected, consult for permissions.



\## Open Questions/Deferred

\- Specific user networking needs (e.g., trade messages).

\- Server Extensions: DLLs implement IServerExtension; register via ModManager (deferred).

\- Testing: Unit tests for validation; integration for mod loading.



This design ensures security (no direct server access), modularity (easy custom events), and UX (intuitive for hobbyists).

