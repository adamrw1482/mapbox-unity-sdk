using System;
using Mapbox.BaseModule.Map;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mapbox.Example.Scripts.MapInput
{
    [Serializable]
    public class Moving3dCamera : MapInput
    {
        [Tooltip("Camera tilt angle. 90 = top-down, 15 = near-horizon")]
        [Range(15, 90)]
        public float Pitch;
        [Tooltip("Camera rotation in degrees. 0 = north up")]
        [Range(-180, 180)]
        public float Bearing;

        [NonSerialized] public float ZoomValue;
        [NonSerialized] public float CameraDistance;

        [Tooltip("Animation curve mapping zoom level to camera distance. X axis = zoom, Y axis = distance")]
        public AnimationCurveContainer CameraCurve;
        [Tooltip("Scales camera distance from curve output. Use to adjust height without re-authoring the curve. Default: 2")]
        [FormerlySerializedAs("CamDistanceMultiplier")]
        public float DistanceScale = 2;
        [Tooltip("Scroll wheel zoom sensitivity per step. Default: 0.25")]
        [FormerlySerializedAs("ZoomSpeed")]
        public float ZoomSensitivity = 0.25f;
        [Tooltip("Right-click rotation sensitivity. Higher = faster rotation. Default: 50")]
        public float RotationSpeed = 50.0f;

        private Vector3 _previousScreenPosition;
        private Vector3 _dragOrigin;
        private Vector3 _targetPosition;


        public override void Initialize(Camera camera, IMapInformation start)
        {
            base.Initialize(camera, start);
            Pitch = start.Pitch;
            Bearing = start.Bearing;
            ZoomValue = start.Zoom;
            SetCamera(start);
            start.LatitudeLongitudeChanged += information =>
            {
                _targetPosition = Vector3.zero;
                SetCamera(information);
            };
            start.ViewChanged += SetCamera;
        }

        public override CameraOutput UpdateCamera(IMapInformation mapInformation)
        {
            _output.Reset();
            UpdateInputState();

            if (IsPointerOverUI())
                return _output;

            var pointerPos = GetPointerPosition();
            Vector3 cursorHit;
            if (!GetPlaneIntersection(pointerPos, out cursorHit))
                return _output;

            if (GetPointerDown() || GetSecondaryDown())
            {
                _previousScreenPosition = pointerPos;
                _dragOrigin = cursorHit;
            }

            if (GetPointerHeld())
            {
                if (!GetPlaneIntersection(pointerPos, out var newPoint))
                    return _output;
                Vector3 pos = newPoint - _dragOrigin;
                _targetPosition -= new Vector3(pos.x, 0, pos.z);
                _output.HasChanged = true;
            }
            else if (GetSecondaryHeld())
            {
                var deltaMousePos = (pointerPos - _previousScreenPosition);
                var deltaAngleH = deltaMousePos.x;
                var deltaAngleV = deltaMousePos.y;
                if (deltaAngleH != 0 || deltaAngleV != 0)
                {
                    Pitch -= deltaAngleV * Time.deltaTime * RotationSpeed;
                    Pitch = ClampPitch(Pitch);
                    Bearing = ClampBearing(Bearing + deltaAngleH * Time.deltaTime * RotationSpeed);
                    _output.HasChanged = true;
                }
            }
            else if (GetPinchZoomDelta(out var zoomDelta))
            {
                var zoomCenter = GetZoomCenter();
                if (GetPlaneIntersection(zoomCenter, out var zoomHit))
                {
                    Zoom(mapInformation, zoomHit, zoomDelta);
                    _output.HasChanged = true;
                }
            }

            if (GetTwoFingerTiltDelta(out var tiltDelta))
            {
                Pitch -= tiltDelta * RotationSpeed;
                Pitch = ClampPitch(Pitch);
                _output.HasChanged = true;
            }

            GetPlaneIntersection(pointerPos, out _dragOrigin);
            _previousScreenPosition = pointerPos;

            if (_output.HasChanged)
            {
                SetCamPositionByMapInfo();
                _output.Zoom = ZoomValue;
                _output.Pitch = Pitch;
                _output.Bearing = Bearing;
            }
            return _output;
        }

        public void Zoom(IMapInformation mapInformation, Vector3 position, float zoomAction)
        {
            var postZoom = ClampZoom(ZoomValue + zoomAction * ZoomSensitivity);
				
            //to be able to achieve zoom on mouse cursor, we have to move camera on mouse world pos - camera pos line (b-c)
            //but our camera distance uses camera target (mid screen) to camera pos distance (a-c)
            //and there'll be a difference between (ac) and (bc) distance
            //so we calculate new distance and then use pre/post distance ratio to calculate the value on mouse-camera line
            //we then use this new (bc) distance for final pos calculation
            /// a----b
            /// |   / 
            /// |  / 
            /// | /
            /// c
            var newScaleWillBe = mapInformation.GetScaleFor(postZoom);
            var latlng = mapInformation.ConvertPositionToLatLng(position);
            var postZoomPos = mapInformation.ConvertLatLngToPositionForScale(latlng, newScaleWillBe);
            
            var targetPoslatlng = mapInformation.ConvertPositionToLatLng(_targetPosition);
            var postZoomTarget = mapInformation.ConvertLatLngToPositionForScale(targetPoslatlng, newScaleWillBe);
            
            
            var preDistance = CalculateCameraDistance(mapInformation, ZoomValue);
            var camDistanceToMouse = Vector3.Distance(_camera.transform.position, position);
            ZoomValue = postZoom;
            var postDistance = CalculateCameraDistance(mapInformation, postZoom);
            var newCamDistanceToMouse = camDistanceToMouse * (postDistance / preDistance);
            CameraDistance = CalculateCameraDistance(mapInformation, ZoomValue);
            _targetPosition = Vector3.LerpUnclamped(postZoomTarget, postZoomPos, (camDistanceToMouse - newCamDistanceToMouse) / camDistanceToMouse);
        }

        private void SetCamPositionByMapInfo()
        {
            _camera.transform.position = _targetPosition;
            _camera.transform.rotation = Quaternion.Euler(Pitch, Bearing, 0);
            _camera.transform.position += _camera.transform.forward * (-1f * CameraDistance);
            GetPlaneIntersection(GetPointerPosition(), out _dragOrigin);
        }

        public void SetCamera(IMapInformation mapInfo)
        {
            Pitch = mapInfo.Pitch;
            Bearing = mapInfo.Bearing;

            CameraDistance = CalculateCameraDistance(mapInfo, mapInfo.Zoom);
            _camera.transform.position = _targetPosition;
            _camera.transform.rotation = Quaternion.Euler(Pitch, Bearing, 0);
            _camera.transform.position += _camera.transform.forward * (-1f * CameraDistance);
            GetPlaneIntersection(GetPointerPosition(), out _dragOrigin);
        }

        private float CalculateCameraDistance(IMapInformation mapInformation, float postZoom)
        {
            var distance = CameraCurve.Evaluate(postZoom);
            return DistanceScale * distance / mapInformation.Scale;
        }

        public Vector3 GetViewCenterPosition()
        {
            GetPlaneIntersection(_camera.ViewportToScreenPoint(new Vector3(.5f, .5f, 0f)), out var center);
            return center;
        }

    }
}