using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.Example.Scripts.Map;
using UnityEngine;

namespace Mapbox.Example.Scripts
{
	/// <summary>
	/// Runtime benchmark for tile providers. Animates the camera through a scripted path
	/// (pan between two lat/lng points with rotation) and records the tile cover output
	/// every frame. Results are written to a JSON file for comparison across algorithm changes.
	///
	/// Usage:
	/// 1. Add this component to the same GameObject as MapboxMapBehaviour
	/// 2. Set StartLatLng, EndLatLng, zoom, pitch, bearing parameters
	/// 3. Enter Play mode — the camera will animate automatically
	/// 4. On-screen overlay shows live tile count and zoom distribution
	/// 5. When the run finishes (or you press the StopKey), results are saved to Application.persistentDataPath
	/// </summary>
	public class TileProviderBenchmark : MonoBehaviour
	{
		[Header("Camera Path")]
		public double StartLat = 40.7128;
		public double StartLng = -74.0060;
		public double EndLat = 40.7580;
		public double EndLng = -73.9855;

		[Header("Camera Settings")]
		public float Zoom = 16f;
		public float StartPitch = 45f;
		public float EndPitch = 45f;
		public float StartBearing = 0f;
		public float EndBearing = 90f;

		[Header("Animation")]
		public float DurationSeconds = 10f;
		public bool PingPong = true;
		public int PingPongCycles = 2;
		public bool AutoStart = true;

		[Header("Controls")]
		public KeyCode StartKey = KeyCode.F5;
		public KeyCode StopKey = KeyCode.F6;

		[Header("Output")]
		public string OutputFileName = "tile_benchmark";

		private MapBehaviourCore _mapBehaviour;
		private MapboxMap _map;
		private bool _isMapReady;
		private bool _isRunning;
		private float _elapsed;
		private int _totalFrames;
		private int _currentCycle;

		// Per-frame recording
		private List<FrameRecord> _records = new List<FrameRecord>(2000);

		// Live stats for on-screen display
		private int _lastTileCount;
		private int[] _lastZoomDistribution = new int[23]; // z0-z22
		private int _tileChangesThisRun;
		private HashSet<UnwrappedTileId> _previousFrameTiles = new HashSet<UnwrappedTileId>();

		private void Awake()
		{
			_mapBehaviour = GetComponent<MapBehaviourCore>();
			if (_mapBehaviour == null)
			{
				Debug.LogError("[TileProviderBenchmark] No MapBehaviourCore found on this GameObject.");
				enabled = false;
				return;
			}

			// Set start position before initialization so the map loads at the right place
			_mapBehaviour.MapInformation.SetInformation(
				new LatitudeLongitude(StartLat, StartLng), Zoom, StartPitch, StartBearing);

			_mapBehaviour.Initialized += map =>
			{
				_map = map;
				_map.LoadViewCompleted += () =>
				{
					_isMapReady = true;
					if (AutoStart) StartRun();
				};
			};

			// Start initialization ourselves (InitializeOnStart should be unchecked on MapBehaviourCore)
			StartCoroutine(_mapBehaviour.Initialize());
		}

		private void Update()
		{
			if (Input.GetKeyDown(StartKey) && !_isRunning && _isMapReady)
				StartRun();
			if (Input.GetKeyDown(StopKey) && _isRunning)
				StopRun();

			if (!_isRunning || !_isMapReady) return;

			_elapsed += Time.deltaTime;

			// Compute normalized time with ping-pong
			float totalDuration = DurationSeconds * (PingPong ? PingPongCycles * 2 : 1);
			if (_elapsed >= totalDuration)
			{
				StopRun();
				return;
			}

			float cycleTime = _elapsed % (PingPong ? DurationSeconds * 2 : DurationSeconds);
			float t;
			if (PingPong && cycleTime > DurationSeconds)
				t = 1f - (cycleTime - DurationSeconds) / DurationSeconds;
			else
				t = cycleTime / DurationSeconds;

			t = Mathf.SmoothStep(0f, 1f, t);

			// Interpolate camera parameters
			var lat = Lerp(StartLat, EndLat, t);
			var lng = Lerp(StartLng, EndLng, t);
			var pitch = Mathf.Lerp(StartPitch, EndPitch, t);
			var bearing = Mathf.Lerp(StartBearing, EndBearing, t);

			var latlng = new LatitudeLongitude(lat, lng);
			_map.ChangeView(latlng, Zoom, pitch, bearing);

			// Record this frame
			RecordFrame(t, lat, lng, pitch, bearing);
			_totalFrames++;
		}

