using System;
using UnityEngine;

namespace Mapbox.Example.Scripts.ModuleBehaviours
{
    [Serializable]
    public class CustomSourceSettings
    {
        [Tooltip("Url format string, structured as C# string format with '{}' fields for X/Y/Z coordinates")]
        public string UrlFormat;
        [Tooltip("Invert Y axis coordinates for TMS coordinate system, which starts from bottom left and grows to top-right")]
        public bool InvertY;
    }
}