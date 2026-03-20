// Folder: ToolChest
// File: AssetBrowserPanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            public override void HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
            }
        }

        private string _currentPath = AppDomain.CurrentDomain.BaseDirectory;
        private string _searchTerm = "";

        public AssetBrowserPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            AllowDragging = true;
            DockState = DockState.Floating;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new AssetBrowserUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
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
            string finalHtml = template.Replace("<!--ITEMS-->", itemsHtml);
            _uiOverlay.LoadUI(finalHtml);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private string BuildItemsHtml()
        {
            var sb = new StringBuilder();
            try
            {
                var dirs = Directory.GetDirectories(_currentPath);
                var files = Directory.GetFiles(_currentPath);
                foreach (var dir in dirs)
                {
                    string name = Path.GetFileName(dir);
                    sb.AppendLine($"<div class='item folder' data-hook='Enter:{dir}'>📁 {name}</div>");
                }
                foreach (var file in files)
                {
                    string name = Path.GetFileName(file);
                    string ext = Path.GetExtension(file).ToLower();
                    string icon = ext switch
                    {
                        ".fbx" => "📦",
                        ".png" or ".jpg" => "🖼️",
                        ".mp3" or ".wav" => "🎵",
                        _ => "📄"
                    };
                    sb.AppendLine($"<div class='item file' data-hook='Select:{file}'>{icon} {name}</div>");
                }
            }
            catch { }
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
                _eventBus.Publish(new FileSelectedEvent(path));
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

        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (!string.IsNullOrEmpty(hook))
            {
                HandleDataHook(hook);
            }
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new AssetBrowserPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}