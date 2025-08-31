using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using SiegeEngine.Interfaces;
using SiegeEngine.Networking;
using SiegeEngine.Events;
using SiegeEngine.Definitions;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Rendering.Shaders;
using SiegeEngine.Rendering;
using SiegeEngine.Rendering.Definitions;

namespace SiegeEngine.Scenes
{
    public unsafe class EditorScene : Scene
    {
        private readonly InputHandler _input;
        private readonly CameraController _camera;
        private readonly EventBus _eventBus;
        private readonly SteamEngine _steamEngine;
        private VertexBuffer _entitiesBuffer;
        private readonly List<Entity> _entities = new List<Entity>();
        private readonly Dictionary<string, int> _brushTypes = new Dictionary<string, int>
        {
            { "Floor", TextureDefinitions.Dirt }, { "Wall", TextureDefinitions.Stone }, { "Door", TextureDefinitions.Door },
            { "Trap", TextureDefinitions.Trap }, { "Light", TextureDefinitions.Light }, { "Fire", TextureDefinitions.Fire },
            { "Roof", TextureDefinitions.Roof }, { "Window", TextureDefinitions.Window }, { "Pathway", TextureDefinitions.Pathway },
            { "Road", TextureDefinitions.Road }, { "Bridge", TextureDefinitions.Bridge }, { "Water", TextureDefinitions.Water },
            { "Monster", TextureDefinitions.Monster }, { "Raise", -1 }, { "Lower", -1 }
        };
        private readonly Dictionary<string, BrushDefinition> _brushRegistry = new Dictionary<string, BrushDefinition>();
        private string _currentBrush = null;
        private bool _mouseCaptured = false;
        public bool IsMouseCaptured => _mouseCaptured;
        public bool _gridSnap;
        private Entity _previewEntity;
        private Vector3 _lastMouseWorldPos;
        private Vector2 _lastSentPos;
        private int _gridWidth = 128;
        private int _gridHeight = 72;
        private float[,] _gridHeights;
        private bool _dragging = false;
        private float _brushSize = 3.0f;
        private readonly string _callbackId;
        private enum EditorMode
        {
            Idle,
            Preview,
            WallPlacementStart,
            WallPlacementEnd
        }
        private EditorMode _editorMode = EditorMode.Idle;
        private Vector3 _startVertex;
        public event Action<Entity, string> OnBrushPlaced;

        public EditorScene(IRenderContext renderContext, Glfw glfw, WindowHandle* window, IGameServer server, EventBus eventBus, ISteamEngine steamEngine, InputHandler input)
            : base(renderContext, glfw, window, server, eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _steamEngine = (SteamEngine)steamEngine ?? throw new ArgumentNullException(nameof(steamEngine));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _camera = new CameraController(glfw, null);
            _gridHeights = new float[_gridWidth + 1, _gridHeight + 1];
            _callbackId = $"EditorScene_{Guid.NewGuid()}";
            InitializeBrushRegistry();
        }

        private void InitializeBrushRegistry()
        {
            _brushRegistry["Floor"] = new BrushDefinition { Type = "Floor", Size = new Vector3(1f, 1f, 0f), TextureId = TextureDefinitions.Dirt };
            _brushRegistry["Wall"] = new BrushDefinition { Type = "Wall", Size = new Vector3(1f, 0.2f, 2f), TextureId = TextureDefinitions.Stone };
            _brushRegistry["Door"] = new BrushDefinition { Type = "Door", Size = new Vector3(1f, 0.2f, 1.5f), TextureId = TextureDefinitions.Door };
            _brushRegistry["Trap"] = new BrushDefinition { Type = "Trap", Size = new Vector3(1f, 1f, 0.2f), TextureId = TextureDefinitions.Trap };
            _brushRegistry["Light"] = new BrushDefinition { Type = "Light", Size = new Vector3(0.2f, 0.2f, 0.1f), TextureId = TextureDefinitions.Light };
            _brushRegistry["Fire"] = new BrushDefinition { Type = "Fire", Size = new Vector3(0.5f, 0.5f, 0.3f), TextureId = TextureDefinitions.Fire };
            _brushRegistry["Roof"] = new BrushDefinition { Type = "Roof", Size = new Vector3(1f, 1f, 2f), TextureId = TextureDefinitions.Roof };
            _brushRegistry["Window"] = new BrushDefinition { Type = "Window", Size = new Vector3(0.5f, 0.2f, 0.5f), TextureId = TextureDefinitions.Window };
            _brushRegistry["Pathway"] = new BrushDefinition { Type = "Pathway", Size = new Vector3(1f, 1f, 0f), TextureId = TextureDefinitions.Pathway };
            _brushRegistry["Road"] = new BrushDefinition { Type = "Road", Size = new Vector3(1f, 1f, 0f), TextureId = TextureDefinitions.Road };
            _brushRegistry["Bridge"] = new BrushDefinition { Type = "Bridge", Size = new Vector3(1f, 1f, 0.25f), TextureId = TextureDefinitions.Bridge };
            _brushRegistry["Water"] = new BrushDefinition { Type = "Water", Size = new Vector3(1f, 1f, 0f), TextureId = TextureDefinitions.Water };
            _brushRegistry["Monster"] = new BrushDefinition { Type = "Monster", Size = new Vector3(1f, 1f, 1f), TextureId = TextureDefinitions.Monster };
            _brushRegistry["Raise"] = new BrushDefinition { Type = "Raise", Size = Vector3.Zero, TextureId = -1 };
            _brushRegistry["Lower"] = new BrushDefinition { Type = "Lower", Size = Vector3.Zero, TextureId = -1 };
        }

        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            _entitiesBuffer = new VertexBuffer(_renderContext);

