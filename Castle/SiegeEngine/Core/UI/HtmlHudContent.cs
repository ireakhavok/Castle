// Folder: SiegeEngine/Core/UI
// File: HtmlHudContent.cs
using SiegeEngine.Core.Interfaces;

namespace SiegeEngine.Core.UI
{
    public sealed class HtmlHudContent : IHostedContent
    {
        public string DataKey { get; }
        public string Html { get; }
        public string BaseDir { get; }

        public HtmlHudContent(string key, string html, string baseDir = "")
        {
            DataKey = key ?? "hud";
            Html = html ?? "";
            BaseDir = baseDir ?? "";
        }

        public void Init() { }
        public void Update(float deltaTime) { }
        public void Render() { }
        public void Dispose() { }
    }
}
