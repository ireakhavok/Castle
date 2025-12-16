// Folder: SiegeEngine.UI/JSParser
// File: JSBinding.cs
using System;
using System.Collections.Generic;

namespace SiegeEngine.Core.UI.JSParser
{
    public class JSBinding
    {
        private Dictionary<string, Delegate> _bindings = new Dictionary<string, Delegate>();

        public void Bind(string name, Delegate action)
        {
            _bindings[name] = action;
        }

        public object Invoke(string name, params object[] args)
        {
            if (_bindings.TryGetValue(name, out Delegate del))
            {
                return del.DynamicInvoke(args);
            }
            throw new Exception($"Binding not found: {name}");
        }
    }
}