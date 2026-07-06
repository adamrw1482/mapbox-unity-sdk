using System.Collections;
using System.Collections.Generic;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.SearchApi.Demo;
using UnityEngine;

public class SearchResultController : MonoBehaviour
{
    public MapBehaviourCore MapBehaviourCore;
    
    public void SearchSelectionTriggered(SearchSelection selection)
    {
        Debug.Log("SearchSelectionTriggered " + selection.Coordinates);
        if (MapBehaviourCore.InitializationStatus >= InitializationStatus.Initialized)
        {
            MapBehaviourCore.MapboxMap.ChangeView(selection.Coordinates); 
        }
    }
}
