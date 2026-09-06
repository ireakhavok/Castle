// Folder: SiegeEngine/Core/Managers
// File: HostedContentRegistry.cs
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Scenes;
using System;
using System.Collections.Generic;

namespace SiegeEngine.Core.Managers
{
    public static class HostedContentRegistry
    {
        private static readonly Dictionary<string, Func<SceneContext, IHostedContent>> _factories =
            new Dictionary<string, Func<SceneContext, IHostedContent>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _titles =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void Register(string key, Func<SceneContext, IHostedContent> factory, string title = null)
        {
            if (string.IsNullOrWhiteSpace(key) || factory == null) return;
            _factories[key] = factory;
            if (!string.IsNullOrWhiteSpace(title))
                _titles[key] = title;
        }

        public static IHostedContent Create(string key, SceneContext ctx)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (!_factories.TryGetValue(key, out var factory) || factory == null) return null;
            return factory(ctx);
        }

        public static IReadOnlyCollection<string> AllKeys => _factories.Keys;

        public static void OpenRegistered(SceneContext ctx)
        {
            if (ctx?.EventBus == null) return;
            foreach (var kv in _factories)
            {
                IHostedContent content = null;
                try { content = kv.Value(ctx); }
                catch (Exception ex)
                {
                    Console.WriteLine("[HostedContentRegistry] Create failed for '" + kv.Key + "': " + ex.Message);
                    continue;
                }
                if (content == null) continue;
                string title = _titles.TryGetValue(kv.Key, out var t) && !string.IsNullOrWhiteSpace(t) ? t : kv.Key;
                ctx.EventBus.Publish(new OpenHostedContentEvent
                {
                    Key = kv.Key,
                    Title = title,
                    Content = content,
                    Open = true
                });
            }
        }
    }
}
