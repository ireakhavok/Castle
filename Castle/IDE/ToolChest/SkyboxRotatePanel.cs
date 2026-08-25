// Folder: ToolChest
// File: SkyboxRotatePanel.cs
using Keystone;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Renderers;
using SiegeEngine.Core.GPU.Shaders;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ToolChest
{
    public unsafe class SkyboxRotatePanel : BasePanel
    {
        private class SkyboxRotateUIOverlay : UIOverlay
        {
            private readonly SkyboxRotatePanel _parent;

            public SkyboxRotateUIOverlay(SkyboxRotatePanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }

            // Match FileSelectorPanel: do NOT call base (base already fires data-hook).
            public override bool HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
                return true;
            }
        }

        private EventBus _eventBus;
        private SkyboxData _workingSkybox;
        private int _selectedFace = -1;
        private string[] _resolvedFaces = new string[6];
        private readonly int[] _faceSteps = new int[6];

        private uint _cubemapTex = 0;
        private VertexBuffer _previewCube;
        private VertexBuffer _axisBuffer;
        private VertexBuffer _faceOutlineBuffer;
        private ShaderProgram _previewShader;
        private ShaderProgram _lineShader;
        private float _previewYaw = 0.6f;
        private float _previewPitch = 0.35f;
        private float _previewDist = 3.2f;
        private bool _orbitDragging = false;
        private Vector2 _lastMouse;
        private Matrix4x4 _previewModel = Matrix4x4.Identity;

        // Whole-cube gizmo drag (axis pick: 0=X, 1=Y, 2=Z)
        private int _gizmoAxis = -1;
        private bool _gizmoDragging = false;
        private Vector2 _gizmoLast;

        public override bool WantsContinuousUpdate => true;

        public SkyboxRotatePanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            _eventBus = eventBus;
            HasTitleBar = true;
            IsClosable = true;
            IsModal = false;
            AllowDragging = true;
            DockState = DockState.Floating;
            DockingMode = DockingMode.IDE;
            Dockable = true;
            RenderOrder = 0;
            BaseWidth = 760f;
            BaseHeight = 620f;
            Size = new Vector2(760f, 620f);
            Scaling = ScalingMode.BestFit;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new SkyboxRotateUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            chrome.close_color = new Vector4(0.486f, 1.0f, 0.796f, 1.0f);

            var level = ProjectSettings.Current.CurrentLevel;
            if (level?.Skybox != null)
                _workingSkybox = CloneSkybox(level.Skybox);
            else
                _workingSkybox = new SkyboxData { Enabled = true };

            ResolveFacePaths();
            LoadPreviewCubemap();
            _previewCube = new VertexBuffer(_renderContext);
            BuildPreviewCube();
            _axisBuffer = new VertexBuffer(_renderContext);
            BuildAxisGizmo();
            _faceOutlineBuffer = new VertexBuffer(_renderContext);
            BuildFaceOutline(-1);

            string vs = @"
#version 330 core
layout(location = 0) in vec3 aPosition;
uniform mat4 uMVP;
out vec3 vDir;
void main() {
    vDir = aPosition;
    gl_Position = uMVP * vec4(aPosition, 1.0);
}";
            string fs = @"
#version 330 core
in vec3 vDir;
uniform samplerCube uSkybox;
out vec4 FragColor;
void main() {
    FragColor = texture(uSkybox, normalize(vDir));
}";
            _previewShader = new ShaderProgram(_renderContext, vs, fs);

            string lvs = @"
#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec4 aColor;
uniform mat4 uMVP;
out vec4 vColor;
void main() {
    vColor = aColor;
    gl_Position = uMVP * vec4(aPosition, 1.0);
}";
            string lfs = @"
#version 330 core
in vec4 vColor;
out vec4 FragColor;
void main() {
    FragColor = vColor;
}";
            _lineShader = new ShaderProgram(_renderContext, lvs, lfs);

            LoadUIFromFile();
            UpdateSelectionUI();
        }

        private void LoadUIFromFile()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SkyboxRotateUI.html");
            if (!File.Exists(htmlPath))
            {
                Console.WriteLine("[SkyboxRotatePanel] SkyboxRotateUI.html not found at " + htmlPath);
                return;
            }
            _uiOverlay.LoadUI(File.ReadAllText(htmlPath));
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private SkyboxData CloneSkybox(SkyboxData src)
        {
            return new SkyboxData
            {
                Enabled = src.Enabled,
                Type = src.Type,
                CubemapPath = src.CubemapPath ?? "",
                Faces = src.Faces != null ? new List<string>(src.Faces) : new List<string>(),
                RotationSpeed = src.RotationSpeed,
                Intensity = src.Intensity
            };
        }

        private void ResolveFacePaths()
        {
            string projectPath = ProjectSettings.Current.ActiveProject ?? "";
            for (int i = 0; i < 6; i++)
            {
                string f = (_workingSkybox.Faces != null && i < _workingSkybox.Faces.Count) ? _workingSkybox.Faces[i] ?? "" : "";
                if (!string.IsNullOrEmpty(f) && !Path.IsPathRooted(f) && !string.IsNullOrEmpty(projectPath))
                    f = Path.GetFullPath(Path.Combine(projectPath, f));
                _resolvedFaces[i] = f;
            }
        }

        private void LoadPreviewCubemap()
        {
            if (_cubemapTex != 0)
            {
                _renderContext.DeleteTexture(_cubemapTex);
                _cubemapTex = 0;
            }
            if (_workingSkybox == null || !_workingSkybox.Enabled) return;

            _renderContext.GenTextures(1, out _cubemapTex);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, _cubemapTex);
            _renderContext.TexParameter(_renderContext.Enums.TextureCubeMap, _renderContext.Enums.TextureMinFilter, _renderContext.Enums.Linear);
            _renderContext.TexParameter(_renderContext.Enums.TextureCubeMap, _renderContext.Enums.TextureMagFilter, _renderContext.Enums.Linear);
            _renderContext.TexParameter(_renderContext.Enums.TextureCubeMap, _renderContext.Enums.TextureWrapS, _renderContext.Enums.ClampToEdge);
            _renderContext.TexParameter(_renderContext.Enums.TextureCubeMap, _renderContext.Enums.TextureWrapT, _renderContext.Enums.ClampToEdge);
            _renderContext.TexParameter(_renderContext.Enums.TextureCubeMap, _renderContext.Enums.TextureWrapR, _renderContext.Enums.ClampToEdge);

            for (int i = 0; i < 6; i++)
                UploadFace(i, 0);

            _renderContext.GenerateMipmap(_renderContext.Enums.TextureCubeMap);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, 0);
        }

        private void UploadFace(int faceIndex, int step)
        {
            string path = _resolvedFaces[faceIndex];
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            RotateFlipType flip = (step & 3) switch
            {
                1 => RotateFlipType.Rotate90FlipNone,
                2 => RotateFlipType.Rotate180FlipNone,
                3 => RotateFlipType.Rotate270FlipNone,
                _ => RotateFlipType.RotateNoneFlipNone
            };

            using (var bmp = new Bitmap(path))
            {
                if (flip != RotateFlipType.RotateNoneFlipNone)
                    bmp.RotateFlip(flip);

                var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int dataSize = bmp.Width * bmp.Height * 4;
                byte[] pixelData = new byte[dataSize];
                Marshal.Copy(data.Scan0, pixelData, 0, dataSize);
                bmp.UnlockBits(data);
                fixed (byte* ptr = pixelData)
                {
                    _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, _cubemapTex);
                    _renderContext.TexImage2D((int)(_renderContext.Enums.TextureCubeMapPositiveX + faceIndex), 0,
                        _renderContext.Enums.InternalRgba, (uint)bmp.Width, (uint)bmp.Height, 0,
                        _renderContext.Enums.PixelBgra, _renderContext.Enums.UnsignedByte, ptr);
                }
            }
        }

        private void RotateSelectedFace(int deltaSteps)
        {
            if (_selectedFace < 0 || _selectedFace > 5) return;
            int step = _faceSteps[_selectedFace] + deltaSteps;
            while (step < 0) step += 4;
            _faceSteps[_selectedFace] = step % 4;
            UploadFace(_selectedFace, _faceSteps[_selectedFace]);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, _cubemapTex);
            _renderContext.GenerateMipmap(_renderContext.Enums.TextureCubeMap);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, 0);
            Console.WriteLine($"[SkyboxRotatePanel] Face {_selectedFace} → {_faceSteps[_selectedFace] * 90}°");
        }

        private void BuildPreviewCube()
        {
            float s = 1.0f;
            var vertices = new List<float>();
            var indices = new List<uint>();
            vertices.AddRange(new float[] { -s, -s, -s, 1, 1, 1, 1, 0, 0 });
            vertices.AddRange(new float[] { s, -s, -s, 1, 1, 1, 1, 1, 0 });
            vertices.AddRange(new float[] { s, s, -s, 1, 1, 1, 1, 1, 1 });
            vertices.AddRange(new float[] { -s, s, -s, 1, 1, 1, 1, 0, 1 });
            vertices.AddRange(new float[] { -s, -s, s, 1, 1, 1, 1, 0, 0 });
            vertices.AddRange(new float[] { s, -s, s, 1, 1, 1, 1, 1, 0 });
            vertices.AddRange(new float[] { s, s, s, 1, 1, 1, 1, 1, 1 });
            vertices.AddRange(new float[] { -s, s, s, 1, 1, 1, 1, 0, 1 });
            indices.AddRange(new uint[] { 0, 1, 2, 2, 3, 0 });
            indices.AddRange(new uint[] { 4, 5, 6, 6, 7, 4 });
            indices.AddRange(new uint[] { 0, 4, 7, 7, 3, 0 });
            indices.AddRange(new uint[] { 1, 5, 6, 6, 2, 1 });
            indices.AddRange(new uint[] { 3, 2, 6, 6, 7, 3 });
            indices.AddRange(new uint[] { 0, 1, 5, 5, 4, 0 });
            _previewCube.UpdateCustomWithUV(vertices, indices);
        }

        // RGB axis lines from center: +X red, +Y green, +Z blue (and faint negatives)
        private void BuildAxisGizmo()
        {
            float L = 1.6f;
            var verts = new List<Vertex>();
            var idx = new List<uint>();
            // +X red
            verts.Add(new Vertex(0, 0, 0, 1, 0.15f, 0.15f, 1));
            verts.Add(new Vertex(L, 0, 0, 1, 0.15f, 0.15f, 1));
            idx.Add(0); idx.Add(1);
            // +Y green
            verts.Add(new Vertex(0, 0, 0, 0.15f, 1, 0.15f, 1));
            verts.Add(new Vertex(0, L, 0, 0.15f, 1, 0.15f, 1));
            idx.Add(2); idx.Add(3);
            // +Z blue
            verts.Add(new Vertex(0, 0, 0, 0.2f, 0.4f, 1, 1));
            verts.Add(new Vertex(0, 0, L, 0.2f, 0.4f, 1, 1));
            idx.Add(4); idx.Add(5);
            // faint negatives
            verts.Add(new Vertex(0, 0, 0, 0.5f, 0.1f, 0.1f, 0.4f));
            verts.Add(new Vertex(-L, 0, 0, 0.5f, 0.1f, 0.1f, 0.4f));
            idx.Add(6); idx.Add(7);
            verts.Add(new Vertex(0, 0, 0, 0.1f, 0.5f, 0.1f, 0.4f));
            verts.Add(new Vertex(0, -L, 0, 0.1f, 0.5f, 0.1f, 0.4f));
            idx.Add(8); idx.Add(9);
            verts.Add(new Vertex(0, 0, 0, 0.1f, 0.2f, 0.5f, 0.4f));
            verts.Add(new Vertex(0, 0, -L, 0.1f, 0.2f, 0.5f, 0.4f));
            idx.Add(10); idx.Add(11);
            _axisBuffer.UpdateCustom(verts, idx);
        }

        // Thick outline of one face of the unit cube for selection feedback
        private void BuildFaceOutline(int face)
        {
            float s = 1.02f;
            var verts = new List<Vertex>();
            var idx = new List<uint>();
            Vector4 c = new Vector4(1f, 0.85f, 0.1f, 1f); // gold highlight

            Vector3[] corners = face switch
            {
                0 => new[] { new Vector3(s, -s, -s), new Vector3(s, s, -s), new Vector3(s, s, s), new Vector3(s, -s, s) },   // +X
                1 => new[] { new Vector3(-s, -s, -s), new Vector3(-s, -s, s), new Vector3(-s, s, s), new Vector3(-s, s, -s) }, // -X
                2 => new[] { new Vector3(-s, s, -s), new Vector3(s, s, -s), new Vector3(s, s, s), new Vector3(-s, s, s) },     // +Y
                3 => new[] { new Vector3(-s, -s, -s), new Vector3(-s, -s, s), new Vector3(s, -s, s), new Vector3(s, -s, -s) }, // -Y
                4 => new[] { new Vector3(-s, -s, s), new Vector3(s, -s, s), new Vector3(s, s, s), new Vector3(-s, s, s) },     // +Z
                5 => new[] { new Vector3(-s, -s, -s), new Vector3(-s, s, -s), new Vector3(s, s, -s), new Vector3(s, -s, -s) }, // -Z
                _ => Array.Empty<Vector3>()
            };

            if (corners.Length == 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    verts.Add(new Vertex(corners[i].X, corners[i].Y, corners[i].Z, c.X, c.Y, c.Z, c.W));
                    idx.Add((uint)i);
                    idx.Add((uint)((i + 1) % 4));
                }
            }
            _faceOutlineBuffer.UpdateCustom(verts, idx);
        }

        private void UpdateSelectionUI()
        {
            for (int i = 0; i < 6; i++)
            {
                var btn = _uiOverlay.FindElementById("face" + i);
                if (btn == null) continue;
                if (_selectedFace == i) btn.Attributes["class"] = "selected";
                else btn.Attributes.Remove("class");
            }
            var whole = _uiOverlay.FindElementById("faceWhole");
            if (whole != null)
            {
                if (_selectedFace < 0) whole.Attributes["class"] = "selected";
                else whole.Attributes.Remove("class");
            }

            var modeSpan = _uiOverlay.FindElementById("modeLabel");
            if (modeSpan != null)
            {
                string text = _selectedFace < 0 ? "Whole Cube (drag axes)" : $"Face {_selectedFace} ({FaceName(_selectedFace)})";
                foreach (var child in modeSpan.Children)
                {
                    if (child is TextElement te) { te.Content = text; break; }
                }
            }

            BuildFaceOutline(_selectedFace);
            _uiOverlay.RefreshUI();
        }

        private static string FaceName(int i) => i switch { 0 => "+X", 1 => "-X", 2 => "+Y", 3 => "-Y", 4 => "+Z", 5 => "-Z", _ => "?" };

        public void HandleUIClick(HtmlElement elem)
        {
            if (elem == null) return;
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (!string.IsNullOrEmpty(hook))
                HandleDataHook(hook);
        }

        public void HandleDataHook(string hook)
        {
            if (hook == "ClosePanel")
            {
                _eventBus.Publish(new ClosePanelEvent(this));
                return;
            }

            if (hook == "SelectWhole")
            {
                _selectedFace = -1;
                UpdateSelectionUI();
                return;
            }
            if (hook.StartsWith("SelectFace") && int.TryParse(hook.AsSpan(10), out int idx) && idx >= 0 && idx < 6)
            {
                _selectedFace = idx;
                UpdateSelectionUI();
                return;
            }

            if (hook == "FaceRotCW") { RotateSelectedFace(+1); return; }
            if (hook == "FaceRotCCW") { RotateSelectedFace(-1); return; }
            if (hook == "FaceRot180") { RotateSelectedFace(+2); return; }
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);

            float header = HasTitleBar ? HeaderHeight : 0f;
            Vector2 rel = absMousePos - Position;
            bool inPreview = rel.Y > header && rel.Y < Size.Y - 140f && rel.X > 0 && rel.X < Size.X;

            if (inPreview && mousePressed)
            {
                // Pick axis for whole-cube rotation when in whole mode
                if (_selectedFace < 0)
                {
                    int axis = PickAxis(rel);
                    if (axis >= 0)
                    {
                        _gizmoAxis = axis;
                        _gizmoDragging = true;
                        _gizmoLast = rel;
                    }
                    else
                    {
                        _orbitDragging = true;
                        _lastMouse = rel;
                    }
                }
                else
                {
                    _orbitDragging = true;
                    _lastMouse = rel;
                }
            }

            if (_gizmoDragging && mouseDown)
            {
                Vector2 delta = rel - _gizmoLast;
                float amount = (delta.X) * 0.012f;
                Matrix4x4 rot = _gizmoAxis switch
                {
                    0 => Matrix4x4.CreateRotationX(amount),
                    1 => Matrix4x4.CreateRotationY(amount),
                    2 => Matrix4x4.CreateRotationZ(amount),
                    _ => Matrix4x4.Identity
                };
                _previewModel = rot * _previewModel;
                _gizmoLast = rel;
            }
            else if (_orbitDragging && mouseDown)
            {
                Vector2 delta = rel - _lastMouse;
                _previewYaw += delta.X * 0.01f;
                _previewPitch = Math.Clamp(_previewPitch + delta.Y * 0.01f, -1.4f, 1.4f);
                _lastMouse = rel;
            }

            if (mouseReleased)
            {
                _gizmoDragging = false;
                _gizmoAxis = -1;
                _orbitDragging = false;
            }

            if (inPreview && MathF.Abs(scrollDelta) > 0.01f)
                _previewDist = Math.Clamp(_previewDist - scrollDelta * 0.3f, 1.8f, 8f);
        }

        // Screen-space pick of the projected axis tips
        private int PickAxis(Vector2 relMouse)
        {
            float header = HasTitleBar ? HeaderHeight : 0f;
            float contentW = Size.X;
            float contentH = Size.Y - header;
            float aspect = contentW / Math.Max(contentH, 1f);

            Vector3 camPos = new Vector3(
                MathF.Sin(_previewYaw) * MathF.Cos(_previewPitch) * _previewDist,
               -MathF.Cos(_previewYaw) * MathF.Cos(_previewPitch) * _previewDist,
                MathF.Sin(_previewPitch) * _previewDist
            );
            Matrix4x4 view = Matrix4x4.CreateLookAt(camPos, Vector3.Zero, Vector3.UnitZ);
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3.2f, aspect, 0.1f, 50f);
            Matrix4x4 mvp = _previewModel * view * proj;

            Vector3[] tips = {
                new Vector3(1.6f, 0, 0),
                new Vector3(0, 1.6f, 0),
                new Vector3(0, 0, 1.6f)
            };

            float bestDist = 18f;
            int best = -1;
            for (int i = 0; i < 3; i++)
            {
                Vector4 clip = Vector4.Transform(new Vector4(tips[i], 1f), mvp);
                if (clip.W <= 0.001f) continue;
                float ndcX = clip.X / clip.W;
                float ndcY = clip.Y / clip.W;
                float sx = (ndcX * 0.5f + 0.5f) * contentW;
                float sy = (1f - (ndcY * 0.5f + 0.5f)) * contentH + header;
                float d = Vector2.Distance(relMouse, new Vector2(sx, sy));
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        protected override void RenderInnerContent()
        {
            if (_cubemapTex == 0 || _previewCube == null || _previewShader == null)
                return;

            float contentW = Size.X;
            float contentH = Size.Y - (HasTitleBar ? HeaderHeight : 0f);
            float aspect = contentW / Math.Max(contentH, 1f);

            Vector3 camPos = new Vector3(
                MathF.Sin(_previewYaw) * MathF.Cos(_previewPitch) * _previewDist,
               -MathF.Cos(_previewYaw) * MathF.Cos(_previewPitch) * _previewDist,
                MathF.Sin(_previewPitch) * _previewDist
            );
            Matrix4x4 view = Matrix4x4.CreateLookAt(camPos, Vector3.Zero, Vector3.UnitZ);
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3.2f, aspect, 0.1f, 50f);
            Matrix4x4 mvp = _previewModel * view * proj;

            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.Disable(_renderContext.Enums.CullFace);
            _renderContext.Clear(_renderContext.Enums.DepthBufferBit);

            // Cubemap cube
            _previewShader.Use();
            _previewShader.SetMatrix4("uMVP", mvp);
            _renderContext.ActiveTexture(0);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, _cubemapTex);
            _previewCube.Bind();
            _renderContext.DrawElements(_renderContext.Enums.Triangles, _previewCube.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);

            // Axis gizmo + selected face outline
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _lineShader.Use();
            _lineShader.SetMatrix4("uMVP", mvp);

            if (_axisBuffer != null)
            {
                _axisBuffer.Bind();
                _renderContext.DrawElements(_renderContext.Enums.Lines, _axisBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            }

            if (_selectedFace >= 0 && _faceOutlineBuffer != null && _faceOutlineBuffer.GetIndexCount() > 0)
            {
                _faceOutlineBuffer.Bind();
                _renderContext.DrawElements(_renderContext.Enums.Lines, _faceOutlineBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            }

            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }

        public override void Dispose()
        {
            if (_cubemapTex != 0) _renderContext.DeleteTexture(_cubemapTex);
            _previewCube?.Dispose();
            _axisBuffer?.Dispose();
            _faceOutlineBuffer?.Dispose();
            _previewShader?.Dispose();
            _lineShader?.Dispose();
            base.Dispose();
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new SkyboxRotatePanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}