using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Data.Vector2d;
using UnityEngine;

[Serializable]
public class CustomWorldObjectData
{
    public LatitudeLongitude LatLng;
    public GameObject Prefab;
    public Vector3 RotationEuler;
    public Vector3 Scale;
}

public class CustomWorldVisualData
{
    public CustomWorldObjectData Data;
    [NonSerialized] public GameObject GeneratedVisual;
    [NonSerialized] public List<LatitudeLongitude> Footprint;

    public CustomWorldVisualData(CustomWorldObjectData sampleData)
    {
        Data = sampleData;
    }
}