		public void StartRun()
		{
			_isRunning = true;
			_elapsed = 0f;
			_totalFrames = 0;
			_currentCycle = 0;
			_tileChangesThisRun = 0;
			_records.Clear();
			_previousFrameTiles.Clear();
			Debug.Log($"[TileProviderBenchmark] Run started. Duration: {DurationSeconds}s, PingPong: {PingPong}");
		}

		public void StopRun()
		{
			_isRunning = false;
			SaveResults();
			Debug.Log($"[TileProviderBenchmark] Run complete. {_totalFrames} frames recorded, {_tileChangesThisRun} tile changes. Results saved.");
		}

		private void RecordFrame(float t, double lat, double lng, float pitch, float bearing)
		{
			var tiles = _map.TileCover.Tiles;
			_lastTileCount = tiles.Count;

			// Zoom distribution
			Array.Clear(_lastZoomDistribution, 0, _lastZoomDistribution.Length);
			foreach (var tile in tiles)
			{
				if (tile.Z >= 0 && tile.Z < _lastZoomDistribution.Length)
					_lastZoomDistribution[tile.Z]++;
			}

			// Count tile changes from previous frame
			int added = 0;
			int removed = 0;
			foreach (var tile in tiles)
			{
				if (!_previousFrameTiles.Contains(tile))
					added++;
			}
			foreach (var tile in _previousFrameTiles)
			{
				if (!tiles.Contains(tile))
					removed++;
			}
			_tileChangesThisRun += added + removed;

			// Build zoom distribution string
			var zoomDist = new int[23];
			Array.Copy(_lastZoomDistribution, zoomDist, 23);

			_records.Add(new FrameRecord
			{
				Frame = _totalFrames,
				Time = _elapsed,
				T = t,
				Lat = lat,
				Lng = lng,
				Pitch = pitch,
				Bearing = bearing,
				TileCount = _lastTileCount,
				TilesAdded = added,
				TilesRemoved = removed,
				ZoomDistribution = zoomDist,
				CameraPosition = Camera.main != null ? Camera.main.transform.position : Vector3.zero,
			});

			// Update previous frame set
			_previousFrameTiles.Clear();
			foreach (var tile in tiles)
				_previousFrameTiles.Add(tile);
		}

		private void SaveResults()
		{
			var sb = new StringBuilder();
			sb.AppendLine("{");
			sb.AppendLine($"  \"provider\": \"{_map.MapService.GetType().Name}\",");
			sb.AppendLine($"  \"startLatLng\": [{StartLat}, {StartLng}],");
			sb.AppendLine($"  \"endLatLng\": [{EndLat}, {EndLng}],");
			sb.AppendLine($"  \"zoom\": {Zoom},");
			sb.AppendLine($"  \"startPitch\": {StartPitch}, \"endPitch\": {EndPitch},");
			sb.AppendLine($"  \"startBearing\": {StartBearing}, \"endBearing\": {EndBearing},");
			sb.AppendLine($"  \"duration\": {DurationSeconds},");
			sb.AppendLine($"  \"pingPong\": {PingPong.ToString().ToLower()},");
			sb.AppendLine($"  \"totalFrames\": {_totalFrames},");
			sb.AppendLine($"  \"totalTileChanges\": {_tileChangesThisRun},");

			// Summary stats
			int maxTiles = 0, minTiles = int.MaxValue;
			long sumTiles = 0;
			int maxChanges = 0;
			long sumChanges = 0;
			foreach (var r in _records)
			{
				if (r.TileCount > maxTiles) maxTiles = r.TileCount;
				if (r.TileCount < minTiles) minTiles = r.TileCount;
				sumTiles += r.TileCount;
				var changes = r.TilesAdded + r.TilesRemoved;
				if (changes > maxChanges) maxChanges = changes;
				sumChanges += changes;
			}
			sb.AppendLine($"  \"summary\": {{");
			sb.AppendLine($"    \"minTileCount\": {minTiles},");
			sb.AppendLine($"    \"maxTileCount\": {maxTiles},");
			sb.AppendLine($"    \"avgTileCount\": {(_records.Count > 0 ? sumTiles / (float)_records.Count : 0):F1},");
			sb.AppendLine($"    \"maxFrameChanges\": {maxChanges},");
			sb.AppendLine($"    \"avgFrameChanges\": {(_records.Count > 0 ? sumChanges / (float)_records.Count : 0):F2}");
			sb.AppendLine($"  }},");

			// Per-frame data
			sb.AppendLine("  \"frames\": [");
			for (int i = 0; i < _records.Count; i++)
			{
				var r = _records[i];
				var zd = FormatZoomDist(r.ZoomDistribution);
				sb.Append($"    {{\"f\":{r.Frame},\"t\":{r.T:F4},\"lat\":{r.Lat:F6},\"lng\":{r.Lng:F6}," +
					$"\"pitch\":{r.Pitch:F1},\"bearing\":{r.Bearing:F1}," +
					$"\"tiles\":{r.TileCount},\"added\":{r.TilesAdded},\"removed\":{r.TilesRemoved}," +
					$"\"camY\":{r.CameraPosition.y:F2}," +
					$"\"zoom_dist\":{zd}}}");
				sb.AppendLine(i < _records.Count - 1 ? "," : "");
			}
			sb.AppendLine("  ]");
			sb.AppendLine("}");

			var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
			var path = Path.Combine(Application.persistentDataPath, $"{OutputFileName}_{timestamp}.json");
			File.WriteAllText(path, sb.ToString());
			Debug.Log($"[TileProviderBenchmark] Results saved to: {path}");
		}

