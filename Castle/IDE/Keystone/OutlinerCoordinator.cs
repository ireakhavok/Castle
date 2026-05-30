// Folder: Keystone
// File: OutlinerCoordinator.cs
using SiegeEngine.Core.Events;
using System;
using System.Collections.Generic;

namespace Keystone
{
    // Static singleton - lives ONLY in Keystone (shared editor layer).
    // No references to this class exist in SiegeEngine.Core.*.
    // Content panels call SetAsActiveProvider(this, eventBus) from their OnContentFocusGained.
    public sealed class OutlinerCoordinator
    {
        private static OutlinerCoordinator _instance;
        public static OutlinerCoordinator Instance => _instance ??= new OutlinerCoordinator();

        private IOutlinerProvider _lastActiveProvider;
        private static EventBus _eventBus;

        // Per-ContentType state cache (expanded + selected) - survives panel switches
        private readonly Dictionary<string, HashSet<string>> _expandedCache = new Dictionary<string, HashSet<string>>();
        private readonly Dictionary<string, List<string>> _selectedCache = new Dictionary<string, List<string>>();

        private OutlinerCoordinator() { }

        public static void Initialize(EventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public void SetAsActiveProvider(IOutlinerProvider provider, EventBus eventBus)
        {
            if (_eventBus == null) _eventBus = eventBus;

            if (provider == _lastActiveProvider) return;

            _lastActiveProvider = provider;
            Console.WriteLine($"[OutlinerCoordinator] Last active provider changed to: {provider?.ContentType ?? "null"}");

            var evt = new GenericEvent { Hook = "OutlinerHierarchyUpdate" };
            evt.Data["contentType"] = provider?.ContentType ?? "unknown";
            _eventBus?.Publish(evt);
        }

        public IOutlinerProvider GetLastActiveProvider() => _lastActiveProvider;

        public void NotifySelectionChanged(string nodeId)
        {
            if (_lastActiveProvider == null) return;
            var evt = new GenericEvent { Hook = "OutlinerSelectionChanged" };
            evt.Data["nodeId"] = nodeId;
            _eventBus?.Publish(evt);
        }

        public void NotifyHierarchyChanged()
        {
            if (_lastActiveProvider == null) return;
            var evt = new GenericEvent { Hook = "OutlinerHierarchyUpdate" };
            evt.Data["contentType"] = _lastActiveProvider.ContentType;
            _eventBus?.Publish(evt);
        }

        public List<OutlinerNode> GetCurrentHierarchy(out string[] expandedIds, out string[] selectedIds)
        {
            expandedIds = Array.Empty<string>();
            selectedIds = Array.Empty<string>();

            if (_lastActiveProvider == null) return new List<OutlinerNode>();

            string ct = _lastActiveProvider.ContentType;
            var hierarchy = _lastActiveProvider.GetCurrentHierarchy();

            if (_expandedCache.TryGetValue(ct, out var expandedSet))
                expandedIds = expandedSet.ToArray();
            if (_selectedCache.TryGetValue(ct, out var selectedList))
                selectedIds = selectedList.ToArray();

            return hierarchy;
        }

        public void SaveExpandedState(string contentType, IEnumerable<string> expanded)
        {
            _expandedCache[contentType] = new HashSet<string>(expanded);
        }

        public void SaveSelectedState(string contentType, IEnumerable<string> selected)
        {
            _selectedCache[contentType] = new List<string>(selected);
        }
    }
}