            _eventBus.Subscribe<SelectBrushEvent>(OnSelectBrush);
            _eventBus.Subscribe<EntityMovedEvent>(OnEntityMoved);
            _eventBus.Subscribe<EntityPlacedEvent>(OnEntityPlaced);
            _eventBus.Subscribe<BrushRegistryUpdateEvent>(OnBrushRegistryUpdate);
            _eventBus.Subscribe<MouseInputEvent>(OnMouseInput);

            _input.SetMouseCallback(_callbackId, (button, action) =>
            {
                Vector2 mousePos = GetMousePosition();
                HandleMouseInput(new MouseInputEvent(mousePos, button, action, _steamEngine.GetSteamId()));
            });

            _glfw.SetKeyCallback(_window, (w, key, scancode, action, mods) =>
            {
                if (key == Keys.Escape && (int)action == (int)InputAction.Press)
                {
                    if (_editorMode == EditorMode.WallPlacementStart)
                    {
                        _editorMode = EditorMode.Preview;
                        UpdatePreview();
                    }
                    else if (_editorMode == EditorMode.Preview)
                    {
                        _editorMode = EditorMode.Idle;
                        if (_previewEntity != null)
                        {
                            _server.RemoveEntity(_previewEntity.Id);
                            _entities.Remove(_previewEntity);
                            _previewEntity = null;
                        }
                    }
                    if (_mouseCaptured)
                    {
                        _mouseCaptured = false;
                        _glfw.SetInputMode(_window, CursorStateAttribute.Cursor, CursorModeValue.CursorNormal);
                    }
                }
            });

            UpdatePreview();
        }

        protected override ShaderProgram CreateShader()
        {
            return new ShaderProgram(_renderContext, EditorSceneShader.VertexShaderSource, EditorSceneShader.FragmentShaderSource);
        }

        protected override void SetupGrid()
        {
            var vertices = new List<Vertex>();
            float step = 1.0f;

            for (float x = 0; x <= _gridWidth; x += step)
            {
                for (float y = 0; y < _gridHeight; y += step * 2)
                {
                    float z1 = GetGridHeightAt((int)x, (int)y);
                    float z2 = GetGridHeightAt((int)x, (int)(y + step));
                    vertices.Add(new Vertex(x, y, z1, 0.6f, 0.6f, 0.6f, 1.0f));
                    vertices.Add(new Vertex(x, Math.Min(y + step, _gridHeight), z2, 0.6f, 0.6f, 0.6f, 1.0f));
                }
            }

            for (float y = 0; y <= _gridHeight; y += step)
            {
                for (float x = 0; x < _gridWidth; x += step * 2)
                {
                    float z1 = GetGridHeightAt((int)x, (int)y);
                    float z2 = GetGridHeightAt((int)(x + step), (int)y);
                    vertices.Add(new Vertex(x, y, z1, 0.6f, 0.6f, 0.6f, 1.0f));
                    vertices.Add(new Vertex(Math.Min(x + step, _gridWidth), y, z2, 0.6f, 0.6f, 0.6f, 1.0f));
                }
            }

            var indices = new List<uint>();
            for (uint i = 0; i < vertices.Count; i++)
                indices.Add(i);

            _gridBuffer.UpdateCustom(vertices, indices);
        }

