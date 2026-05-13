using UnityEditor;
using UnityEngine;

namespace Mapbox.ImageModule.Editor
{
	/// <summary>
	/// Renders a <c>TerrainLayerModuleSettings</c> SerializedProperty with two inspector
	/// enhancements: (1) the <c>ExtractCpuElevationData</c> toggle is force-checked and
	/// disabled when another setting requires CPU elevation; (2) a warning HelpBox appears
	/// when the scene contains a module that typically needs CPU elevation but extraction
	/// is user-disabled. Shared by both terrain-settings editors.
	/// </summary>
	public static class TerrainSettingsInspectorHelper
	{
		private const string ExtractFieldName = "ExtractCpuElevationData";
		private const string UseShaderFieldName = "UseShaderTerrain";
		private const string ElevationPropsFieldName = "ElevationLayerProperties";
		private const string ColliderOptionsFieldName = "colliderOptions";
		private const string AddColliderFieldName = "addCollider";

		public static void Draw(SerializedProperty settingsProp)
		{
			settingsProp.isExpanded = EditorGUILayout.Foldout(settingsProp.isExpanded, settingsProp.displayName, true);
			if (!settingsProp.isExpanded)
			{
				return;
			}

			var useShaderProp = settingsProp.FindPropertyRelative(UseShaderFieldName);
			var colliderProp = settingsProp
				.FindPropertyRelative(ElevationPropsFieldName)
				?.FindPropertyRelative(ColliderOptionsFieldName)
				?.FindPropertyRelative(AddColliderFieldName);

			var useShaderOff = useShaderProp != null && !useShaderProp.boolValue;
			var colliderOn = colliderProp != null && colliderProp.boolValue;
			var forcedOn = useShaderOff || colliderOn;
			var reason = useShaderOff
				? "CPU-elevation rendering (UseShaderTerrain is off)"
				: "the terrain collider (addCollider is on)";

			EditorGUI.indentLevel++;
			var child = settingsProp.Copy();
			var end = settingsProp.GetEndProperty();
			child.NextVisible(true);
			while (!SerializedProperty.EqualContents(child, end))
			{
				if (child.name == ExtractFieldName && forcedOn)
				{
					// Persist the forced state into the serialized value so runtime code that
					// reads ExtractCpuElevationData sees the effective configuration (and so
					// TerrainLayerModule.Initialize doesn't log an override warning).
					if (!child.boolValue)
					{
						child.boolValue = true;
					}
					using (new EditorGUI.DisabledScope(true))
					{
						EditorGUILayout.PropertyField(child);
					}
					EditorGUILayout.HelpBox(
						$"Extraction is force-enabled by {reason}. CPU elevation will be decoded regardless of this checkbox.",
						MessageType.Info);
				}
				else
				{
					EditorGUILayout.PropertyField(child, true);
				}

				if (!child.NextVisible(false))
				{
					break;
				}
			}
			EditorGUI.indentLevel--;

			DrawSceneNeedsExtractionWarning(settingsProp);
		}

		// Warns if extraction is genuinely user-disabled (box off AND nothing forces it on)
		// while the scene contains modules that typically need ground heights.
		private static void DrawSceneNeedsExtractionWarning(SerializedProperty settingsProp)
		{
			var extractProp = settingsProp.FindPropertyRelative(ExtractFieldName);
			if (extractProp == null || extractProp.boolValue)
			{
				return;
			}
			var useShaderProp = settingsProp.FindPropertyRelative(UseShaderFieldName);
			var colliderProp = settingsProp
				.FindPropertyRelative(ElevationPropsFieldName)
				?.FindPropertyRelative(ColliderOptionsFieldName)
				?.FindPropertyRelative(AddColliderFieldName);
			var forcedOn =
				(useShaderProp != null && !useShaderProp.boolValue) ||
				(colliderProp != null && colliderProp.boolValue);
			if (forcedOn)
			{
				return;
			}

			if (SceneHasFeaturesNeedingCpuElevation(out var detected))
			{
				EditorGUILayout.HelpBox(
					$"CPU elevation extraction is disabled, but this scene contains a {detected} that typically snaps to terrain. " +
					"Ground-clamped features will be placed at Y=0 and the elevation query APIs will return 0. " +
					"Re-enable ExtractCpuElevationData unless you are sure nothing in this scene needs ground heights.",
					MessageType.Warning);
			}
		}

		private static readonly string[] WatchedTypeNames =
		{
			"VectorLayerModuleScript",
			"MapboxComponentsModuleScript"
		};

		private static bool SceneHasFeaturesNeedingCpuElevation(out string detectedTypeName)
		{
			detectedTypeName = null;
			var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (var mb in all)
			{
				if (mb == null) continue;
				var name = mb.GetType().Name;
				for (int i = 0; i < WatchedTypeNames.Length; i++)
				{
					if (name == WatchedTypeNames[i])
					{
						detectedTypeName = name;
						return true;
					}
				}
			}
			return false;
		}
	}
}
