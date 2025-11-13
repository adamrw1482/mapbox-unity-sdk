using System;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.Example.Scripts.MapInput
{
    [Serializable]
    public class SlippyMapCamera : MapInput
    {
        public enum RotationMode
        {
            RotateTheCamera,
            RotateTheMap
        }

        [Serializable]
        private class RotationSettings
        {
            public bool Enabled = true;
            public float Speed = 50.0f;
            public RotationMode rotationMode;
            public Transform MapRoot;
        }
        
        [Serializable]
        private class ZoomSettings
        {
            public bool Enabled = true;
            public float Speed = 0.25f;
            public bool ZoomAtCursor = true;
        }

        public LatitudeLongitude CenterLatitudeLongitude;
        [Range(15, 90)]
        public float Pitch;
        [Range(-180, 180)]
        public float Bearing;
        public float CameraDistance;
        [NonSerialized] public float ZoomValue;
        [NonSerialized] public float ScaleValue;

        private float _initialZoom;
        private float _initialScale;

        public bool PanEnabled = true;
        
        // public bool RotateEnabled = true;
        // public float RotationSpeed = 50.0f;
        [SerializeField] private RotationSettings _rotationSettings;
        [SerializeField] private ZoomSettings _zoomSettings;
        private float _initialPitch;
        
        private Vector3 _previousScreenPosition;
        private Vector3 _dragOrigin;
        private Vector3 _targetPosition;
        private Plane _controlPlane;
        private float deltaAngleH;
        private float deltaAngleV;
        

        public void Initialize(Camera camera, IMapInformation start, Plane? controlPlane = null)
        {
            _camera = camera;
            _controlPlane = controlPlane ?? new Plane(Vector3.up, Vector3.zero);
            if (_rotationSettings.MapRoot == null)
            {
                _rotationSettings.MapRoot = GameObject.FindObjectOfType<MapBehaviourCore>().transform;
            }
            Pitch = start.Pitch;
            Bearing = start.Bearing;
            _initialZoom = start.Zoom;
            _initialScale = start.Scale;
            _initialPitch = Pitch;
            ZoomValue = start.Zoom;
            ScaleValue = start.Scale;
            SetCamPositionByMapInfo();
            start.SetView += SetCamera;
        }
        
        public override bool UpdateCamera(IMapInformation mapInformation)
        {
            var hasChanged = false;
            Vector3 cursorHit;
            Vector2d newMercatorCenter = mapInformation.CenterMercator;
            
            if (!GetPlaneIntersection(Input.mousePosition, out cursorHit))
                return false;
            
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                _previousScreenPosition = Input.mousePosition;
                _dragOrigin = cursorHit;
            }
            
            if (Input.GetMouseButton(0) && PanEnabled)
            {
                Vector3 pos = cursorHit - _dragOrigin;

                var oldCursorLatLng = Conversions.LatitudeLongitudeToWebMercator(mapInformation.ConvertPositionToLatLng(_rotationSettings.MapRoot.InverseTransformPoint(_dragOrigin)));
                var newCursorLatLng = Conversions.LatitudeLongitudeToWebMercator(mapInformation.ConvertPositionToLatLng(_rotationSettings.MapRoot.InverseTransformPoint(cursorHit)));
                newMercatorCenter = mapInformation.CenterMercator - (newCursorLatLng - oldCursorLatLng);
                hasChanged = true;
            }
            else if (Input.GetMouseButton(1) && _rotationSettings.Enabled)
            {
                var deltaMousePos = (Input.mousePosition - _previousScreenPosition);
                deltaAngleH = deltaMousePos.x;
                deltaAngleV = deltaMousePos.y;
                if (deltaAngleH != 0 || deltaAngleV != 0)
                {
                    var currentPitch = deltaAngleV * Time.deltaTime * _rotationSettings.Speed;
                    Pitch -= currentPitch;
                    Pitch = Mathf.Min(90, Mathf.Max(15, Pitch));
                    var currentBearing = deltaAngleH * Time.deltaTime * _rotationSettings.Speed;
                    Bearing += currentBearing;
                    hasChanged = true;
                }
            }
            else if (Input.mouseScrollDelta.magnitude > 0 && _zoomSettings.Enabled)
            {
                if (!_zoomSettings.ZoomAtCursor)
                {
                    var postZoom = ZoomValue + Input.GetAxis("Mouse ScrollWheel") * _zoomSettings.Speed;
                    ZoomValue = postZoom;
                    ScaleValue = _initialScale / Mathf.Pow(2, (ZoomValue - _initialZoom));
                    hasChanged = true;
                }
                else
                {
                    var mouseMeter = cursorHit * ScaleValue;
                    var startingMercatorCursor = Conversions.LatitudeLongitudeToWebMercator(mapInformation.ConvertPositionToLatLng(_rotationSettings.MapRoot.InverseTransformPoint(cursorHit)));
                    ZoomValue = ZoomValue + Input.GetAxis("Mouse ScrollWheel") * _zoomSettings.Speed;
                    ScaleValue = _initialScale / Mathf.Pow(2, (ZoomValue - _initialZoom));
                    var newMercatorCursor = Conversions.LatitudeLongitudeToWebMercator(mapInformation.ConvertPositionToLatLngForScale(_rotationSettings.MapRoot.InverseTransformPoint(cursorHit), ScaleValue));
                    var change = newMercatorCursor - startingMercatorCursor;
                    newMercatorCenter = mapInformation.CenterMercator - change;
                    hasChanged = true;
                }
            }

            CenterLatitudeLongitude = Conversions.WebMercatorToLatLon(newMercatorCenter);
            _dragOrigin = cursorHit;
            _previousScreenPosition = Input.mousePosition;

            if(hasChanged) SetCamPositionByMapInfo();
            
            return hasChanged;
        }

        private void SetCamPositionByMapInfo()
        {
            _camera.transform.position = _targetPosition;
            
            if (_rotationSettings.rotationMode == RotationMode.RotateTheCamera)
            {
                _camera.transform.rotation = Quaternion.Euler(Pitch, Bearing, 0);
            }
            else if (_rotationSettings.rotationMode == RotationMode.RotateTheMap)
            {
                var projectedForward = Vector3.ProjectOnPlane(_camera.transform.forward, _controlPlane.normal);
                var verticalRotation = Quaternion.AngleAxis(Pitch-_initialPitch, projectedForward.Perpendicular());
                var vector = Quaternion.Euler(Pitch, Bearing, 0);
                var worldVector = _camera.transform.rotation * vector;
                _rotationSettings.MapRoot.rotation = verticalRotation * Quaternion.Euler(0, Bearing, 0);
            }
            
            _camera.transform.position += _camera.transform.forward * (-1f * CameraDistance);
        }

        public void SetCamera(IMapInformation mapInfo)
        {
            _targetPosition = Vector3.zero;
            Pitch = mapInfo.Pitch;
            Bearing = mapInfo.Bearing;
            //SetCamPositionByMapInfo(mapInfo);
            
            if (_rotationSettings.rotationMode == RotationMode.RotateTheCamera)
            {
                SetCamPositionByMapInfo();
            }
            else if (_rotationSettings.rotationMode == RotationMode.RotateTheMap)
            {
                var projectedForward = Vector3.ProjectOnPlane(_camera.transform.forward, _controlPlane.normal);
                var verticalRotation = Quaternion.AngleAxis(Pitch-_initialPitch, projectedForward.Perpendicular());
                var vector = Quaternion.Euler(Pitch, Bearing, 0);
                var worldVector = _camera.transform.rotation * vector;
                _rotationSettings.MapRoot.rotation = verticalRotation * Quaternion.Euler(0, Bearing, 0);
            }
        }

        private bool GetPlaneIntersection(Vector3 screenPosition, out Vector3 hit)
        {
            hit = Vector3.zero;
            var ray = _camera.ScreenPointToRay(screenPosition);
            if (_controlPlane.Raycast(ray, out var distance))
            { 
                hit = ray.GetPoint(distance);
                return true;
            }

            return false;
        }

        public Plane[] GetFrustrumPlanes()
        {
            return GeometryUtility.CalculateFrustumPlanes(_camera);
        }

        public Transform GetTransform()
        {
            return _camera.transform;
        }
    }
}