// Folder: MapRoom
// File: TwoDCreatorScene.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Scenes;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace MapRoom
{
    public class TwoDCreatorScene : Scene
    {
        private AngledOrthoCamera _orthoCamera;
        private VertexBuffer _gridBuffer;
        private string _activeSpriteTexturePath = null;
        private Vector2 _activeSpriteSize = new Vector2(2f, 2f);
        private bool _spriteGhostVisible = false;
        private Vector3 _spriteGhostPosition;
        private VertexBuffer _ghostBuffer;

        public TwoDCreatorScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            _eventBus.Subscribe<SelectSpriteEvent>(OnSpriteSelected);
        }

        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            _orthoCamera = new AngledOrthoCamera(_controlContext, _window);
            SetupGrid();
            _ghostBuffer = new VertexBuffer(_renderContext);
        }

        private void SetupGrid()
        {
            var vertices = new List<Vertex>();
            for (int x = -5000; x <= 5000; x += 100)
            {
                vertices.Add(new Vertex(x, -5000, 0, 0.3f, 0.3f, 0.3f, 1f));
                vertices.Add(new Vertex(x, 5000, 0, 0.3f, 0.3f, 0.3f, 1f));
            }
            for (int y = -5000; y <= 5000; y += 100)
            {
                vertices.Add(new Vertex(-5000, y, 0, 0.3f, 0.3f, 0.3f, 1f));
                vertices.Add(new Vertex(5000, y, 0, 0.3f, 0.3f, 0.3f, 1f));
            }
            _gridBuffer = new VertexBuffer(_renderContext);
            _gridBuffer.UpdateCustom(vertices, new List<uint>());
        }

        private void OnSpriteSelected(SelectSpriteEvent e)
        {
            _activeSpriteTexturePath = e.TexturePath;
            _activeSpriteSize = new Vector2(e.Width, e.Height);
            Console.WriteLine($"[TwoDCreatorScene] Sprite selected for ghost preview: {e.TexturePath}");
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // Correct call to match CameraController.Update(float deltaTime, float scrollDelta, bool isGameActive)
            _orthoCamera.Update(deltaTime, 0f, true);

            // Correct GetCursorPos signature (matches IControlContext exactly)
            double mouseX = 0, mouseY = 0;
            _controlContext.GetCursorPos(_window, out mouseX, out mouseY);

            // Simple ortho mouse-to-world mapping (centered origin)
            float worldX = (float)(mouseX - _width / 2f);
            float worldY = (float)(_height / 2f - mouseY);
            _spriteGhostPosition = new Vector3(worldX, worldY, 0.1f);
            _spriteGhostVisible = !string.IsNullOrEmpty(_activeSpriteTexturePath);
        }

        protected override void RenderContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            projection = Matrix4x4.CreateOrthographic(_width * 1.5f, _height * 1.5f, 0.1f, 1000f);
            view = _orthoCamera.ViewMatrix;

            // Grid and entities rendering here (expand later with your existing shader pattern)

            if (_spriteGhostVisible && _ghostBuffer != null)
            {
                Matrix4x4 model = Matrix4x4.CreateTranslation(_spriteGhostPosition);
                // TODO: draw ghost quad (reuse brush ghost style - circle/square outline)
            }
        }

        public override void Dispose()
        {
            _gridBuffer?.Dispose();
            _ghostBuffer?.Dispose();
            base.Dispose();
        }
    }
}