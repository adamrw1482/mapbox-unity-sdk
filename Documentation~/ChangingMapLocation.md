## How to change map location

To be able to jump to a certain location during runtime, we’ll be using ChangeView method under MapboxMap class;
`public void ChangeView(LatitudeLongitude? latlng = null, float? zoom = null, float? pitch = null, float? bearing = null)`
This method will change the map values with the provided ones and trigger a redraw of the map.

First step will be getting the map object, you can read more about that in [Working with map object](WorkingWithMapObject.md) short tutorial.

Once you have the map object, you can call the method directly. A sample script is as follows;

```
public class ChangeLocation : MonoBehaviour
{
    public MapBehaviourCore Core;
    private MapboxMap _map;
    
    private void Awake()
    {
        Core.Initialized += map =>
        {
            _map = map;
        };
    }
    
    public void ChangeLocationTo(string location)
    {
        _map.ChangeView(Conversions.StringToLatLon(location));
    }
}
```