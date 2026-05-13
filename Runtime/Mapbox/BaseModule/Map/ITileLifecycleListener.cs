namespace Mapbox.BaseModule.Map
{
    /// <summary>
    /// Opt-in extension for <see cref="Mapbox.BaseModule.Data.Interfaces.ILayerModule"/>
    /// implementations that need a reference to the owning visualizer (e.g. to subscribe
    /// to <c>TileLoaded</c> / <c>TileUnloading</c> events for bookkeeping that depends on
    /// tile lifecycle).
    ///
    /// <see cref="MapboxMapVisualizer.Initialize"/> calls <see cref="AttachToMapVisualizer"/>
    /// on every layer module that implements this interface, after the modules are added
    /// but before their own <c>Initialize</c> runs. Modules that don't need the
    /// visualizer reference simply don't implement this interface.
    /// </summary>
    public interface ITileLifecycleListener
    {
        void AttachToMapVisualizer(MapboxMapVisualizer visualizer);
    }
}
