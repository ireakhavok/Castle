using System;

namespace SiegeEngine.Core.Events
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class ProtectedEventAttribute : Attribute
    {
    }
}