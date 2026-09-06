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
        public HudAnchor Anchor { get; }
        public float RequestedX { get; }
        public float RequestedY { get; }
        private readonly string _htmlPath;
        private readonly string _htmlContent;

        public GameHudPanel(
            IRenderContext renderContext,
            IControlContext controlContext,
            nint window,
            EventBus eventBus,
            OpenGameHudEvent request)
            : base(renderContext, controlContext, window, eventBus)
        {
            HudKey = request.HtmlRelativePath ?? request.Title ?? "hud";
            Anchor = request.Anchor;
            RequestedX = request.PosX;
            RequestedY = request.PosY;
            _htmlContent = request.HtmlContent;
            _htmlPath = string.IsNullOrEmpty(_htmlContent) ? ResolveHtml(request.HtmlRelativePath) : null;
            ChromeStyle = request.Chrome;
            DockingMode = request.Docking;
            HasTitleBar = request.AllowMove;
            IsClosable = request.Chrome != PanelChromeStyle.Bare;
            AllowDragging = request.AllowMove;
            BaseWidth = request.Width > 0 ? request.Width : 360f;
            BaseHeight = request.Height > 0 ? request.Height : 280f;
        }

        public override void Init()
        {
            base.Init();
            string html = _htmlContent;
            string baseDir = "";
            if (string.IsNullOrEmpty(html) && !string.IsNullOrEmpty(_htmlPath) && File.Exists(_htmlPath))
            {
                html = File.ReadAllText(_htmlPath);
                baseDir = Path.GetDirectoryName(_htmlPath) ?? "";
            }
            if (!string.IsNullOrEmpty(html))
            {
                _uiOverlay.LoadUI(html, baseDir);
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
            string name = Path.GetFileName(path);
            string[] guesses =
            {
                path,
                Path.Combine(Directory.GetCurrentDirectory(), path),
                Path.Combine(Directory.GetCurrentDirectory(), "Scripts", name),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name)
            };
            for (int i = 0; i < guesses.Length; i++)
            {
                if (!string.IsNullOrEmpty(guesses[i]) && File.Exists(guesses[i]))
                    return Path.GetFullPath(guesses[i]);
            }
            return path;
        }
    }
}
