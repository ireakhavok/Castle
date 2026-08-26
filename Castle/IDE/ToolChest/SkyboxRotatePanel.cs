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
using SiegeEngine.Core.Networking;
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
        private SkyboxPreviewScene _previewScene;
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
            _previewScene = new SkyboxPreviewScene(renderContext, controlContext, window, new ClientGameServerProxy(eventBus), eventBus);
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
            _previewScene.Initialize((int)Size.Y, (int)Size.X);
            LoadPreviewCubemap();
            _previewScene.SetOrientation(_workingSkybox.Orientation);
            _previewScene.SetSelectedFace(_selectedFace);
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
            uint old = _previewScene.CubemapTexture;
            if (old != 0)
            {
                _renderContext.DeleteTexture(old);
                _previewScene.SetCubemapTexture(0);
            }
            if (_workingSkybox == null || !_workingSkybox.Enabled)
                return;
            uint tex;
            _renderContext.GenTextures(1, out tex);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, tex);
            _renderContext.TexParameter(_renderContext.Enums.TextureCubeMap, _renderContext.Enums.TextureMinFilter, _renderContext.Enums.Linear);
            _renderContext.TexParameter(_renderContext.Enums.TextureCubeMap, _renderContext.Enums.TextureMagFilter, _renderContext.Enums.Linear);
            _renderContext.TexParameter(_renderContext.Enums.TextureCubeMap, _renderContext.Enums.TextureWrapS, _renderContext.Enums.ClampToEdge);
            _renderContext.TexParameter(_renderContext.Enums.TextureCubeMap, _renderContext.Enums.TextureWrapT, _renderContext.Enums.ClampToEdge);
            _renderContext.TexParameter(_renderContext.Enums.TextureCubeMap, _renderContext.Enums.TextureWrapR, _renderContext.Enums.ClampToEdge);
            for (int i = 0; i < 6; i++)
                UploadFace(tex, i);
            _renderContext.GenerateMipmap(_renderContext.Enums.TextureCubeMap);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, 0);
            _previewScene.SetCubemapTexture(tex);
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
        private void UploadFace(uint tex, int faceIndex)
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
                    _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, tex);
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
            uint tex = _previewScene.CubemapTexture;
            if (tex == 0) return;
            UploadFace(tex, _selectedFace);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, tex);
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
            uint tex = _previewScene.CubemapTexture;
            if (tex == 0) return;
            UploadFace(tex, _selectedFace);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, tex);
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
            uint tex = _previewScene.CubemapTexture;
            if (tex != 0)
            {
                UploadFace(tex, a);
                UploadFace(tex, b);
                _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, tex);
                _renderContext.GenerateMipmap(_renderContext.Enums.TextureCubeMap);
                _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, 0);
            }
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
        private void PushSkyboxOffsetLive()
        {
            var level = ProjectSettings.Current.CurrentLevel;
            if (level != null)
                level.Skybox = _workingSkybox;
            string sceneName = ProjectSettings.Current.CurrentSceneName;
            if (!string.IsNullOrEmpty(sceneName))
            {
                var live = ProjectStateManager.Current.GetLiveState(sceneName);
                if (live != null)
                    live.Skybox = _workingSkybox;
            }
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
            bool changed = false;
            foreach (var child in span.Children)
            {
                if (child is TextElement te)
                {
                    if (te.Content != text)
                    {
                        te.Content = text;
                        changed = true;
                    }
                    break;
                }
            }
            if (changed)
                _uiOverlay.RefreshUI();
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
            _previewScene?.SetSelectedFace(_selectedFace);
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
            var slider = _uiOverlay.FindElementById("heightSlider") as RangeElement;
            if (slider != null)
            {
                float v = slider.Value;
                if (v != _workingSkybox.VerticalOffset)
                {
                    _workingSkybox.VerticalOffset = v;
                    UpdateHeightLabel();
                    PushSkyboxOffsetLive();
                }
            }
            if (_previewScene == null) return;
            float header = HasTitleBar ? HeaderHeight : 0f;
            Vector2 rel = absMousePos - Position;
            bool inPreview = rel.Y > header && rel.Y < Size.Y - 140f && rel.X > 0 && rel.X < Size.X;
            if (inPreview && mousePressed)
            {
                int ring = _previewScene.PickRing(rel, Size.X, Size.Y - header, header, RingPickTolerance);
                if (ring >= 0)
                {
                    _activeRing = ring;
                    _dragStartOrient = _workingSkybox.Orientation;
                    _accumAngle = 0f;
                    var (o, d, ok) = _previewScene.GetPreviewRay(rel, Size.X, Size.Y - header, header);
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
                var (o, d, ok) = _previewScene.GetPreviewRay(rel, Size.X, Size.Y - header, header);
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
                    _previewScene.SetOrientation(_workingSkybox.Orientation);
                }
            }
            if (_ringDragging && mouseReleased)
            {
                float snap = MathF.Round(_accumAngle / (MathF.PI * 0.5f)) * (MathF.PI * 0.5f);
                Vector3 localAxis = GetAxisVector(_activeRing);
                Quaternion delta = Quaternion.CreateFromAxisAngle(localAxis, snap);
                _workingSkybox.Orientation = Quaternion.Normalize(delta * _dragStartOrient);
                _previewScene.SetOrientation(_workingSkybox.Orientation);
                PushSkyboxLive();
                _ringDragging = false;
                _activeRing = -1;
            }
            if (_orbitDragging && mouseDown)
            {
                Vector2 delta = rel - _lastMouse;
                _previewScene.PreviewYaw += delta.X * 0.012f;
                _previewScene.PreviewPitch = Math.Clamp(_previewScene.PreviewPitch + delta.Y * 0.012f, -1.4f, 1.4f);
                _lastMouse = rel;
            }
            if (mouseReleased)
            {
                _orbitDragging = false;
            }
            if (inPreview && MathF.Abs(scrollDelta) > 0.01f)
                _previewScene.PreviewDist = Math.Clamp(_previewScene.PreviewDist - scrollDelta * 0.25f, 1.6f, 7f);
        }
        private Vector3 ClosestPointOnPlane(Vector3 rayO, Vector3 rayD, Vector3 center, Vector3 normal)
        {
            float denom = Vector3.Dot(rayD, normal);
            if (Math.Abs(denom) < 1e-6f) return center;
            float t = Vector3.Dot(center - rayO, normal) / denom;
            return rayO + t * rayD;
        }
        private Vector3 GetAxisVector(int axis) => axis switch { 0 => Vector3.UnitX, 1 => Vector3.UnitY, 2 => Vector3.UnitZ, _ => Vector3.UnitZ };
        public override void OnLiveResize(float w, float h)
        {
            _previewScene?.Resize((int)w, (int)h);
            base.OnLiveResize(w, h);
        }
        protected override void RenderInnerContent()
        {
            if (_previewScene == null) return;
            _previewScene.Render(null);
        }
        public override void Dispose()
        {
            _previewScene?.Dispose();
            _previewScene = null;
            base.Dispose();
        }
        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new SkyboxRotatePanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}