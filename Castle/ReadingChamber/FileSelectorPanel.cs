// Folder: ReadingChamber
// File: FileSelectorPanel.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Definitions;
using SiegeEngine.Events;
using SiegeEngine.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ReadingChamber
{
    public class FileSelectorPanel : BasePanel
    {
        private class FileSelectorUIOverlay : UIOverlay
        {
            private readonly FileSelectorPanel _parent;
            public FileSelectorUIOverlay(FileSelectorPanel parent, IRenderContext renderContext, IControlContext controlContext, IntPtr window) : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }
            protected override void HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
            }
        }

        private string _currentDir;
        private List<string> _history = new List<string>();
        private int _historyIndex = -1;
        private string _viewType = "list";
        private string _sortBy = "name";
        private bool _sortAscending = true;

        public FileSelectorPanel(IRenderContext renderContext, IControlContext controlContext, IntPtr window, EventBus eventBus, string initialDir) : base(renderContext, controlContext, window, eventBus)
        {
            _currentDir = initialDir;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new FileSelectorUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            NavigateTo(_currentDir);
        }

        private void NavigateTo(string dir, bool addToHistory = true)
        {
            if (addToHistory)
            {
                if (_historyIndex < _history.Count - 1)
                {
                    _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
                }
                _history.Add(dir);
                _historyIndex = _history.Count - 1;
            }
            _currentDir = dir;
            UpdateFileList();
        }

        private void UpdateFileList()
        {
            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileSelectorTemplate.html");
            if (!File.Exists(templatePath))
            {
                Console.WriteLine($"FileSelectorPanel: Template HTML file not found at {templatePath}");
                return;
            }
            string templateHtml = File.ReadAllText(templatePath);
            StringBuilder dynamicItems = new StringBuilder();

            // Get directories and files
            var dirs = Directory.GetDirectories(_currentDir);
            var files = Directory.GetFiles(_currentDir);

            // Combine and sort
            var items = new List<(string Name, string Path, bool IsDir, long Size, DateTime Modified)>();
            foreach (var dir in dirs)
            {
                items.Add((Path.GetFileName(dir), dir, true, 0, Directory.GetLastWriteTime(dir)));
            }
            foreach (var file in files)
            {
                var fi = new FileInfo(file);
                items.Add((fi.Name, file, false, fi.Length, fi.LastWriteTime));
            }

            // Sort
            if (_sortBy == "name")
            {
                items = _sortAscending ? items.OrderBy(i => i.IsDir ? 0 : 1).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList() : items.OrderByDescending(i => i.IsDir ? 0 : 1).ThenByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
            else if (_sortBy == "size")
            {
                items = _sortAscending ? items.OrderBy(i => i.IsDir ? 0 : 1).ThenBy(i => i.Size).ToList() : items.OrderByDescending(i => i.IsDir ? 0 : 1).ThenByDescending(i => i.Size).ToList();
            }
            else if (_sortBy == "date")
            {
                items = _sortAscending ? items.OrderBy(i => i.IsDir ? 0 : 1).ThenBy(i => i.Modified).ToList() : items.OrderByDescending(i => i.IsDir ? 0 : 1).ThenByDescending(i => i.Modified).ToList();
            }

            // Generate HTML table rows
            if (items.Count == 0)
            {
                dynamicItems.Append("<tr><td colspan=\"4\" style=\"text-align: center; color: #888888;\">No files or directories found.</td></tr>");
            }
            else
            {
                foreach (var item in items)
                {
                    string hook = item.IsDir ? $"EnterDir:{item.Path.Replace("\\", "\\\\")}" : $"SelectFile:{item.Path.Replace("\\", "\\\\")}";
                    string cls = item.IsDir ? "dir" : GetFileClass(item.Name);
                    string icon = GetIcon(cls);
                    string sizeStr = item.IsDir ? "" : FormatSize(item.Size);
                    string dateStr = item.Modified.ToString("yyyy-MM-dd HH:mm");
                    dynamicItems.Append($"<tr class='{cls}' data-hook=\"{hook}\"><td>{icon}</td><td>{item.Name}</td><td>{sizeStr}</td><td>{dateStr}</td></tr>");
                }
            }

            string currentDirEscaped = _currentDir.Replace("\\", "\\\\");
            string modifiedHtml = templateHtml.Replace("<!--CURRENT_DIR-->", currentDirEscaped).Replace("<!--DYNAMIC_ITEMS-->", dynamicItems.ToString());
            modifiedHtml = modifiedHtml.Replace("class=\"file-table\"", $"class=\"file-table {_viewType}\"");

            _uiOverlay.LoadUI(modifiedHtml);
            var fileTableElem = _uiOverlay.FindElementById("file-table");
            if (fileTableElem != null)
            {
                fileTableElem.Style.AlignItems = "flex-start";
                _uiOverlay.RefreshUI();
            }
        }

        private string GetFileClass(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            return "file" + (string.IsNullOrEmpty(ext) ? "" : ext.Replace(".", "-"));
        }

        private string GetIcon(string cls)
        {
            if (cls == "dir") return "📁";
            if (cls == "file-fbx") return "🗿";
            if (cls == "file-png" || cls == "file-jpg" || cls == "file-jpeg" || cls == "file-gif") return "🖼️";
            if (cls == "file-txt" || cls == "file-md") return "📝";
            if (cls == "file-json" || cls == "file-xml") return "⚙️";
            return "📄";
        }

        private string FormatSize(long size)
        {
            if (size < 1024) return size + " B";
            if (size < 1024 * 1024) return (size / 1024) + " KB";
            if (size < 1024 * 1024 * 1024) return (size / (1024 * 1024)) + " MB";
            return (size / (1024 * 1024 * 1024)) + " GB";
        }

        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (hook.StartsWith("EnterDir:"))
            {
                string path = hook.Substring(9);
                NavigateTo(path);
            }
            else if (hook.StartsWith("SelectFile:"))
            {
                string path = hook.Substring(11);
                _eventBus.Publish(new FileSelectedEvent(path));
                // Close panel
            }
            else if (hook == "back")
            {
                if (_historyIndex > 0)
                {
                    _historyIndex--;
                    NavigateTo(_history[_historyIndex], false);
                }
            }
            else if (hook == "forward")
            {
                if (_historyIndex < _history.Count - 1)
                {
                    _historyIndex++;
                    NavigateTo(_history[_historyIndex], false);
                }
            }
            else if (hook == "up")
            {
                string parent = Directory.GetParent(_currentDir)?.FullName;
                if (parent != null)
                {
                    NavigateTo(parent);
                }
            }
            else if (hook == "view-list")
            {
                _viewType = "list";
                UpdateFileList();
            }
            else if (hook == "view-grid")
            {
                _viewType = "grid";
                UpdateFileList();
            }
            else if (hook == "sort-name")
            {
                if (_sortBy == "name") _sortAscending = !_sortAscending;
                _sortBy = "name";
                UpdateFileList();
            }
            else if (hook == "sort-size")
            {
                if (_sortBy == "size") _sortAscending = !_sortAscending;
                _sortBy = "size";
                UpdateFileList();
            }
            else if (hook == "sort-date")
            {
                if (_sortBy == "date") _sortAscending = !_sortAscending;
                _sortBy = "date";
                UpdateFileList();
            }
        }
    }
}