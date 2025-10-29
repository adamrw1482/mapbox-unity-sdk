using Mapbox.VectorModule.BuildingLayerVisualizer;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BuildingLayerVisualizerObject))]
public class BuildingLayerVisualizerObjectEditor : Editor
{
    private SerializedProperty _settingsProp;

    private void OnEnable()
    {
        
    }

    public override void OnInspectorGUI()
    {
        _settingsProp = serializedObject.FindProperty("Settings");
        serializedObject.Update();

        EditorGUILayout.LabelField("Building Layer Visualizer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (_settingsProp != null)
        {
            DrawSettings(_settingsProp);
        }
        else
        {
            EditorGUILayout.HelpBox("No _settings field found.", MessageType.Warning);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSettings(SerializedProperty settingsProp)
    {
        var enableTerrainSnapping = settingsProp.FindPropertyRelative("EnableTerrainSnapping");
        var chamferSettings = settingsProp.FindPropertyRelative("ChamferModifierSettings");
        var material = settingsProp.FindPropertyRelative("Material");
        
        EditorGUILayout.PropertyField(material);
        EditorGUILayout.PropertyField(enableTerrainSnapping);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Chamfer Modifier Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(chamferSettings, true);
        EditorGUI.indentLevel--;
    }
}