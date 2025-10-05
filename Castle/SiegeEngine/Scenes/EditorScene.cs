// Folder: SiegeEngine/Scenes
// File: EditorScene.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Definitions;
using SiegeEngine.Events;
using SiegeEngine.Interfaces;
using SiegeEngine.Managers;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Rendering;
using SiegeEngine.Rendering.Shaders;
using SiegeEngine.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Scenes
{
    public unsafe class EditorScene : Scene
    {
        public EditorScene(IRenderContext renderContext, IControlContext controlContext, IntPtr window, Player player, IGameServer server, PlayerMovement playerMovement, EventBus eventBus, ModelManager modelManager)
            : base(renderContext, controlContext, window, server, eventBus)
        {
        }
        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
        }
        public override void Update(float deltaTime)
        {
        }
        public override void Render(IReadOnlyList<Entity> entities)
        {
            _renderContext.ClearColor(1.0f, 0.0f, 0.0f, 1.0f);
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
        }
        public override void Dispose()
        {
            base.Dispose();
        }
    }
}