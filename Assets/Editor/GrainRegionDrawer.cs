using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(GrainRegion))]
public sealed class GrainRegionDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight, gap = EditorGUIUtility.standardVerticalSpacing;
        return property.isExpanded ? line * 2 + gap * 2 + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("layers"), true) : line;
    }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position,label,property);
        float line = EditorGUIUtility.singleLineHeight, gap = EditorGUIUtility.standardVerticalSpacing;
        var percent = property.FindPropertyRelative("percent"); var layers = property.FindPropertyRelative("layers");
        string crops = "";
        if (layers.arraySize > 0)
        {
            var parts = layers.GetArrayElementAtIndex(0).FindPropertyRelative("mixture").FindPropertyRelative("parts");
            for (int i = 0; i < parts.arraySize; i++)
            {
                if (i > 0) crops += " + ";
                crops += GrainNames.Display((GrainType)parts.GetArrayElementAtIndex(i).FindPropertyRelative("grain").intValue);
            }
            if (layers.arraySize > 1) crops += " → buried layers";
        }
        property.isExpanded = EditorGUI.Foldout(new Rect(position.x,position.y,position.width,line),property.isExpanded,
            label.text.Replace("Element","Region") + "  ·  " + percent.floatValue.ToString("0.#") + "%  ·  " + crops,true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            position.y += line + gap;position.height = line;
            EditorGUI.PropertyField(position,percent,new GUIContent("Share of starting mass (%)"));
            position.y += line + gap;position.height = EditorGUI.GetPropertyHeight(layers,true);
            EditorGUI.PropertyField(position,layers,new GUIContent("Local depth layers (top first)"),true);
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}