		private string FormatZoomDist(int[] dist)
		{
			var sb = new StringBuilder("{");
			bool first = true;
			for (int z = 0; z < dist.Length; z++)
			{
				if (dist[z] > 0)
				{
					if (!first) sb.Append(",");
					sb.Append($"\"{z}\":{dist[z]}");
					first = false;
				}
			}
			sb.Append("}");
			return sb.ToString();
		}

		private void OnGUI()
		{
			if (!_isRunning && _records.Count == 0) return;

			var style = new GUIStyle(GUI.skin.label)
			{
				fontSize = 14,
				fontStyle = FontStyle.Bold,
			};
			style.normal.textColor = Color.white;

			float x = 10, y = 10;
			float lineHeight = 20;

			// Measure content height first
			int zoomLines = 0;
			for (int z = 0; z < _lastZoomDistribution.Length; z++)
			{
				if (_lastZoomDistribution[z] > 0)
					zoomLines++;
			}
			// header + stats + "Zoom distribution:" + zoom bars + optional hint line + padding
			float contentHeight = lineHeight * (3 + zoomLines) + 20;
			if (!_isRunning && _records.Count > 0)
				contentHeight += lineHeight + 4;

			// Background
			GUI.DrawTexture(new Rect(5, 5, 320, contentHeight), Texture2D.grayTexture);

			GUI.Label(new Rect(x, y, 300, lineHeight),
				_isRunning ? "TILE BENCHMARK RUNNING" : "BENCHMARK COMPLETE", style);
			y += lineHeight;

			GUI.Label(new Rect(x, y, 300, lineHeight),
				$"Frame: {_totalFrames}  Tiles: {_lastTileCount}  Changes: {_tileChangesThisRun}", style);
			y += lineHeight;

			// Zoom distribution bars (highest zoom first)
			GUI.Label(new Rect(x, y, 300, lineHeight), "Zoom distribution:", style);
			y += lineHeight;
			for (int z = _lastZoomDistribution.Length - 1; z >= 0; z--)
			{
				if (_lastZoomDistribution[z] > 0)
				{
					var barWidth = Mathf.Min(_lastZoomDistribution[z] * 4, 200);
					GUI.DrawTexture(new Rect(x + 30, y + 3, barWidth, 14), Texture2D.whiteTexture);
					GUI.Label(new Rect(x, y, 30, lineHeight), $"z{z}", style);
					GUI.Label(new Rect(x + 35 + barWidth, y, 50, lineHeight), $"{_lastZoomDistribution[z]}", style);
					y += lineHeight;
				}
			}

			if (!_isRunning && _records.Count > 0)
			{
				y += 4;
				GUI.Label(new Rect(x, y, 300, lineHeight),
					$"Press {StartKey} to re-run, {StopKey} during run to stop early", style);
			}
		}

		private static double Lerp(double a, double b, float t) => a + (b - a) * t;

		[Serializable]
		private struct FrameRecord
		{
			public int Frame;
			public float Time;
			public float T;
			public double Lat, Lng;
			public float Pitch, Bearing;
			public int TileCount;
			public int TilesAdded, TilesRemoved;
			public int[] ZoomDistribution;
			public Vector3 CameraPosition;
		}
	}
}
