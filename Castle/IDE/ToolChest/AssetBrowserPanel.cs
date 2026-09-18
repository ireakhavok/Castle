// Folder: ToolChest
// File: AssetBrowserPanel.cs
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace ToolChest
{
    public class AssetBrowserPanel : BasePanel
    {
        private class AssetBrowserUIOverlay : UIOverlay
        {
            private readonly AssetBrowserPanel _parent;
            public AssetBrowserUIOverlay(AssetBrowserPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }
            protected override void HandleDataHook(string hook)
            {
                _parent.HandleDataHook(hook);
            }
            public override bool HandleUIClick(HtmlElement elem)
            {
                base.HandleUIClick(elem);
                _parent.HandleUIClick(elem);
                return true;
            }
        }

        private string _currentPath;
        private string _armedPath;
        private int _viewMode = 1; // 0 list, 1 icons, 2 details

        public AssetBrowserPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            AllowDragging = true;
            DockState = DockState.Floating;
            DockingMode = SiegeEngine.Core.Definitions.DockingMode.IDE;
            BaseWidth = 420f;
            BaseHeight = 420f;
            string assets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            _currentPath = Directory.Exists(assets) ? assets : AppDomain.CurrentDomain.BaseDirectory;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new AssetBrowserUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            chrome.close_color = new Vector4(0.486f, 1.0f, 0.796f, 1.0f);
            LoadBrowserUI();
        }

        private void LoadBrowserUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AssetBrowserPanelUI.html");
            if (!File.Exists(htmlPath))
            {
                Console.WriteLine($"[AssetBrowserPanel] ERROR: AssetBrowserPanelUI.html not found at {htmlPath}");
                return;
            }
            string html = File.ReadAllText(htmlPath);
            _uiOverlay.LoadUI(html);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
            RefreshBrowser();
        }

        private void RefreshBrowser()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AssetBrowserPanelUI.html");
            if (!File.Exists(htmlPath)) return;
            string template = File.ReadAllText(htmlPath);
            string itemsHtml = BuildItemsHtml();
            string[] viewClass = { "view-list", "view-icons", "view-details" };
            int mode = _viewMode % 3;
            string on = "active";
            string finalHtml = template
                .Replace("<!--ITEMS-->", itemsHtml)
                .Replace("<!--PATH-->", _currentPath ?? "")
                .Replace("<!--VIEWCLASS-->", viewClass[mode])
                .Replace("<!--LISTACTIVE-->", mode == 0 ? on : "")
                .Replace("<!--ICONSACTIVE-->", mode == 1 ? on : "")
                .Replace("<!--DETAILSACTIVE-->", mode == 2 ? on : "");
            _uiOverlay.LoadUI(finalHtml);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private static bool IsHidden(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            if (name.StartsWith(".")) return true;
            string ext = Path.GetExtension(name).ToLowerInvariant();
            return ext == ".meta" || ext == ".cs" || ext == ".dll" || ext == ".pdb";
        }

        private static bool IsImage(string ext)
        {
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
        }

        private static bool IsPlaceable(string ext)
        {
            return ext == ".fbx" || ext == ".json" || ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga";
        }

        private static string FindFbxPreview(string fbxPath)
        {
            string dir = Path.GetDirectoryName(fbxPath);
            string stem = Path.GetFileNameWithoutExtension(fbxPath);
            if (string.IsNullOrEmpty(dir)) return null;
            string fbm = Path.Combine(dir, stem + ".fbm");
            string[] guesses =
            {
                Path.Combine(dir, stem + ".png"),
                Path.Combine(dir, "T_" + stem + "_B.png"),
                Path.Combine(dir, "..", "Textures", "T_" + stem + "_B.png"),
                Path.Combine(dir, "..", "..", "Textures", "T_" + stem + "_B.png")
            };
            foreach (var g in guesses)
            {
                try
                {
                    string full = Path.GetFullPath(g);
                    if (File.Exists(full)) return full;
                }
                catch { }
            }
            if (Directory.Exists(fbm))
            {
                var first = Directory.GetFiles(fbm)
                    .FirstOrDefault(f =>
                    {
                        string e = Path.GetExtension(f).ToLowerInvariant();
                        return e == ".png" || e == ".jpg" || e == ".jpeg";
                    });
                if (first != null) return first;
            }
            try
            {
                string textures = Path.GetFullPath(Path.Combine(dir, "..", "Textures"));
                if (Directory.Exists(textures))
                {
                    var hit = Directory.GetFiles(textures, "*.png")
                        .FirstOrDefault(f => Path.GetFileName(f).StartsWith("T_", StringComparison.OrdinalIgnoreCase)
                                          && Path.GetFileName(f).IndexOf("_B", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (hit != null) return hit;
                }
            }
            catch { }
            return null;
        }

        private static string CssUrl(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            return path.Replace("\\", "/").Replace("'", "%27");
        }

        private string BuildItemsHtml()
        {
            int mode = _viewMode % 3;
            if (mode == 1) return BuildIconItems();
            if (mode == 2) return BuildDetailItems();
            return BuildListItems();
        }

        private string BuildListItems()
        {
            var sb = new StringBuilder();
            try
            {
                foreach (var dir in Directory.GetDirectories(_currentPath).OrderBy(d => d))
                {
                    string name = Path.GetFileName(dir);
                    if (IsHidden(name)) continue;
                    sb.AppendLine($"<div class='item folder' data-hook='Enter:{dir}'>📁 {name}</div>");
                }
                foreach (var file in Directory.GetFiles(_currentPath).OrderBy(f => f))
                {
                    string name = Path.GetFileName(file);
                    if (IsHidden(name)) continue;
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    string icon = ext == ".fbx" ? "📦" : IsImage(ext) ? "🖼️" : ext == ".wav" || ext == ".mp3" || ext == ".ogg" ? "🎵" : "📄";
                    sb.AppendLine($"<div class='item file' data-hook='Select:{file}'>{icon} {name}</div>");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssetBrowserPanel] list failed: {ex.Message}");
            }
            return sb.ToString();
        }

        private string BuildIconItems()
        {
            var sb = new StringBuilder();
            try
            {
                foreach (var dir in Directory.GetDirectories(_currentPath).OrderBy(d => d))
                {
                    string name = Path.GetFileName(dir);
                    if (IsHidden(name)) continue;
                    sb.AppendLine($"<div class='tile folder' data-hook='Enter:{dir}'><div class='thumb'>📁</div><div class='label'>{name}</div></div>");
                }
                foreach (var file in Directory.GetFiles(_currentPath).OrderBy(f => f))
                {
                    string name = Path.GetFileName(file);
                    if (IsHidden(name)) continue;
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    string preview = "";
                    if (IsImage(ext)) preview = CssUrl(file);
                    else if (ext == ".fbx")
                    {
                        string img = FindFbxPreview(file);
                        if (!string.IsNullOrEmpty(img)) preview = CssUrl(img);
                    }
                    string style = string.IsNullOrEmpty(preview) ? "" : $" style=\"background-image:url('{preview}')\"";
                    string inner = "";
                    if (string.IsNullOrEmpty(preview))
                        inner = ext == ".fbx" ? "📦" : ext == ".wav" || ext == ".mp3" || ext == ".ogg" ? "🎵" : ext == ".json" ? "📦" : "📄";
                    sb.AppendLine($"<div class='tile file' data-hook='Select:{file}'><div class='thumb'{style}>{inner}</div><div class='label'>{name}</div></div>");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssetBrowserPanel] icons failed: {ex.Message}");
            }
            return sb.ToString();
        }

        private string BuildDetailItems()
        {
            var sb = new StringBuilder();
            try
            {
                foreach (var dir in Directory.GetDirectories(_currentPath).OrderBy(d => d))
                {
                    string name = Path.GetFileName(dir);
                    if (IsHidden(name)) continue;
                    sb.AppendLine($"<div class='detail folder' data-hook='Enter:{dir}'><span>{name}</span><span class='kind'>Folder</span><span class='fullpath'>{dir}</span></div>");
                }
                foreach (var file in Directory.GetFiles(_currentPath).OrderBy(f => f))
                {
                    string name = Path.GetFileName(file);
                    if (IsHidden(name)) continue;
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    string kind = ext == ".fbx" ? "Mesh" : IsImage(ext) ? "Texture" : ext == ".json" ? "Pack" : ext.Trim('.');
                    sb.AppendLine($"<div class='detail file' data-hook='Select:{file}'><span>{name}</span><span class='kind'>{kind}</span><span class='fullpath'>{file}</span></div>");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssetBrowserPanel] details failed: {ex.Message}");
            }
            return sb.ToString();
        }

        public void HandleDataHook(string hook)
        {
            if (hook.StartsWith("Enter:"))
            {
                string path = hook.Substring(6);
                if (Directory.Exists(path))
                {
                    _currentPath = path;
                    RefreshBrowser();
                }
            }
            else if (hook.StartsWith("Select:"))
            {
                string path = hook.Substring(7);
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (IsPlaceable(ext))
                {
                    if (!string.IsNullOrEmpty(_armedPath) && _armedPath != path)
                        _eventBus.Publish(new AssetDragEvent(_armedPath, AssetDragPhase.Cancel));
                    _armedPath = path;
                    _eventBus.Publish(new AssetDragEvent(path, AssetDragPhase.Begin));
                    Console.WriteLine($"[AssetBrowserPanel] Armed '{nameOf(path)}' — click the Scene Editor viewport to place");
                }
                _eventBus.Publish(new FileSelectedEvent(path));
            }
            else if (hook == "ViewList")
            {
                _viewMode = 0;
                RefreshBrowser();
            }
            else if (hook == "ViewIcons")
            {
                _viewMode = 1;
                RefreshBrowser();
            }
            else if (hook == "ViewDetails")
            {
                _viewMode = 2;
                RefreshBrowser();
            }
            else if (hook == "Up")
            {
                var parent = Directory.GetParent(_currentPath);
                if (parent != null)
                {
                    _currentPath = parent.FullName;
                    RefreshBrowser();
                }
            }
        }

        static string nameOf(string path)
        {
            try { return Path.GetFileName(path); } catch { return path; }
        }

        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (!string.IsNullOrEmpty(hook))
                HandleDataHook(hook);
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new AssetBrowserPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}
