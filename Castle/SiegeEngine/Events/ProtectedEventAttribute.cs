using System;

namespace SiegeEngine.Events
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class ProtectedEventAttribute : Attribute
    {
    }
}