        public override void Update(float deltaTime)
        {
            _camera.Update(deltaTime, _window, 0f, _mouseCaptured);
            if (!_mouseCaptured) return;

            Vector2 mousePos = GetMousePosition();
            Vector3 worldPos = ScreenToWorld(mousePos);
            if (_gridSnap)
            {
                worldPos.X = (float)Math.Round(worldPos.X);
                worldPos.Y = (float)Math.Round(worldPos.Y);
            }

            if (_dragging && (_currentBrush == "Raise" || _currentBrush == "Lower"))
            {
                AdjustGridHeight(worldPos, _currentBrush == "Raise" ? 1f : -1f, deltaTime);
            }
            else if (_previewEntity != null)
            {
                var preview = _previewEntity.GetComponent<PreviewComponent>();
                if (preview != null && preview.PlayerId == _steamEngine.GetSteamId())
                {
                    worldPos.Z = GetGridHeightAt((int)worldPos.X, (int)worldPos.Y);
                    Vector2 newPos = new Vector2(worldPos.X, worldPos.Y);
                    if (newPos != _lastSentPos)
                    {
                        _server.ValidateAndUpdateMovement(_previewEntity.Id, newPos, Quaternion.Identity, _steamEngine.GetSteamId());
                        _lastSentPos = newPos;
                    }
                    var physics = _previewEntity.GetComponent<PhysicsComponent>();
                    if (physics != null)
                        physics.Position = worldPos;
                    _lastMouseWorldPos = worldPos;
                    UpdateGridSize(worldPos);
                }
            }
        }

        public override void Render(IReadOnlyList<Entity> entities)
        {
            base.Render(entities);

            Matrix4x4 view = _camera.ViewMatrix;
            _shader.SetMatrix4("uView", view);

            var vertices = new List<Vertex>();
            var indices = new List<uint>();
            uint index = 0;

            foreach (var entity in entities)
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                var wall = entity.GetComponent<WallComponent>();
                var preview = entity.GetComponent<PreviewComponent>();

                if (physics != null && wall == null)
                {
                    float alpha = preview != null && preview.IsPreview ? 0.35f : 1.0f;
                    Vector3 pos = physics.Position;
                    Vector3 size = physics.Size;

                    vertices.Add(new Vertex(pos.X, pos.Y, pos.Z, 0.8f, 0.8f, 0.8f, alpha));
                    vertices.Add(new Vertex(pos.X + size.X, pos.Y, pos.Z, 0.8f, 0.8f, 0.8f, alpha));
                    vertices.Add(new Vertex(pos.X, pos.Y + size.Y, pos.Z, 0.8f, 0.8f, 0.8f, alpha));
                    vertices.Add(new Vertex(pos.X + size.X, pos.Y + size.Y, pos.Z, 0.8f, 0.8f, 0.8f, alpha));

                    indices.Add(index); indices.Add(index + 1); indices.Add(index + 2);
                    indices.Add(index + 1); indices.Add(index + 2); indices.Add(index + 3);
                    index += 4;

                    if (preview != null && preview.IsPreview)
                    {
                        _shader.SetUniform("uOutline", 1.0f);
                        _entitiesBuffer.UpdateCustom(vertices, indices);
                        _entitiesBuffer.Bind();
                        _renderContext.DrawElements(PrimitiveType.Triangles, _entitiesBuffer.GetIndexCount(), DrawElementsType.UnsignedInt, null);
                        _shader.SetUniform("uOutline", 0.0f);
                    }
                }
                else if (wall != null)
                {
                    float alpha = preview != null && preview.IsPreview ? 0.35f : 1.0f;
                    vertices.Add(new Vertex(wall.StartVertex.X, wall.StartVertex.Y, wall.StartVertex.Z, 0.0f, 1.0f, 0.0f, alpha));
                    vertices.Add(new Vertex(wall.EndVertex.X, wall.EndVertex.Y, wall.EndVertex.Z, 0.0f, 1.0f, 0.0f, alpha));

                    indices.Add(index); indices.Add(index + 1);
                    index += 2;

                    if (preview != null && preview.IsPreview)
                    {
                        _shader.SetUniform("uOutline", 1.0f);
                        _entitiesBuffer.UpdateCustom(vertices, indices);
                        _entitiesBuffer.Bind();
                        _renderContext.DrawElements(PrimitiveType.Lines, _entitiesBuffer.GetIndexCount(), DrawElementsType.UnsignedInt, null);
                        _shader.SetUniform("uOutline", 0.0f);
                    }
                }
            }

