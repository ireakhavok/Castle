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
            public override bool HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
                return true;
            }
            protected override void HandleDataHook(string hook)
            {
                _parent.HandleDataHook(hook);
            }
        }

        private EventBus _eventBus;
        private SkyboxData _workingSkybox;
        private int _selectedFace = -1;
        private string[] _resolvedFaces = new string[6];
        private readonly int[] _faceSteps = new int[6];
        private readonly bool[] _faceFlipH = new bool[6];
        private readonly bool[] _faceFlipV = new bool[6];
        private bool _swapMode = false;

        private uint _cubemapTex = 0;
        private VertexBuffer _previewCube;
        private VertexBuffer _axisBuffer;
        private VertexBuffer _ringBuffer;
        private VertexBuffer _faceOutlineBuffer;
        private ShaderProgram _previewShader;
        private ShaderProgram _lineShader;

        private float _previewYaw = 0.6f;
        private float _previewPitch = 0.35f;
        private float _previewDist = 2.8f;
        private bool _orbitDragging = false;
        private Vector2 _lastMouse;

        private bool _ringDragging = false;
        private int _activeRing = -1;
        private Quaternion _dragStartOrient = Quaternion.Identity;
        private float _accumAngle = 0f;
        private Vector3 _lastPlanePoint;

        private const float RingPickTolerance = 18f;

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

            EnsureFacesList();
            ResolveFacePaths();
            LoadPreviewCubemap();

            _previewCube = new VertexBuffer(_renderContext);
            BuildPreviewCube();
            _axisBuffer = new VertexBuffer(_renderContext);
            _ringBuffer = new VertexBuffer(_renderContext);
            _faceOutlineBuffer = new VertexBuffer(_renderContext);
            RebuildAxesAndRings();
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
            SyncSliderFromData();
            UpdateSelectionUI();
        }

        private void LoadUIFromFile()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SkyboxRotateUI.html");
            if (!File.Exists(htmlPath))
                return;
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
                Intensity = src.Intensity,
                VerticalOffset = src.VerticalOffset,
                Orientation = src.Orientation
            };
        }

        private void EnsureFacesList()
        {
            if (_workingSkybox.Faces == null)
                _workingSkybox.Faces = new List<string>();
            while (_workingSkybox.Faces.Count < 6)
                _workingSkybox.Faces.Add("");
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
                UploadFace(i);

            _renderContext.GenerateMipmap(_renderContext.Enums.TextureCubeMap);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, 0);
        }

        private void ApplyOrientationToBitmap(Bitmap bmp, int step, bool flipH, bool flipV)
        {
            RotateFlipType rot = (step & 3) switch
            {
                1 => RotateFlipType.Rotate90FlipNone,
                2 => RotateFlipType.Rotate180FlipNone,
                3 => RotateFlipType.Rotate270FlipNone,
                _ => RotateFlipType.RotateNoneFlipNone
            };
            if (rot != RotateFlipType.RotateNoneFlipNone)
                bmp.RotateFlip(rot);
            if (flipH)
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
            if (flipV)
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
        }

        private void UploadFace(int faceIndex)
        {
            string path = _resolvedFaces[faceIndex];
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            using (var bmp = new Bitmap(path))
            {
                ApplyOrientationToBitmap(bmp, _faceSteps[faceIndex], _faceFlipH[faceIndex], _faceFlipV[faceIndex]);

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
            UploadFace(_selectedFace);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, _cubemapTex);
            _renderContext.GenerateMipmap(_renderContext.Enums.TextureCubeMap);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, 0);
        }

        private void MirrorSelectedFace(bool horizontal)
        {
            if (_selectedFace < 0 || _selectedFace > 5) return;
            if (horizontal)
                _faceFlipH[_selectedFace] = !_faceFlipH[_selectedFace];
            else
                _faceFlipV[_selectedFace] = !_faceFlipV[_selectedFace];
            UploadFace(_selectedFace);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, _cubemapTex);
            _renderContext.GenerateMipmap(_renderContext.Enums.TextureCubeMap);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, 0);
            UpdateSelectionUI();
        }

        private void SwapFaces(int a, int b)
        {
            if (a < 0 || a > 5 || b < 0 || b > 5 || a == b) return;

            string tmpPath = _resolvedFaces[a];
            _resolvedFaces[a] = _resolvedFaces[b];
            _resolvedFaces[b] = tmpPath;

            EnsureFacesList();
            string tmpFace = _workingSkybox.Faces[a];
            _workingSkybox.Faces[a] = _workingSkybox.Faces[b];
            _workingSkybox.Faces[b] = tmpFace;

            int tmpStep = _faceSteps[a];
            _faceSteps[a] = _faceSteps[b];
            _faceSteps[b] = tmpStep;

            bool tmpH = _faceFlipH[a];
            _faceFlipH[a] = _faceFlipH[b];
            _faceFlipH[b] = tmpH;

            bool tmpV = _faceFlipV[a];
            _faceFlipV[a] = _faceFlipV[b];
            _faceFlipV[b] = tmpV;

            UploadFace(a);
            UploadFace(b);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, _cubemapTex);
            _renderContext.GenerateMipmap(_renderContext.Enums.TextureCubeMap);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, 0);
        }

        private static void AtomicWriteBitmap(string targetPath, Bitmap bmp)
        {
            string dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            string temp = targetPath + ".tmp";
            bmp.Save(temp, ImageFormat.Png);
            if (File.Exists(targetPath))
                File.Delete(targetPath);
            File.Move(temp, targetPath);
        }

        private void Apply()
        {
            for (int i = 0; i < 6; i++)
            {
                if (_faceSteps[i] == 0 && !_faceFlipH[i] && !_faceFlipV[i])
                    continue;

                string path = _resolvedFaces[i];
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    continue;

                using (var bmp = new Bitmap(path))
                {
                    ApplyOrientationToBitmap(bmp, _faceSteps[i], _faceFlipH[i], _faceFlipV[i]);
                    AtomicWriteBitmap(path, bmp);
                }

                _faceSteps[i] = 0;
                _faceFlipH[i] = false;
                _faceFlipV[i] = false;
            }

            PushSkyboxLive();
            LoadPreviewCubemap();
            UpdateSelectionUI();
        }

        private void PushSkyboxLive()
        {
            var level = ProjectSettings.Current.CurrentLevel;
            if (level != null)
                level.Skybox = _workingSkybox;

            string sceneName = ProjectSettings.Current.CurrentSceneName;
            if (!string.IsNullOrEmpty(sceneName))
            {
                var live = ProjectStateManager.Current.GetLiveState(sceneName);
                if (live != null)
                {
                    live.Skybox = _workingSkybox;
                    live.SyncSkyboxIfNeeded();
                }
            }

            if (_eventBus != null)
                _eventBus.Publish(new GenericEvent { Hook = "SkyboxRefresh" });
        }

        private void SyncSliderFromData()
        {
            var slider = _uiOverlay.FindElementById("heightSlider") as RangeElement;
            if (slider != null)
            {
                slider.Value = _workingSkybox.VerticalOffset;
                slider.Attributes["value"] = _workingSkybox.VerticalOffset.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            UpdateHeightLabel();
        }

        private void UpdateHeightLabel()
        {
            var span = _uiOverlay.FindElementById("heightValue");
            if (span == null) return;
            string text = ((int)Math.Round(_workingSkybox.VerticalOffset)).ToString();
            foreach (var child in span.Children)
            {
                if (child is TextElement te) { te.Content = text; break; }
            }
        }

        private void BuildPreviewCube()
        {
            float s = 0.7f;
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

        private void RebuildAxesAndRings()
        {
            var aVerts = new List<Vertex>();
            var aIdx = new List<uint>();
            float len = 1.1f;
            aVerts.Add(new Vertex(0, 0, 0, 1, 0.15f, 0.15f, 1));
            aVerts.Add(new Vertex(len, 0, 0, 1, 0.15f, 0.15f, 1));
            aIdx.Add(0); aIdx.Add(1);
            aVerts.Add(new Vertex(0, 0, 0, 0.15f, 1, 0.15f, 1));
            aVerts.Add(new Vertex(0, len, 0, 0.15f, 1, 0.15f, 1));
            aIdx.Add(2); aIdx.Add(3);
            aVerts.Add(new Vertex(0, 0, 0, 0.2f, 0.4f, 1, 1));
            aVerts.Add(new Vertex(0, 0, len, 0.2f, 0.4f, 1, 1));
            aIdx.Add(4); aIdx.Add(5);
            _axisBuffer.UpdateCustom(aVerts, aIdx);

            var rVerts = new List<Vertex>();
            var rIdx = new List<uint>();
            AddRing(rVerts, rIdx, Vector3.UnitX, new Vector4(1f, 0.2f, 0.2f, 1f));
            AddRing(rVerts, rIdx, Vector3.UnitY, new Vector4(0.2f, 1f, 0.2f, 1f));
            AddRing(rVerts, rIdx, Vector3.UnitZ, new Vector4(0.2f, 0.4f, 1f, 1f));
            _ringBuffer.UpdateCustom(rVerts, rIdx);
        }

        private void AddRing(List<Vertex> vertices, List<uint> indices, Vector3 axis, Vector4 color)
        {
            uint baseIndex = (uint)vertices.Count;
            int segments = 48;
            float radius = 1.05f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * MathF.PI * 2f / segments;
                float x = MathF.Cos(angle) * radius;
                float y = MathF.Sin(angle) * radius;
                Vector3 point;
                if (axis == Vector3.UnitX) point = new Vector3(0, x, y);
                else if (axis == Vector3.UnitY) point = new Vector3(x, 0, y);
                else point = new Vector3(x, y, 0);
                vertices.Add(new Vertex(point.X, point.Y, point.Z, color.X, color.Y, color.Z, color.W));
            }
            for (int i = 0; i < segments; i++)
            {
                uint current = baseIndex + (uint)i;
                uint next = baseIndex + (uint)((i + 1) % segments);
                indices.Add(current);
                indices.Add(next);
            }
        }

        private void BuildFaceOutline(int face)
        {
            float s = 0.72f;
            var verts = new List<Vertex>();
            var idx = new List<uint>();
            Vector4 c = new Vector4(1f, 0.85f, 0.1f, 1f);
            Vector3[] corners = face switch
            {
                0 => new[] { new Vector3(s, -s, -s), new Vector3(s, s, -s), new Vector3(s, s, s), new Vector3(s, -s, s) },
                1 => new[] { new Vector3(-s, -s, -s), new Vector3(-s, -s, s), new Vector3(-s, s, s), new Vector3(-s, s, -s) },
                2 => new[] { new Vector3(-s, s, -s), new Vector3(s, s, -s), new Vector3(s, s, s), new Vector3(-s, s, s) },
                3 => new[] { new Vector3(-s, -s, -s), new Vector3(-s, -s, s), new Vector3(s, -s, s), new Vector3(s, -s, -s) },
                4 => new[] { new Vector3(-s, -s, s), new Vector3(s, -s, s), new Vector3(s, s, s), new Vector3(-s, s, s) },
                5 => new[] { new Vector3(-s, -s, -s), new Vector3(-s, s, -s), new Vector3(s, s, -s), new Vector3(s, -s, -s) },
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
                string text;
                if (_swapMode)
                    text = "Swap: pick target face";
                else if (_selectedFace < 0)
                    text = "Whole Cube (gizmo rings)";
                else
                {
                    string flips = "";
                    if (_faceFlipH[_selectedFace]) flips += " H";
                    if (_faceFlipV[_selectedFace]) flips += " V";
                    string rot = _faceSteps[_selectedFace] > 0 ? $" {_faceSteps[_selectedFace] * 90}°" : "";
                    text = $"Face {_selectedFace} ({FaceName(_selectedFace)}){rot}{flips}";
                }
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
            if (hook == "Apply")
            {
                Apply();
                return;
            }
            // Height is handled by the continuous poll in Update – no data-hook needed
            if (hook == "SelectWhole")
            {
                _selectedFace = -1;
                _swapMode = false;
                UpdateSelectionUI();
                return;
            }
            if (hook.StartsWith("SelectFace") && int.TryParse(hook.AsSpan(10), out int idx) && idx >= 0 && idx < 6)
            {
                if (_swapMode && _selectedFace >= 0 && _selectedFace != idx)
                {
                    SwapFaces(_selectedFace, idx);
                    _swapMode = false;
                    UpdateSelectionUI();
                    return;
                }
                _selectedFace = idx;
                _swapMode = false;
                UpdateSelectionUI();
                return;
            }
            if (hook == "FaceRotCW") { RotateSelectedFace(+1); UpdateSelectionUI(); return; }
            if (hook == "FaceRotCCW") { RotateSelectedFace(-1); UpdateSelectionUI(); return; }
            if (hook == "FaceRot180") { RotateSelectedFace(+2); UpdateSelectionUI(); return; }
            if (hook == "MirrorH") { MirrorSelectedFace(true); return; }
            if (hook == "MirrorV") { MirrorSelectedFace(false); return; }
            if (hook == "StartSwap")
            {
                if (_selectedFace >= 0)
                {
                    _swapMode = true;
                    UpdateSelectionUI();
                }
                return;
            }
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);

            // Pure AnimationTimeline-style poll – this is the only reliable way
            var slider = _uiOverlay.FindElementById("heightSlider") as RangeElement;
            if (slider != null && Math.Abs(slider.Value - _workingSkybox.VerticalOffset) > 0.5f)
            {
                _workingSkybox.VerticalOffset = slider.Value;
                UpdateHeightLabel();
                PushSkyboxLive();
            }

            float header = HasTitleBar ? HeaderHeight : 0f;
            Vector2 rel = absMousePos - Position;
            bool inPreview = rel.Y > header && rel.Y < Size.Y - 140f && rel.X > 0 && rel.X < Size.X;

            if (inPreview && mousePressed)
            {
                int ring = PickRing(rel);
                if (ring >= 0)
                {
                    _activeRing = ring;
                    _dragStartOrient = _workingSkybox.Orientation;
                    _accumAngle = 0f;
                    var (o, d, ok) = GetPreviewRay(rel);
                    if (ok)
                    {
                        Vector3 localAxis = GetAxisVector(ring);
                        Vector3 worldAxis = Vector3.Transform(localAxis, Matrix4x4.CreateFromQuaternion(_dragStartOrient));
                        _lastPlanePoint = ClosestPointOnPlane(o, d, Vector3.Zero, worldAxis);
                    }
                    _ringDragging = true;
                }
                else
                {
                    _orbitDragging = true;
                    _lastMouse = rel;
                }
            }

            if (_ringDragging && mouseDown)
            {
                var (o, d, ok) = GetPreviewRay(rel);
                if (ok)
                {
                    Vector3 localAxis = GetAxisVector(_activeRing);
                    Vector3 worldAxis = Vector3.Transform(localAxis, Matrix4x4.CreateFromQuaternion(_dragStartOrient));
                    Vector3 cur = ClosestPointOnPlane(o, d, Vector3.Zero, worldAxis);
                    Vector3 v1 = Vector3.Normalize(_lastPlanePoint);
                    Vector3 v2 = Vector3.Normalize(cur);
                    float dot = Math.Clamp(Vector3.Dot(v1, v2), -1f, 1f);
                    float ang = MathF.Acos(dot);
                    if (Vector3.Dot(Vector3.Cross(v1, v2), worldAxis) < 0) ang = -ang;
                    _accumAngle += ang;
                    _lastPlanePoint = cur;

                    Quaternion delta = Quaternion.CreateFromAxisAngle(localAxis, _accumAngle);
                    _workingSkybox.Orientation = Quaternion.Normalize(delta * _dragStartOrient);
                }
            }

            if (_ringDragging && mouseReleased)
            {
                float snap = MathF.Round(_accumAngle / (MathF.PI * 0.5f)) * (MathF.PI * 0.5f);
                Vector3 localAxis = GetAxisVector(_activeRing);
                Quaternion delta = Quaternion.CreateFromAxisAngle(localAxis, snap);
                _workingSkybox.Orientation = Quaternion.Normalize(delta * _dragStartOrient);
                PushSkyboxLive();

                _ringDragging = false;
                _activeRing = -1;
            }

            if (_orbitDragging && mouseDown)
            {
                Vector2 delta = rel - _lastMouse;
                _previewYaw += delta.X * 0.012f;
                _previewPitch = Math.Clamp(_previewPitch + delta.Y * 0.012f, -1.4f, 1.4f);
                _lastMouse = rel;
            }

            if (mouseReleased)
            {
                _orbitDragging = false;
            }

            if (inPreview && MathF.Abs(scrollDelta) > 0.01f)
                _previewDist = Math.Clamp(_previewDist - scrollDelta * 0.25f, 1.6f, 7f);
        }

        private (Vector3 origin, Vector3 dir, bool ok) GetPreviewRay(Vector2 relMouse)
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
            if (!Matrix4x4.Invert(proj, out Matrix4x4 invProj)) return (Vector3.Zero, Vector3.Zero, false);
            if (!Matrix4x4.Invert(view, out Matrix4x4 invView)) return (Vector3.Zero, Vector3.Zero, false);
            float ndcX = (relMouse.X / contentW) * 2f - 1f;
            float ndcY = 1f - ((relMouse.Y - header) / contentH) * 2f;
            Vector4 nearH = Vector4.Transform(new Vector4(ndcX, ndcY, -1f, 1f), invProj);
            Vector4 farH = Vector4.Transform(new Vector4(ndcX, ndcY, 1f, 1f), invProj);
            Vector3 near = new Vector3(nearH.X / nearH.W, nearH.Y / nearH.W, nearH.Z / nearH.W);
            Vector3 far = new Vector3(farH.X / farH.W, farH.Y / farH.W, farH.Z / farH.W);
            Vector3 origin = Vector3.Transform(near, invView);
            Vector3 dir = Vector3.Normalize(Vector3.Transform(far, invView) - origin);
            return (origin, dir, true);
        }

        private int PickRing(Vector2 relMouse)
        {
            float header = HasTitleBar ? HeaderHeight : 0f;
            float contentW = Size.X;
            float contentH = Size.Y - header;
            // Use the same inverse orientation that the preview uses so picking matches what the user sees
            Matrix4x4 orient = Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(_workingSkybox.Orientation));

            float best = float.MaxValue;
            int bestRing = -1;
            for (int ring = 0; ring < 3; ring++)
            {
                int segs = 48;
                float radius = 1.05f;
                Vector3 prev = Vector3.Zero;
                for (int i = 0; i <= segs; i++)
                {
                    float ang = i * MathF.PI * 2f / segs;
                    float x = MathF.Cos(ang) * radius;
                    float y = MathF.Sin(ang) * radius;
                    Vector3 local;
                    if (ring == 0) local = new Vector3(0, x, y);
                    else if (ring == 1) local = new Vector3(x, 0, y);
                    else local = new Vector3(x, y, 0);
                    Vector3 world = Vector3.Transform(local, orient);
                    if (i > 0)
                    {
                        float d = DistanceToSegment2D(relMouse, prev, world, contentW, contentH, header);
                        if (d < best) { best = d; bestRing = ring; }
                    }
                    prev = world;
                }
            }
            return best < RingPickTolerance ? bestRing : -1;
        }

        private float DistanceToSegment2D(Vector2 p, Vector3 a3, Vector3 b3, float vw, float vh, float header)
        {
            Vector2 a = WorldToScreen(a3, vw, vh, header);
            Vector2 b = WorldToScreen(b3, vw, vh, header);
            Vector2 ab = b - a;
            float len2 = ab.LengthSquared();
            if (len2 < 1e-8f) return Vector2.Distance(p, a);
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / len2, 0f, 1f);
            return Vector2.Distance(p, a + ab * t);
        }

        private Vector2 WorldToScreen(Vector3 world, float vw, float vh, float header)
        {
            Vector3 camPos = new Vector3(
                MathF.Sin(_previewYaw) * MathF.Cos(_previewPitch) * _previewDist,
               -MathF.Cos(_previewYaw) * MathF.Cos(_previewPitch) * _previewDist,
                MathF.Sin(_previewPitch) * _previewDist
            );
            Matrix4x4 view = Matrix4x4.CreateLookAt(camPos, Vector3.Zero, Vector3.UnitZ);
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3.2f, vw / Math.Max(vh, 1f), 0.1f, 50f);
            Vector4 clip = Vector4.Transform(new Vector4(world, 1f), view * proj);
            if (Math.Abs(clip.W) < 1e-6f) return new Vector2(vw * 0.5f, vh * 0.5f + header);
            float ndcX = clip.X / clip.W;
            float ndcY = clip.Y / clip.W;
            return new Vector2((ndcX * 0.5f + 0.5f) * vw, (1f - (ndcY * 0.5f + 0.5f)) * vh + header);
        }

        private Vector3 ClosestPointOnPlane(Vector3 rayO, Vector3 rayD, Vector3 center, Vector3 normal)
        {
            float denom = Vector3.Dot(rayD, normal);
            if (Math.Abs(denom) < 1e-6f) return center;
            float t = Vector3.Dot(center - rayO, normal) / denom;
            return rayO + t * rayD;
        }

        private Vector3 GetAxisVector(int axis) => axis switch { 0 => Vector3.UnitX, 1 => Vector3.UnitY, 2 => Vector3.UnitZ, _ => Vector3.UnitZ };

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

            // Inverse Orientation so the panel matches the main-scene sample-direction rotation
            Matrix4x4 orient = Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(_workingSkybox.Orientation));
            Matrix4x4 mvp = orient * view * proj;

            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.Disable(_renderContext.Enums.CullFace);
            _renderContext.Clear(_renderContext.Enums.DepthBufferBit);

            _previewShader.Use();
            _previewShader.SetMatrix4("uMVP", mvp);
            _renderContext.ActiveTexture(0);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, _cubemapTex);
            _previewCube.Bind();
            _renderContext.DrawElements(_renderContext.Enums.Triangles, _previewCube.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);

            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _lineShader.Use();
            _lineShader.SetMatrix4("uMVP", mvp);

            if (_axisBuffer != null)
            {
                _axisBuffer.Bind();
                _renderContext.DrawElements(_renderContext.Enums.Lines, _axisBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            }
            if (_ringBuffer != null)
            {
                _ringBuffer.Bind();
                _renderContext.LineWidth(2.5f);
                _renderContext.DrawElements(_renderContext.Enums.Lines, _ringBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
                _renderContext.LineWidth(1f);
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
            _ringBuffer?.Dispose();
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