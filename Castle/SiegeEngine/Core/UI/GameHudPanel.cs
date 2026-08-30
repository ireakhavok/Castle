// Folder: SiegeEngine/Core/UI
// File: GameHudPanel.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.Interfaces;
using System;
using System.IO;

namespace SiegeEngine.Core.UI
{
    public class GameHudPanel : BasePanel
    {
        public string HudKey { get; }
        private readonly string _htmlPath;

        public GameHudPanel(
            IRenderContext renderContext,
            IControlContext controlContext,
            nint window,
            EventBus eventBus,
            OpenGameHudEvent request)
            : base(renderContext, controlContext, window, eventBus)
        {
            HudKey = request.HtmlRelativePath ?? "hud";
            _htmlPath = ResolveHtml(request.HtmlRelativePath);
            ChromeStyle = request.Chrome;
            DockingMode = request.Docking;
            HasTitleBar = request.Chrome != PanelChromeStyle.Bare;
            IsClosable = request.Chrome != PanelChromeStyle.Bare;
            AllowDragging = request.Chrome != PanelChromeStyle.Bare;
            BaseWidth = request.Width > 0 ? request.Width : 360f;
            BaseHeight = request.Height > 0 ? request.Height : 280f;
        }

        public override void Init()
        {
            base.Init();
            if (!string.IsNullOrEmpty(_htmlPath) && File.Exists(_htmlPath))
            {
                _uiOverlay.LoadUI(File.ReadAllText(_htmlPath), Path.GetDirectoryName(_htmlPath) ?? "");
                _uiOverlay.RefreshUI();
            }
            else
            {
                Console.WriteLine("[GameHudPanel] HTML missing: " + _htmlPath);
            }
        }

        private static string ResolveHtml(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (Path.IsPathRooted(path) && File.Exists(path)) return path;
            if (File.Exists(path)) return Path.GetFullPath(path);
            string cwd = Path.Combine(Directory.GetCurrentDirectory(), path);
            if (File.Exists(cwd)) return cwd;
            string underExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
            if (File.Exists(underExe)) return underExe;
            return path;
        }
    }
}
