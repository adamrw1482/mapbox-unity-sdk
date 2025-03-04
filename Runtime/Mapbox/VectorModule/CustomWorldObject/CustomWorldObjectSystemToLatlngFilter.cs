using System;
using System.Collections;
using System.Collections.Generic;
using Mapbox.VectorModule.Filters;
using Samples.CustomObjects;
using UnityEngine;
using UnityEngine.Serialization;

public class CustomWorldObjectSystemToLatlngFilter : MonoBehaviour
{
    public CustomWorldObjectSystem CustomWorldObjectSystem;
    public LatLngCollisionFilterObject LatlngFilterObject;

    public void Start()
    {
        CustomWorldObjectSystem.FootprintEvent += (data, e) =>
        {
            if (e == CustomObjectEvent.Created)
            {
                LatlngFilterObject.AddCollisionPolygon(data.Footprint);
            }
        };
    }
}
