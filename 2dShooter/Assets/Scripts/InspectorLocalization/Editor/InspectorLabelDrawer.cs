#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace InspectorLocalization.Editor
{
    [CustomPropertyDrawer(typeof(InspectorLabelAttribute))]
    public sealed class InspectorLabelDrawer : PropertyDrawer
    {
        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            var localizedAttribute = (InspectorLabelAttribute)attribute;
            var localizedLabel = new GUIContent(
                localizedAttribute.Label,
                label.tooltip);

            EditorGUI.PropertyField(position, property, localizedLabel, true);
        }
    }
}
#endif
