using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liviu.Environment.HDRP.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(PolyverseStyleSky))]
    internal sealed class PolyverseStyleSkyEditor : SkySettingsEditor
    {
        private sealed class Section
        {
            public readonly string title;
            public readonly List<SerializedDataParameter> fields = new();
            public readonly List<Section> children = new();
            public SerializedDataParameter toggle;
            public Section(string title) => this.title = title;
        }

        private readonly List<Section> sections = new();

        public override void OnEnable()
        {
            base.OnEnable();
            sections.Clear();
            Section root = null;
            Section current = null;
            foreach (var field in typeof(PolyverseStyleSky)
                         .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                         .Where(field => typeof(VolumeParameter).IsAssignableFrom(field.FieldType))
                         .OrderBy(field => field.MetadataToken))
            {
                var parameter = Unpack(serializedObject.FindProperty(field.Name));
                var (title, nested, toggle) = GetSection(field.Name);
                if (title != null)
                {
                    current = new Section(title);
                    if (nested)
                        root.children.Add(current);
                    else
                    {
                        root = current;
                        sections.Add(current);
                    }
                    if (toggle)
                    {
                        current.toggle = parameter;
                        continue;
                    }
                }
                current.fields.Add(parameter);
            }
        }

        public override void OnInspectorGUI()
        {
            foreach (var section in sections)
                DrawSection(section);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("HDRP Sky Settings", EditorStyles.boldLabel);
                CommonSkySettingsGUI();
            }
        }

        private void DrawSection(Section section)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var toggle = section.toggle;
                    if (toggle == null)
                        EditorGUILayout.LabelField(section.title, EditorStyles.boldLabel);
                    else
                    {
                        // Volume override and feature enable remain separate controls in the header.
                        if (enableOverrides)
                            DrawOverrideCheckbox(toggle);
                        using (new EditorGUI.DisabledScope(!toggle.overrideState.boolValue))
                        {
                            var rect = EditorGUILayout.GetControlRect();
                            var label = new GUIContent(section.title, toggle.GetAttribute<TooltipAttribute>()?.tooltip);
                            EditorGUI.BeginProperty(rect, label, toggle.value);
                            bool previousMixed = EditorGUI.showMixedValue;
                            EditorGUI.showMixedValue = toggle.value.hasMultipleDifferentValues;
                            EditorGUI.BeginChangeCheck();
                            bool enabled = EditorGUI.ToggleLeft(rect, label, toggle.value.boolValue, EditorStyles.boldLabel);
                            if (EditorGUI.EndChangeCheck())
                                toggle.value.boolValue = enabled;
                            EditorGUI.showMixedValue = previousMixed;
                            EditorGUI.EndProperty();
                        }
                    }
                }
                if (section.toggle != null && !section.toggle.value.hasMultipleDifferentValues
                                           && !section.toggle.value.boolValue)
                    return;
                if (section.fields.Count > 0 || section.children.Count > 0)
                {
                    EditorGUILayout.Space(3);
                    foreach (var field in section.fields)
                        PropertyField(field);
                    foreach (var child in section.children)
                        DrawSection(child);
                }
            }
            EditorGUILayout.Space(3);
        }

        private static (string title, bool nested, bool toggle) GetSection(string name) => name switch
        {
            nameof(PolyverseStyleSky.backgroundMode) => ("Background", false, false),
            nameof(PolyverseStyleSky.debugSolidColor) => ("Debug Solid Color", false, true),
            nameof(PolyverseStyleSky.enablePatternOverlay) => ("Pattern Overlay", false, true),
            nameof(PolyverseStyleSky.enableStars) => ("Stars", false, true),
            nameof(PolyverseStyleSky.enableStarsTwinkling) => ("Twinkling", true, true),
            nameof(PolyverseStyleSky.enableStarsRotation) => ("Stars Rotation", true, true),
            nameof(PolyverseStyleSky.enableSun) => ("Sun", false, true),
            nameof(PolyverseStyleSky.enableMoon) => ("Moon", false, true),
            nameof(PolyverseStyleSky.enableClouds) => ("Clouds", false, true),
            nameof(PolyverseStyleSky.cloudsCoverage) => ("Procedural Cloud Shape", true, false),
            nameof(PolyverseStyleSky.cloudsLayerAltitude) => ("Cloud Layer And Distance", true, false),
            nameof(PolyverseStyleSky.cloudsWindVelocity) => ("Procedural Cloud Animation", true, false),
            nameof(PolyverseStyleSky.cloudsLightColor) => ("Cloud Lighting", true, false),
            nameof(PolyverseStyleSky.enableCloudsRotation) => ("Cloud Rotation", true, true),
            nameof(PolyverseStyleSky.enableHorizonFog) => ("Horizon Fog Overlay", false, true),
            nameof(PolyverseStyleSky.skyboxOffset) => ("Sky Orientation", false, false),
            _ => (null, false, false)
        };
    }
}
