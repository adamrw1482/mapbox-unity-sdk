//-----------------------------------------------------------------------
// <copyright file="MapboxSearchApi.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Text;
using Mapbox.BaseModule.Data.Platform;
using Mapbox.BaseModule.Utilities.JsonConverters;
using Mapbox.SearchApi.Response;
using Newtonsoft.Json;

namespace Mapbox.SearchApi
{
    /// <summary>
    /// Wrapper for the Mapbox Search Box API.
    /// Obtain an instance by passing an <see cref="IFileSource"/> from the running map:
    /// <code>new MapboxSearchApi(map.MapService.FileSource)</code>
    /// Access tokens and SKU tokens are appended automatically by the file source.
    /// </summary>
    public sealed class MapboxSearchApi
    {
        private readonly IFileSource _fileSource;

        /// <summary>Initializes a new instance of <see cref="MapboxSearchApi"/>.</summary>
        /// <param name="fileSource">
        /// The file source used to issue HTTP requests. Tokens are appended automatically.
        /// Obtain from <c>map.MapService.FileSource</c> or construct a standalone
        /// <c>FileSource</c> via <c>MapboxContext</c>.
        /// </param>
        public MapboxSearchApi(IFileSource fileSource)
        {
            _fileSource = fileSource ?? throw new ArgumentNullException(nameof(fileSource));
        }

        /// <summary>
        /// Request autocomplete suggestions for a partial query string.
        /// Use together with <see cref="Retrieve"/> to complete an interactive search session.
        /// </summary>
        public IAsyncRequest Suggest(SuggestResource resource, Action<SuggestResponse> callback)
            => Send(resource, callback);

        /// <summary>
        /// Retrieve full feature details (including coordinates) for a suggestion selected by the user.
        /// Always pair with a preceding <see cref="Suggest"/> call sharing the same session token.
        /// </summary>
        public IAsyncRequest Retrieve(RetrieveResource resource, Action<SearchFeatureCollection> callback)
            => Send(resource, callback);

        /// <summary>
        /// One-off forward text search. Returns a GeoJSON FeatureCollection with coordinates directly.
        /// Billed per request (not per session).
        /// </summary>
        public IAsyncRequest Forward(ForwardSearchResource resource, Action<SearchFeatureCollection> callback)
            => Send(resource, callback);

        /// <summary>
        /// Reverse lookup: returns addresses and POIs around the given coordinates.
        /// </summary>
        public IAsyncRequest Reverse(ReverseSearchResource resource, Action<SearchFeatureCollection> callback)
            => Send(resource, callback);

        /// <summary>
        /// Return POIs matching a canonical category ID (e.g. "coffee", "gas_station").
        /// </summary>
        public IAsyncRequest Category(CategorySearchResource resource, Action<SearchFeatureCollection> callback)
            => Send(resource, callback);

        /// <summary>
        /// Retrieve the full list of available POI categories with their canonical IDs.
        /// </summary>
        public IAsyncRequest ListCategories(ListCategoryResource resource, Action<CategoryListResponse> callback)
            => Send(resource, callback);

        private IAsyncRequest Send<T>(Resource resource, Action<T> callback)
        {
            return _fileSource.Request(
                resource.GetUrl(),
                response =>
                {
                    if (response.HasError)
                    {
                        UnityEngine.Debug.LogError(
                            $"[MapboxSearchApi] Request failed: {response.ExceptionsAsString}");
                        callback(default);
                        return;
                    }
                    if (response.Data == null || response.Data.Length == 0)
                    {
                        UnityEngine.Debug.LogError("[MapboxSearchApi] Response data is empty.");
                        callback(default);
                        return;
                    }
                    var json = Encoding.UTF8.GetString(response.Data);
                    var result = JsonConvert.DeserializeObject<T>(json, JsonConverters.Converters);
                    callback(result);
                },
                addSkuToken: false);
        }
    }
}
