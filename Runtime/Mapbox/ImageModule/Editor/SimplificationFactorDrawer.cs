using System.Linq;
using Mapbox.ImageModule.Terrain.Settings;
using UnityEditor;
using UnityEngine;

namespace Mapbox.ImageModule.Editor
{
	/// <summary>
	/// Inspector drawer for fields tagged with <see cref="SimplificationFactorAttribute"/>.
	/// Renders a dropdown of distinct-grid presets (each halving the resolution) and a
	/// live HelpBox describing the resulting grid, upgrading to a warning at coarse values.
	/// Legacy non-preset serialized values are snapped to the nearest preset.
	/// </summary>
	[CustomPropertyDrawer(typeof(SimplificationFactorAttribute))]
	public class SimplificationFactorDrawer : PropertyDrawer
	{
		private const float Spacing = 2f;

		// Only factors that produce a distinct grid size are worth exposing — everything in
		// between collapses via integer division. Each entry halves the grid resolution.
		private static readonly int[] PresetFactors = { 1, 2, 4, 8, 16, 32, 64, 128 };

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var attr = (SimplificationFactorAttribute)attribute;
			var factors = PresetFactors.Where(f => f >= attr.Min && f <= attr.Max).ToArray();
			var labels = factors.Select(f => new GUIContent(BuildLabel(f, attr.VertexBase))).ToArray();

			// Snap legacy non-preset values to the nearest preset so the dropdown always
			// matches a concrete entry.
			var current = property.intValue;
			if (System.Array.IndexOf(factors, current) < 0)
			{
				property.intValue = NearestPreset(factors, current);
				current = property.intValue;
			}

			var fieldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
			property.intValue = EditorGUI.IntPopup(fieldRect, label, current, labels, factors);

			var (message, severity) = BuildMessage(property.intValue, attr.VertexBase);
			var helpHeight = CalcHelpBoxHeight(message, position.width);
			var helpRect = new Rect(position.x, fieldRect.yMax + Spacing, position.width, helpHeight);
			EditorGUI.HelpBox(helpRect, message, severity);
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			var attr = (SimplificationFactorAttribute)attribute;
			var (message, _) = BuildMessage(property.intValue, attr.VertexBase);
			return EditorGUIUtility.singleLineHeight + Spacing + CalcHelpBoxHeight(message, EditorGUIUtility.currentViewWidth - 40f);
		}

		private static string BuildLabel(int factor, int vertexBase)
		{
			var sampleCount = vertexBase / Mathf.Max(1, factor);
			var gridSide = sampleCount + 1;
			return $"{factor}  ({gridSide}x{gridSide} verts)";
		}

		private static (string message, MessageType severity) BuildMessage(int factor, int vertexBase)
		{
			var clampedFactor = Mathf.Max(1, factor);
			var sampleCount = vertexBase / clampedFactor;
			var gridSide = sampleCount + 1;
			var verts = gridSide * gridSide;
			var baseLine = $"{gridSide}x{gridSide} vertex grid per tile ({verts} verts, {sampleCount}x{sampleCount} quads).";

			if (sampleCount >= 64)
			{
				return (baseLine + " Highest visual quality — heaviest CPU/GPU cost, especially with colliders enabled.", MessageType.Info);
			}
			if (sampleCount >= 16)
			{
				return (baseLine + " Good balance of detail and performance.", MessageType.Info);
			}
			if (sampleCount >= 8)
			{
				return (baseLine + " Lower-density geometry — fine terrain details will be lost.", MessageType.Info);
			}
			return (baseLine + " Very coarse — terrain will look blocky and neighboring tiles may visibly mismatch at seams.", MessageType.Warning);
		}

		private static int NearestPreset(int[] presets, int value)
		{
			var best = presets[0];
			var bestDiff = Mathf.Abs(best - value);
			for (int i = 1; i < presets.Length; i++)
			{
				var diff = Mathf.Abs(presets[i] - value);
				if (diff < bestDiff)
				{
					bestDiff = diff;
					best = presets[i];
				}
			}
			return best;
		}

		private static float CalcHelpBoxHeight(string message, float width)
		{
			var content = new GUIContent(message);
			var style = EditorStyles.helpBox;
			var textWidth = Mathf.Max(40f, width - 32f);
			return Mathf.Max(EditorGUIUtility.singleLineHeight * 2f, style.CalcHeight(content, textWidth));
		}
	}
}