            _entitiesBuffer.UpdateCustom(vertices, indices);
            _entitiesBuffer.Bind();
            _renderContext.DrawElements(_currentBrush == "Wall" ? PrimitiveType.Lines : PrimitiveType.Triangles, _entitiesBuffer.GetIndexCount(), DrawElementsType.UnsignedInt, null);
        }

        private void OnSelectBrush(SelectBrushEvent e)
        {
            if (e.PlayerId == _steamEngine.GetSteamId())
                SetBrush(e.BrushType);
        }

        private void OnEntityMoved(EntityMovedEvent e)
        {
            var entity = _entities.Find(x => x.Id == e.EntityId);
            if (entity != null)
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null)
                    physics.Position = new Vector3(e.Position.X, e.Position.Y, physics.Position.Z);
            }
        }

        private void OnEntityPlaced(EntityPlacedEvent e)
        {
            if (!e.IsPreview)
            {
                var entity = new Entity { Id = e.EntityId, Type = e.EntityType };
                if (e.EntityType == "Wall")
                {
                    entity.AddComponent(new WallComponent { StartVertex = e.Position, EndVertex = e.Position });
                }
                else
                {
                    entity.AddComponent(new PhysicsComponent { Position = e.Position, Size = GetSize(e.EntityType) });
                }
                _entities.Add(entity);
                OnBrushPlaced?.Invoke(entity, e.EntityType);
            }
            else if (_previewEntity == null || _previewEntity.Id != e.EntityId)
            {
                _previewEntity = new Entity { Id = e.EntityId, Type = e.EntityType };
                if (e.EntityType == "Wall")
                {
                    _previewEntity.AddComponent(new WallComponent { StartVertex = e.Position, EndVertex = e.Position, IsPreview = true });
                }
                else
                {
                    _previewEntity.AddComponent(new PhysicsComponent { Position = e.Position, Size = GetSize(e.EntityType) });
                }
                _previewEntity.AddComponent(new PreviewComponent(e.PlayerId ?? 0, e.EntityType));
                _entities.Add(_previewEntity);
                _lastSentPos = new Vector2(e.Position.X, e.Position.Y);
            }
        }

        private void OnBrushRegistryUpdate(BrushRegistryUpdateEvent e)
        {
            _brushRegistry[e.BrushType] = new BrushDefinition { Type = e.BrushType, Size = e.Size, TextureId = e.TextureId };
            _brushTypes[e.BrushType] = e.TextureId;
        }

        private void OnMouseInput(MouseInputEvent e)
        {
            if (e.SteamId == _steamEngine.GetSteamId())
            {
                HandleMouseInput(e);
            }
        }

        private void HandleMouseInput(MouseInputEvent e)
        {
            Vector2 mousePos = e.Position;
            MouseButton button = e.Button;
            InputAction action = e.Action;

            if (button == MouseButton.Left)
            {
                if (action == InputAction.Press)
                {
                    if (!_mouseCaptured && mousePos.X > 250)
                    {
                        _mouseCaptured = true;
                        _glfw.SetInputMode(_window, CursorStateAttribute.Cursor, CursorModeValue.CursorDisabled);
                    }
                    if (_mouseCaptured && _currentBrush != null)
                    {
                        Vector3 worldPos = ScreenToWorld(mousePos);
                        if (_gridSnap)
                        {
                            worldPos.X = (float)Math.Round(worldPos.X);
                            worldPos.Y = (float)Math.Round(worldPos.Y);
                        }
                        if (_editorMode == EditorMode.Preview)
                        {
                            if (_currentBrush == "Raise" || _currentBrush == "Lower")
                            {
                                _dragging = true;
                            }
                            else if (_currentBrush == "Wall")
                            {
                                _startVertex = worldPos;
                                _editorMode = EditorMode.WallPlacementStart;
                            }
                            else
                            {
                                PlaceEntity(worldPos);
                            }
                        }
                        else if (_editorMode == EditorMode.WallPlacementStart && _currentBrush == "Wall")
                        {
                            PlaceWall(worldPos);
                        }
                    }
                }
                else if (action == InputAction.Release)
                {
                    _dragging = false;
                }
            }
        }

        private Vector2 GetMousePosition()
        {
            _glfw.GetCursorPos(_window, out double mx, out double my);
            return new Vector2((float)mx, (float)my);
        }

        private Vector3 ScreenToWorld(Vector2 screenPos)
        {
            float x = Math.Clamp(screenPos.X / _width * _gridWidth, 0, _gridWidth);
            float y = Math.Clamp(screenPos.Y / _height * _gridHeight, 0, _gridHeight);
            float z = GetGridHeightAt((int)x, (int)y);
            return new Vector3(x, y, z);
        }

        private Vector3 GetSize(string brushType)
        {
            return _brushRegistry.TryGetValue(brushType, out var def) ? def.Size : new Vector3(1f, 1f, 0f);
        }

        private void PlaceEntity(Vector3 position)
        {
            if (_previewEntity != null)
            {
                _server.RemoveEntity(_previewEntity.Id);
                _entities.Remove(_previewEntity);
                _previewEntity = null;
            }
            Entity entity = new Entity { Id = _entities.Count + 1, Type = _currentBrush };
            entity.AddComponent(new PhysicsComponent { Position = position, Size = GetSize(_currentBrush) });
            _entities.Add(entity);
            _server.AddEntity(entity);
            _server.Publish(new EntityPlacedEvent(entity.Id, _currentBrush, position));
            OnBrushPlaced?.Invoke(entity, _currentBrush);
        }

        private void PlaceWall(Vector3 endVertex)
        {
            if (_previewEntity != null)
            {
                _server.RemoveEntity(_previewEntity.Id);
                _entities.Remove(_previewEntity);
                _previewEntity = null;
            }
            Entity entity = new Entity { Id = _entities.Count + 1, Type = "Wall" };
            entity.AddComponent(new WallComponent { StartVertex = _startVertex, EndVertex = endVertex });
            _entities.Add(entity);
            _server.AddEntity(entity);
            _server.Publish(new EntityPlacedEvent(entity.Id, "Wall", _startVertex));
            OnBrushPlaced?.Invoke(entity, "Wall");
            _editorMode = EditorMode.Preview;
        }

        private void AdjustGridHeight(Vector3 center, float delta, float deltaTime)
        {
            int x = (int)center.X;
            int y = (int)center.Y;
            float change = delta * deltaTime * 5f;

            for (int i = Math.Max(0, x - (int)_brushSize); i <= Math.Min(_gridWidth, x + (int)_brushSize); i++)
            {
                for (int j = Math.Max(0, y - (int)_brushSize); j <= Math.Min(_gridHeight, y + (int)_brushSize); j++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(i, j));
                    if (distance <= _brushSize)
                    {
                        float intensity = (float)Math.Cos(distance / _brushSize * Math.PI / 2);
                        _gridHeights[i, j] += change * intensity;
                        _gridHeights[i, j] = Math.Clamp(_gridHeights[i, j], -50f, 50f);
                    }
                }
            }
            SetupGrid();
            foreach (var entity in _entities)
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null)
                    physics.Position = new Vector3(physics.Position.X, physics.Position.Y, GetGridHeightAt((int)physics.Position.X, (int)physics.Position.Y));
            }
        }

        private float GetGridHeightAt(int x, int y)
        {
            x = Math.Clamp(x, 0, _gridWidth);
            y = Math.Clamp(y, 0, _gridHeight);
            return _gridHeights[x, y];
        }

        private void UpdatePreview()
        {
            if (_currentBrush != null && _currentBrush != "Raise" && _currentBrush != "Lower")
            {
                Vector2 mousePos = GetMousePosition();
                Vector3 worldPos = ScreenToWorld(mousePos);
                if (_gridSnap)
                {
                    worldPos.X = (float)Math.Round(worldPos.X);
                    worldPos.Y = (float)Math.Round(worldPos.Y);
                }
                worldPos.Z = GetGridHeightAt((int)worldPos.X, (int)worldPos.Y);

                if (_previewEntity == null && _steamEngine.GetSteamId() != 0)
                {
                    _previewEntity = new Entity { Id = -(_entities.Count + 1), Type = _currentBrush };
                    if (_currentBrush == "Wall")
                    {
                        _previewEntity.AddComponent(new WallComponent { StartVertex = worldPos, EndVertex = worldPos, IsPreview = true });
                    }
                    else
                    {
                        _previewEntity.AddComponent(new PhysicsComponent { Position = worldPos, Size = GetSize(_currentBrush) });
                    }
                    _previewEntity.AddComponent(new PreviewComponent(_steamEngine.GetSteamId(), _currentBrush));
                    _entities.Add(_previewEntity);
                    _server.AddEntity(_previewEntity);
                }
                else if (_previewEntity != null)
                {
                    var preview = _previewEntity.GetComponent<PreviewComponent>();
                    if (preview != null && preview.PlayerId == _steamEngine.GetSteamId())
                    {
                        _previewEntity.Type = _currentBrush;
                        if (_currentBrush == "Wall")
                        {
                            var wall = _previewEntity.GetComponent<WallComponent>();
                            if (wall == null)
                            {
                                _previewEntity.RemoveComponent<PhysicsComponent>();
                                _previewEntity.AddComponent(new WallComponent { StartVertex = worldPos, EndVertex = worldPos, IsPreview = true });
                            }
                            else if (_editorMode == EditorMode.Preview)
                            {
                                wall.StartVertex = wall.EndVertex = worldPos;
                            }
                            else if (_editorMode == EditorMode.WallPlacementStart)
                            {
                                wall.StartVertex = _startVertex;
                                wall.EndVertex = worldPos;
                            }
                        }
                        else
                        {
                            var physics = _previewEntity.GetComponent<PhysicsComponent>();
                            if (physics == null)
                            {
                                _previewEntity.RemoveComponent<WallComponent>();
                                _previewEntity.AddComponent(new PhysicsComponent { Position = worldPos, Size = GetSize(_currentBrush) });
                            }
                            else
                            {
                                physics.Size = GetSize(_currentBrush);
                                physics.Position = worldPos;
                            }
                        }
                    }
                }
                _lastMouseWorldPos = worldPos;
                UpdateGridSize(worldPos);
            }
            else if (_previewEntity != null)
            {
                _server.RemoveEntity(_previewEntity.Id);
                _entities.Remove(_previewEntity);
                _previewEntity = null;
            }
        }

        private void UpdateGridSize(Vector3 position)
        {
            int buffer = 10;
            int newWidth = Math.Max(_gridWidth, (int)(position.X + buffer));
            int newHeight = Math.Max(_gridHeight, (int)(position.Y + buffer));
            if (newWidth > _gridWidth || newHeight > _gridHeight)
            {
                var oldHeights = _gridHeights;
                _gridWidth = newWidth;
                _gridHeight = newHeight;
                _gridHeights = new float[_gridWidth + 1, _gridHeight + 1];
                for (int i = 0; i <= Math.Min(oldHeights.GetLength(0) - 1, _gridWidth); i++)
                    for (int j = 0; j <= Math.Min(oldHeights.GetLength(1) - 1, _gridHeight); j++)
                        _gridHeights[i, j] = oldHeights[i, j];
                SetupGrid();
            }
        }

        public void SaveLevel(string path)
        {
            var level = new LevelData
            {
                Width = _gridWidth,
                Height = _gridHeight,
                Entities = _entities.Where(e => e.GetComponent<PreviewComponent>() == null).Select(e => new EntityData
                {
                    Type = e.Type,
                    Position = e.GetComponent<PhysicsComponent>()?.Position ?? e.GetComponent<WallComponent>().Position,
                    TextureId = _brushTypes[e.Type],
                    Height = e.GetComponent<PhysicsComponent>()?.Size.Z ?? e.GetComponent<WallComponent>().Size.Z
                }).ToList()
            };
            File.WriteAllText(path, JsonSerializer.Serialize(level, new JsonSerializerOptions { WriteIndented = true }));
        }

        public Vector3 GetCameraPosition()
        {
            return _camera.Position;
        }

        public void SetBrush(string brushType)
        {
            if (_brushRegistry.ContainsKey(brushType))
            {
                _currentBrush = brushType;
                _editorMode = EditorMode.Preview;
                _previewEntity = null;
                UpdatePreview();
            }
        }

        public void ToggleGridSnap(bool state)
        {
            _gridSnap = state;
            UpdatePreview();
        }

        public override void Dispose()
        {
            if (!_disposed)
            {
                _entitiesBuffer?.Dispose();
                base.Dispose();
            }
        }
    }

    public class BrushDefinition
    {
        public string Type { get; set; }
        public Vector3 Size { get; set; }
        public int TextureId { get; set; }
    }
}