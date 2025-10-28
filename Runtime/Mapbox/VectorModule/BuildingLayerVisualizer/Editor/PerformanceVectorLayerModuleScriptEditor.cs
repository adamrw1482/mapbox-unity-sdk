using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mapbox.VectorModule.BuildingLayerVisualizer;
using System.Collections.Generic;
using System.ComponentModel;

[CustomEditor(typeof(PerformanceVectorLayerModuleScript))]
public class PerformanceVectorLayerModuleScriptEditor : Editor
{
    private SerializedProperty _layerVisualizersProp;
    private GUIStyle _boxStyle;
    private List<Type> _visualizerTypes;

    private void OnEnable()
    {
        _layerVisualizersProp = serializedObject.FindProperty("_layerVisualizers");

        // Find all ScriptableObject types that implement IPerformanceLayerVisualizer
        _visualizerTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t =>
                typeof(IPerformanceLayerVisualizer).IsAssignableFrom(t) &&
                !t.IsAbstract)
            .ToList();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (_boxStyle == null)
        {
            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(4, 4, 8, 8)
            };
        }

        for (int i = 0; i < _layerVisualizersProp.arraySize; i++)
        {
            var element = _layerVisualizersProp.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical(_boxStyle);
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(element, new GUIContent(""));
            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("Remove", GUILayout.Width(70)))
            {
                _layerVisualizersProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }

            EditorGUILayout.EndHorizontal();

            if (element.objectReferenceValue != null)
            {
                var editor = CreateEditor(element.objectReferenceValue);
                if (editor != null)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    editor.OnInspectorGUI();
                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Add Layer Visualizer", GUILayout.Height(28)))
        {
            ShowAddVisualizerMenu();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void ShowAddVisualizerMenu()
    {
        GenericMenu menu = new GenericMenu();

        if (_visualizerTypes.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("No visualizer types found"));
        }
        else
        {
            foreach (var type in _visualizerTypes)
            {
                var displayName = type
                    .GetCustomAttributes(typeof(DisplayNameAttribute), true)
                    .FirstOrDefault() as DisplayNameAttribute;
                string name = (displayName != null) ? displayName.DisplayName : ObjectNames.NicifyVariableName(type.Name);
                menu.AddItem(new GUIContent(name), false, () => CreateAndAddVisualizer(type));
            }
        }

        menu.ShowAsContext();
    }

    private void CreateAndAddVisualizer(Type type)
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Visualizer",
            type.Name + ".asset",
            "asset",
            "Select a location to save the new visualizer asset."
        );

        if (string.IsNullOrEmpty(path))
            return; // cancelled

        var instance = ScriptableObject.CreateInstance(type);
        AssetDatabase.CreateAsset(instance, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        serializedObject.Update();
        int newIndex = _layerVisualizersProp.arraySize;
        _layerVisualizersProp.InsertArrayElementAtIndex(newIndex);
        _layerVisualizersProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = instance;
        serializedObject.ApplyModifiedProperties();
    }
}
