using System;
using UnityEngine;

namespace InspectorLocalization
{
    /// <summary>
    /// Replaces the field name shown in the Unity Inspector without changing
    /// the actual C# field name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class InspectorLabelAttribute : PropertyAttribute
    {
        public string Label { get; }

        public InspectorLabelAttribute(string label)
        {
            Label = label;
        }
    }
}
