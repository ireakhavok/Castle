// Folder: ReadingChamber
// File: FileSelectorPanel.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Definitions;
using SiegeEngine.Events;
using SiegeEngine.Interfaces;
using SiegeEngine.UI;
using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace ReadingChamber
{
    public class FileSelectorPanel : IPanel
    {
        private readonly IRenderContext _renderContext;
        private readonly IControlContext _controlContext;
        private readonly IntPtr _window;
        private readonly EventBus _eventBus;
        private string _currentDir;
        private UIOverlay _uiOverlay;
        private int _lastW;
        private int _lastH;

        public DockState DockState { get; set; } = DockState.Floating;

        public FileSelectorPanel(IRenderContext renderContext, IControlContext controlContext, IntPtr window, EventBus eventBus, string initialDir)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
            _eventBus = eventBus;
            _currentDir = initialDir;
            _uiOverlay = new UIOverlay(renderContext, controlContext, window);
        }

        public void Init()
        {
            _uiOverlay.Init();
            UpdateFileList();
        }

        private void UpdateFileList()
        {
            StringBuilder html = new StringBuilder();
            html.Append("<div style=\"position: absolute; top: 0; left: 0; width: 100%; height: 100%; background-color: rgba(255,255,255,0.8); display: flex; flex-direction: column;\">");
            html.Append("<h2>Files in " + _currentDir + "</h2>");
            // List directories
            foreach (var dir in Directory.GetDirectories(_currentDir))
            {
                string dirName = Path.GetFileName(dir);
                html.Append($"<button data-hook=\"EnterDir:{dir}\">{dirName}/</button>");
            }
            // List FBX files
            foreach (var file in Directory.GetFiles(_currentDir, "*.fbx"))
            {
                string fileName = Path.GetFileName(file);
                html.Append($"<button data-hook=\"SelectFile:{file}\">{fileName}</button>");
            }
            html.Append("</div>");
            _uiOverlay.LoadUI(html.ToString());
        }

        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (hook.StartsWith("EnterDir:"))
            {
                _currentDir = hook.Substring(9);
                UpdateFileList();
            }
            else if (hook.StartsWith("SelectFile:"))
            {
                string path = hook.Substring(11);
                _eventBus.Publish(new FileSelectedEvent(path));
                // Close panel, assuming PanelManager handles removal
            }
        }

        public void Update(float deltaTime)
        {
            _uiOverlay.Update(deltaTime);
        }

        public void Render()
        {
            _controlContext.GetWindowSize(_window, out int w, out int h);
            if (w != _lastW || h != _lastH)
            {
                _lastW = w;
                _lastH = h;
                _uiOverlay.RecomputeLayout(w, h);
            }
            _renderContext.Viewport(0, 0, (uint)w, (uint)h);
            _renderContext.ClearColor(0.8f, 0.8f, 0.8f, 1.0f);
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _uiOverlay.Render();
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }

        public void Dispose()
        {
            _uiOverlay.Dispose();
        }

        public void Detach()
        {
        }
    }
}