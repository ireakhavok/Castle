using SiegeEngine.AssetParsing;
using SiegeEngine.ContextManagement;
using SiegeEngine.Definitions;
using SiegeEngine.Interfaces;
using SiegeEngine.Managers;
using SiegeEngine.Rendering;
using SiegeEngine.Rendering.Shaders;
using System;
using System.IO;
using System.Numerics;
namespace ReadingChamber
{
    public unsafe class AssetViewerPanel : IPanel
    {
        private readonly IRenderContext _renderContext;
        private readonly IControlContext _controlContext;
        private readonly IntPtr _window;
        private FBXModel _model;
        private ModelManager.ModelData _modelData;
        private float _time = 0f;
        private string _currentAnimation;
        private bool _playing = true;
        private ShaderProgram _assetShader;
        private Vector3 _cameraPosition = new Vector3(0, 0, 5);
        private Vector3 _cameraTarget = Vector3.Zero;
        private Vector3 _cameraUp = Vector3.UnitZ;
        private float _yaw = 0f;
        private float _pitch = 0f;
        private float _lastMouseX, _lastMouseY;
        private bool _firstMouse = true;
        private bool _isPanning = false;
        private string _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Characters", "Man_Mesh.fbx");
        public DockState DockState { get; set; } = DockState.Floating;
        public AssetViewerPanel(IRenderContext renderContext, IControlContext controlContext, IntPtr window)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
        }
        public void Init()
        {
            var modelManager = new ModelManager(renderContext: _renderContext);
            modelManager.LoadModel(_path, new HashSet<string>(), new Dictionary<string, string>());
            string key = Path.GetFileNameWithoutExtension(_path).ToLower();
            if (!modelManager.TryGetModel(key, out _model))
            {
                Console.WriteLine("AssetViewerPanel: Failed to load or parse model");
                return;
            }
            Console.WriteLine($"AssetViewerPanel: Loaded model with {_model.Meshes.Count} meshes");
            if (!modelManager.TryGetModelData(key, out _modelData))
            {
                Console.WriteLine("AssetViewerPanel: Failed to get model data");
                return;
            }
            // Center model based on bounds
            Vector3 minBounds = new Vector3(float.MaxValue);
            Vector3 maxBounds = new Vector3(float.MinValue);
            foreach (var mesh in _model.Meshes)
            {
                foreach (var v in mesh.Vertices)
                {
                    minBounds = Vector3.Min(minBounds, new Vector3(v.X, v.Y, v.Z));
                    maxBounds = Vector3.Max(maxBounds, new Vector3(v.X, v.Y, v.Z));
                }
            }
            Vector3 center = (minBounds + maxBounds) / 2;
            float maxExtent = Math.Max(maxBounds.X - minBounds.X, Math.Max(maxBounds.Y - minBounds.Y, maxBounds.Z - minBounds.Z)) / 2;
            _cameraPosition = center + new Vector3(0, -maxExtent * 2.5f, 0);
            _cameraTarget = center;
            Console.WriteLine($"AssetViewerPanel: Model center: {center}, maxExtent: {maxExtent}, cameraPosition: {_cameraPosition}");
            if (_model.Animations.Count > 0)
            {
                _currentAnimation = _model.Animations[0].Name;
            }
            // Initialize shader
            _assetShader = new ShaderProgram(_renderContext, AssetShader.VertexShaderSource, AssetShader.FragmentShaderSource);
        }
        public void Update(float deltaTime)
        {
            // Camera control
            _controlContext.GetCursorPos(_window, out double mx, out double my);
            float mouseX = (float)mx;
            float mouseY = (float)my;
            if (_firstMouse)
            {
                _lastMouseX = mouseX;
                _lastMouseY = mouseY;
                _firstMouse = false;
            }
            float deltaX = mouseX - _lastMouseX;
            float deltaY = _lastMouseY - mouseY;
            _lastMouseX = mouseX;
            _lastMouseY = mouseY;
            if (_controlContext.GetMouseButton(_window, MouseButton.Left) == InputAction.Press)
            {
                _yaw += -deltaX * 0.1f;
                _pitch += -deltaY * 0.1f;
            }
            if (_controlContext.GetMouseButton(_window, MouseButton.Right) == InputAction.Press)
            {
                _isPanning = true;
                Vector3 right = Vector3.Normalize(Vector3.Cross(_cameraPosition - _cameraTarget, _cameraUp));
                Vector3 up = Vector3.Normalize(Vector3.Cross(right, _cameraPosition - _cameraTarget));
                _cameraPosition += -right * deltaX * 0.01f + up * deltaY * 0.01f;
                _cameraTarget += -right * deltaX * 0.01f + up * deltaY * 0.01f;
            }
            else
            {
                _isPanning = false;
            }
            Vector3 front = new Vector3
            {
                X = MathF.Cos(MathF.PI * _yaw / 180) * MathF.Cos(MathF.PI * _pitch / 180),
                Y = MathF.Sin(MathF.PI * _yaw / 180) * MathF.Cos(MathF.PI * _pitch / 180),
                Z = MathF.Sin(MathF.PI * _pitch / 180)
            };
            if (!_isPanning)
            {
                _cameraPosition = _cameraTarget + Vector3.Normalize(front) * (_cameraPosition - _cameraTarget).Length();
            }
            if (_playing)
            {
                _time += deltaTime;
                if (_model.Skeleton != null && _model.Animations.Count > 0)
                {
                    var animation = _model.Animations.Find(a => a.Name == _currentAnimation);
                    if (animation != null)
                    {
                        var transforms = animation.GetBoneTransforms(_time);
                        _model.Skeleton.UpdateTransforms(transforms);
                    }
                }
            }
            // Handle input for animation selection, play/pause, etc. (expand with UI buttons)
            if (_controlContext.GetKey(_window, Key.Space) == InputAction.Press)
            {
                _playing = !_playing;
            }
        }
        public unsafe void Render()
        {
            _controlContext.GetWindowSize(_window, out int w, out int h);
            _renderContext.Viewport(0, 0, (uint)w, (uint)h);
            _renderContext.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.CullFace);
            _renderContext.CullFace(_renderContext.Enums.Back);
            // Set matrices
            Matrix4x4 modelMatrix = Matrix4x4.Identity;
            Matrix4x4 view = Matrix4x4.CreateLookAt(_cameraPosition, _cameraTarget, _cameraUp);
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, (float)w / h, 0.1f, 100f);
            _assetShader.Use();
            _assetShader.SetMatrix4("uModel", modelMatrix);
            _assetShader.SetMatrix4("uView", view);
            _assetShader.SetMatrix4("uProjection", projection);
            _assetShader.SetUniform("uLightDir", -0.707f, -0.707f, 0.707f);
            _assetShader.SetUniform("uLightColor", 1.0f, 1.0f, 1.0f);
            _assetShader.SetUniform("uLightIntensity", 1.0f);
            _assetShader.SetUniform("uViewPos", _cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z);
            _assetShader.SetUniform("uAmbientStrength", 0.3f);
            _assetShader.SetUniform("uSpecularStrength", 0.05f);
            _assetShader.SetUniform("uShininess", 4.0f);
            _assetShader.SetUniform("uHasBones", _model.HasSkin ? 1 : 0);
            if (_model.Skeleton != null && _model.HasSkin)
            {
                var transforms = _model.Skeleton.GetTransforms();
                _assetShader.SetMatrix4Array("uBoneTransforms", transforms);
            }
            foreach (var mmr in _modelData.MeshRenders)
            {
                // Bind textures
                for (int t = 0; t < Math.Min(mmr.AlbedoTextures.Length, 4); t++)
                {
                    _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + t);
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.AlbedoTextures[t]);
                    _assetShader.SetUniform($"uAlbedoMap[{t}]", t);
                }
                for (int t = 0; t < Math.Min(mmr.NormalTextures.Length, 4); t++)
                {
                    _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + 4 + t);
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.NormalTextures[t]);
                    _assetShader.SetUniform($"uNormalMap[{t}]", 4 + t);
                }
                for (int t = 0; t < Math.Min(mmr.MetallicTextures.Length, 4); t++)
                {
                    _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + 8 + t);
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.MetallicTextures[t]);
                    _assetShader.SetUniform($"uMetallicMap[{t}]", 8 + t);
                }
                _renderContext.BindVertexArray(mmr.Vao);
                _renderContext.DrawElements(_renderContext.Enums.Triangles, mmr.IndexCount, _renderContext.Enums.UnsignedInt, null);
                int error;
                while ((error = _renderContext.GetError()) != _renderContext.Enums.NoError)
                {
                    Console.WriteLine($"AssetViewerPanel: OpenGL error after draw: {error}");
                }
            }
        }
        public void Dispose()
        {
            // No need to delete VAOs etc., as they are managed by ModelManager
            _assetShader.Dispose();
        }
        public void Detach()
        {
            // Implement pop-out to new window
            // Create new ContextManager, new window, transfer state
        }
    }
}