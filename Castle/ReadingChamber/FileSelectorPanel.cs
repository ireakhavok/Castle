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
    public class FileSelectorPanel : UIPanel
    {
        private string _currentDir;
        private readonly EventBus _eventBus;
        public FileSelectorPanel(IRenderContext renderContext, IControlContext controlContext, IntPtr window, EventBus eventBus, string initialDir) : base(renderContext, controlContext, window)
        {
            _eventBus = eventBus;
            _currentDir = initialDir;
        }
        public override void Init()
        {
            base.Init();
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
            LoadUI(html.ToString());
        }
        protected override void HandleUIClick(HtmlElement elem)
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
        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
        }
        public override void Render()
        {
            _controlContext.GetWindowSize(_window, out int w, out int h);
            _renderContext.Viewport(0, 0, (uint)w, (uint)h);
            _renderContext.ClearColor(0.8f, 0.8f, 0.8f, 1.0f);
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            base.Render();
        }
    }
}