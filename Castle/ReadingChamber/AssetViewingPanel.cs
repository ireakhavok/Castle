// Folder: ReadingChamber
// File: AssetViewerPanel.cs
using SiegeEngine.AssetObjects;
using SiegeEngine.AssetParsing;
using SiegeEngine.ContextManagement;
using SiegeEngine.Definitions;
using SiegeEngine.Interfaces;
using SiegeEngine.Rendering;
using SiegeEngine.Rendering.Shaders;
using System;
using System.Collections.Generic;
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
        private List<uint> _vaos = new List<uint>();
        private List<uint> _vbos = new List<uint>();
        private List<uint> _ebos = new List<uint>();
        private List<uint> _indexCounts = new List<uint>();
        private List<uint[]> _albedoTextures = new List<uint[]>();
        private List<uint[]> _normalTextures = new List<uint[]>();
        private List<uint[]> _metallicTextures = new List<uint[]>();
        private float _time = 0f;
        private string _currentAnimation;
        private bool _playing = true;
        private ShaderProgram _assetShader;
        private Vector3 _cameraPosition = new Vector3(0, 0, 5);
        private Vector3 _cameraTarget = Vector3.Zero;
        private Vector3 _cameraUp = Vector3.UnitY;
        private float _yaw = -90f;
        private float _pitch = 0f;
        private float _lastMouseX, _lastMouseY;
        private bool _firstMouse = true;
        private bool _isPanning = false;
        private uint _defaultAlbedo;
        private uint _defaultNormal;
        private uint _defaultMetallic;
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
            // Create default textures
            _defaultAlbedo = CreateDefaultTexture(new byte[] { 255, 255, 255, 255 }, _renderContext.Enums.Rgba);
            _defaultNormal = CreateDefaultTexture(new byte[] { 128, 128, 255, 255 }, _renderContext.Enums.Rgba);
            _defaultMetallic = CreateDefaultTexture(new byte[] { 0, 0, 0, 255 }, _renderContext.Enums.Rgba);

            // Load the asset
            if (!File.Exists(_path))
            {
                Console.WriteLine($"AssetViewerPanel: Sample FBX not found at {_path}");
                return;
            }
            FBXFileForest forest = FBXParser.Load(_path);
            _model = FBXParser.BuildModelFromForest(forest);
            if (_model == null || _model.Meshes.Count == 0)
            {
                Console.WriteLine("AssetViewerPanel: Failed to load or parse model");
                return;
            }
            Console.WriteLine($"AssetViewerPanel: Loaded model with {_model.Meshes.Count} meshes");

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
            _cameraPosition = center + new Vector3(0, 0, maxExtent * 2.5f);
            _cameraTarget = center;
            Console.WriteLine($"AssetViewerPanel: Model center: {center}, maxExtent: {maxExtent}, cameraPosition: {_cameraPosition}");

            // Setup VAO/VBO and textures for model
            int meshIndex = 0;
            foreach (var mesh in _model.Meshes)
            {
                float[] vertexData = new float[mesh.Vertices.Count * 20]; // Expanded for boneIDs and weights
                for (int i = 0; i < mesh.Vertices.Count; i++)
                {
                    var v = mesh.Vertices[i];
                    vertexData[i * 20 + 0] = v.X;
                    vertexData[i * 20 + 1] = v.Y;
                    vertexData[i * 20 + 2] = v.Z;
                    vertexData[i * 20 + 3] = v.Nx;
                    vertexData[i * 20 + 4] = v.Ny;
                    vertexData[i * 20 + 5] = v.Nz;
                    vertexData[i * 20 + 6] = v.U;
                    vertexData[i * 20 + 7] = v.V;
                    vertexData[i * 20 + 8] = v.MatIdx;
                    vertexData[i * 20 + 9] = v.Tx;
                    vertexData[i * 20 + 10] = v.Ty;
                    vertexData[i * 20 + 11] = v.Tz;
                    vertexData[i * 20 + 12] = v.BoneID0;
                    vertexData[i * 20 + 13] = v.BoneID1;
                    vertexData[i * 20 + 14] = v.BoneID2;
                    vertexData[i * 20 + 15] = v.BoneID3;
                    vertexData[i * 20 + 16] = v.Weight0;
                    vertexData[i * 20 + 17] = v.Weight1;
                    vertexData[i * 20 + 18] = v.Weight2;
                    vertexData[i * 20 + 19] = v.Weight3;
                }

                uint vao = _renderContext.GenVertexArray();
                uint vbo = _renderContext.GenBuffer();
                uint ebo = _renderContext.GenBuffer();

                _renderContext.BindVertexArray(vao);

                fixed (float* ptr = vertexData)
                {
                    _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, vbo);
                    _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(vertexData.Length * sizeof(float)), ptr, _renderContext.Enums.StaticDraw);
                }

                fixed (uint* ptr = mesh.Indices.ToArray())
                {
                    _renderContext.BindBuffer(_renderContext.Enums.ElementArrayBuffer, ebo);
                    _renderContext.BufferData(_renderContext.Enums.ElementArrayBuffer, (uint)(mesh.Indices.Count * sizeof(uint)), ptr, _renderContext.Enums.StaticDraw);
                }

                uint stride = 20 * sizeof(float);
                _renderContext.EnableVertexAttribArray(0); // Position
                _renderContext.VertexAttribPointer(0, 3, _renderContext.Enums.Float, false, stride, (void*)0);
                _renderContext.EnableVertexAttribArray(3); // Normal
                _renderContext.VertexAttribPointer(3, 3, _renderContext.Enums.Float, false, stride, (void*)(3 * sizeof(float)));
                _renderContext.EnableVertexAttribArray(2); // UV
                _renderContext.VertexAttribPointer(2, 2, _renderContext.Enums.Float, false, stride, (void*)(6 * sizeof(float)));
                _renderContext.EnableVertexAttribArray(4); // MaterialIndex
                _renderContext.VertexAttribPointer(4, 1, _renderContext.Enums.Float, false, stride, (void*)(8 * sizeof(float)));
                _renderContext.EnableVertexAttribArray(5); // Tangent
                _renderContext.VertexAttribPointer(5, 3, _renderContext.Enums.Float, false, stride, (void*)(9 * sizeof(float)));
                _renderContext.EnableVertexAttribArray(6); // BoneIDs
                _renderContext.VertexAttribIPointer(6, 4, _renderContext.Enums.Int, stride, (void*)(12 * sizeof(float)));
                _renderContext.EnableVertexAttribArray(7); // Weights
                _renderContext.VertexAttribPointer(7, 4, _renderContext.Enums.Float, false, stride, (void*)(16 * sizeof(float)));

                _renderContext.BindVertexArray(0);

                _vaos.Add(vao);
                _vbos.Add(vbo);
                _ebos.Add(ebo);
                _indexCounts.Add((uint)mesh.Indices.Count);

                // Load textures for this mesh
                uint[] albedos = new uint[mesh.Materials.Count];
                uint[] normals = new uint[mesh.Materials.Count];
                uint[] metallics = new uint[mesh.Materials.Count];

                for (int m = 0; m < mesh.Materials.Count; m++)
                {
                    var mat = mesh.Materials[m];

                    // Albedo
                    uint albedo = _defaultAlbedo;
                    if (mat.Textures.TryGetValue("albedo", out var texInfo))
                    {
                        string texPath = texInfo.Path;
                        if (texPath.StartsWith("embedded_"))
                        {
                            string embName = texPath.Substring(9);
                            var data = forest.EmbeddedTextures.FirstOrDefault(t => t.Name == embName).Data;
                            if (data != null)
                            {
                                (albedo, _) = TextureLoader.LoadEmbeddedTexture(_renderContext, data, embName, texInfo.WrapU, texInfo.WrapV);
                            }
                        }
                        else
                        {
                            (albedo, _) = TextureLoader.LoadTexture(_renderContext, texPath, texInfo.WrapU, texInfo.WrapV);
                        }
                    }
                    albedos[m] = albedo > 0 ? albedo : _defaultAlbedo;

                    // Normal
                    uint normalTex = _defaultNormal;
                    if (mat.Textures.TryGetValue("normal", out texInfo))
                    {
                        string texPath = texInfo.Path;
                        if (texPath.StartsWith("embedded_"))
                        {
                            string embName = texPath.Substring(9);
                            var data = forest.EmbeddedTextures.FirstOrDefault(t => t.Name == embName).Data;
                            if (data != null)
                            {
                                (normalTex, _) = TextureLoader.LoadEmbeddedTexture(_renderContext, data, embName, texInfo.WrapU, texInfo.WrapV);
                            }
                        }
                        else
                        {
                            (normalTex, _) = TextureLoader.LoadTexture(_renderContext, texPath, texInfo.WrapU, texInfo.WrapV);
                        }
                    }
                    normals[m] = normalTex > 0 ? normalTex : _defaultNormal;

                    // Metallic
                    uint metallicTex = _defaultMetallic;
                    if (mat.Textures.TryGetValue("metallic", out texInfo))
                    {
                        string texPath = texInfo.Path;
                        if (texPath.StartsWith("embedded_"))
                        {
                            string embName = texPath.Substring(9);
                            var data = forest.EmbeddedTextures.FirstOrDefault(t => t.Name == embName).Data;
                            if (data != null)
                            {
                                (metallicTex, _) = TextureLoader.LoadEmbeddedTexture(_renderContext, data, embName, texInfo.WrapU, texInfo.WrapV);
                            }
                        }
                        else
                        {
                            (metallicTex, _) = TextureLoader.LoadTexture(_renderContext, texPath, texInfo.WrapU, texInfo.WrapV);
                        }
                    }
                    metallics[m] = metallicTex > 0 ? metallicTex : _defaultMetallic;
                }

                _albedoTextures.Add(albedos);
                _normalTextures.Add(normals);
                _metallicTextures.Add(metallics);

                meshIndex++;
            }

            if (_model.Animations.Count > 0)
            {
                _currentAnimation = _model.Animations[0].Name;
            }

            // Initialize shader
            _assetShader = new ShaderProgram(_renderContext, AssetShader.VertexShaderSource, AssetShader.FragmentShaderSource);
        }

        private uint CreateDefaultTexture(byte[] rgba, int format)
        {
            uint tex;
            _renderContext.GenTextures(1, out tex);
            _renderContext.BindTexture(_renderContext.Enums.Texture2D, tex);
            fixed (byte* ptr = rgba)
            {
                _renderContext.TexImage2D(_renderContext.Enums.Texture2D, 0, format, 1, 1, 0, _renderContext.Enums.Rgba, _renderContext.Enums.UnsignedByte, ptr);
            }
            _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureMinFilter, _renderContext.Enums.Linear);
            _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureMagFilter, _renderContext.Enums.Linear);
            _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
            return tex;
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
                _yaw += deltaX * 0.1f;
                _pitch += deltaY * 0.1f;
                _pitch = Math.Clamp(_pitch, -89f, 89f);
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
                Y = MathF.Sin(MathF.PI * _pitch / 180),
                Z = MathF.Sin(MathF.PI * _yaw / 180) * MathF.Cos(MathF.PI * _pitch / 180)
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
            _assetShader.SetUniform("uLightDir", -0.707f, -0.707f, 0.707f, 0.0f);
            _assetShader.SetUniform("uLightColor", 1.0f, 1.0f, 1.0f, 0.0f);
            _assetShader.SetUniform("uLightIntensity", 1.0f);
            _assetShader.SetUniform("uViewPos", _cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z, 0.0f);
            _assetShader.SetUniform("uAmbientStrength", 0.3f);
            _assetShader.SetUniform("uSpecularStrength", 0.05f);
            _assetShader.SetUniform("uShininess", 4.0f);
            _assetShader.SetUniform("uHasBones", _model.HasSkin ? 1 : 0);

            if (_model.Skeleton != null && _model.HasSkin)
            {
                var transforms = _model.Skeleton.GetTransforms();
                _assetShader.SetMatrix4Array("uBoneTransforms", transforms);
            }

            for (int i = 0; i < _vaos.Count; i++)
            {
                // Bind textures for this mesh
                var albedos = _albedoTextures[i];
                var normals = _normalTextures[i];
                var metallics = _metallicTextures[i];

                for (int t = 0; t < Math.Min(albedos.Length, 4); t++)
                {
                    _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + t);
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, albedos[t]);
                    _assetShader.SetUniform($"uAlbedoMap[{t}]", t);
                }
                for (int t = 0; t < Math.Min(normals.Length, 4); t++)
                {
                    _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + 4 + t);
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, normals[t]);
                    _assetShader.SetUniform($"uNormalMap[{t}]", 4 + t);
                }
                for (int t = 0; t < Math.Min(metallics.Length, 4); t++)
                {
                    _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + 8 + t);
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, metallics[t]);
                    _assetShader.SetUniform($"uMetallicMap[{t}]", 8 + t);
                }

                _renderContext.BindVertexArray(_vaos[i]);
                _renderContext.DrawElements(_renderContext.Enums.Triangles, _indexCounts[i], _renderContext.Enums.UnsignedInt, null);

                int error;
                while ((error = _renderContext.GetError()) != _renderContext.Enums.NoError)
                {
                    Console.WriteLine($"AssetViewerPanel: OpenGL error after draw: {error}");
                }
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _vaos.Count; i++)
            {
                _renderContext.DeleteVertexArray(_vaos[i]);
                _renderContext.DeleteBuffer(_vbos[i]);
                _renderContext.DeleteBuffer(_ebos[i]);
                // Clean textures
                foreach (var tex in _albedoTextures[i]) if (tex != _defaultAlbedo) _renderContext.DeleteTexture(tex);
                foreach (var tex in _normalTextures[i]) if (tex != _defaultNormal) _renderContext.DeleteTexture(tex);
                foreach (var tex in _metallicTextures[i]) if (tex != _defaultMetallic) _renderContext.DeleteTexture(tex);
            }
            _renderContext.DeleteTexture(_defaultAlbedo);
            _renderContext.DeleteTexture(_defaultNormal);
            _renderContext.DeleteTexture(_defaultMetallic);
            _assetShader.Dispose();
        }

        public void Detach()
        {
            // Implement pop-out to new window
            // Create new ContextManager, new window, transfer state
        }
    }
}