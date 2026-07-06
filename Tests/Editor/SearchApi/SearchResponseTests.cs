using Mapbox.BaseModule.Utilities.JsonConverters;
using Mapbox.SearchApi.Response;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Mapbox.SearchApiTests
{
    [TestFixture]
    public class SearchResponseTests
    {
        // ── Suggest response ───────────────────────────────────────────────────

        private const string SuggestJson = @"{
            ""suggestions"": [
                {
                    ""name"": ""Michigan Stadium"",
                    ""mapbox_id"": ""poi.abc123"",
                    ""feature_type"": ""poi"",
                    ""address"": ""1201 S Main St"",
                    ""full_address"": ""1201 S Main St, Ann Arbor, Michigan 48104, United States"",
                    ""place_formatted"": ""Ann Arbor, Michigan 48104, United States"",
                    ""language"": ""en"",
                    ""maki"": ""marker"",
                    ""poi_category"": [""sports""],
                    ""poi_category_ids"": [""sports""],
                    ""context"": {
                        ""country"": {
                            ""id"": ""country.us"",
                            ""name"": ""United States"",
                            ""country_code"": ""US"",
                            ""country_code_alpha_3"": ""USA""
                        },
                        ""region"": {
                            ""id"": ""region.mi"",
                            ""name"": ""Michigan"",
                            ""region_code"": ""MI"",
                            ""region_code_full"": ""US-MI""
                        },
                        ""place"": { ""id"": ""place.ann_arbor"", ""name"": ""Ann Arbor"" }
                    }
                }
            ],
            ""attribution"": ""© 2024 Mapbox""
        }";

        [Test]
        public void SuggestResponse_Deserializes_Suggestions()
        {
            var r = JsonConvert.DeserializeObject<SuggestResponse>(SuggestJson, JsonConverters.Converters);
            Assert.IsNotNull(r);
            Assert.IsNotNull(r.Suggestions);
            Assert.AreEqual(1, r.Suggestions.Count);
        }

        [Test]
        public void SuggestResponse_Suggestion_HasCorrectName()
        {
            var r = JsonConvert.DeserializeObject<SuggestResponse>(SuggestJson, JsonConverters.Converters);
            Assert.AreEqual("Michigan Stadium", r.Suggestions[0].Name);
        }

        [Test]
        public void SuggestResponse_Suggestion_HasMapboxId()
        {
            var r = JsonConvert.DeserializeObject<SuggestResponse>(SuggestJson, JsonConverters.Converters);
            Assert.AreEqual("poi.abc123", r.Suggestions[0].MapboxId);
        }

        [Test]
        public void SuggestResponse_Suggestion_HasPlaceFormatted()
        {
            var r = JsonConvert.DeserializeObject<SuggestResponse>(SuggestJson, JsonConverters.Converters);
            StringAssert.Contains("Ann Arbor", r.Suggestions[0].PlaceFormatted);
        }

        [Test]
        public void SuggestResponse_Suggestion_HasContext()
        {
            var r   = JsonConvert.DeserializeObject<SuggestResponse>(SuggestJson, JsonConverters.Converters);
            var ctx = r.Suggestions[0].Context;
            Assert.IsNotNull(ctx);
            Assert.AreEqual("US",      ctx.Country?.CountryCode);
            Assert.AreEqual("MI",      ctx.Region?.RegionCode);
            Assert.AreEqual("Ann Arbor", ctx.Place?.Name);
        }

        [Test]
        public void SuggestResponse_HasAttribution()
        {
            var r = JsonConvert.DeserializeObject<SuggestResponse>(SuggestJson, JsonConverters.Converters);
            StringAssert.Contains("Mapbox", r.Attribution);
        }

        // ── FeatureCollection (retrieve / forward / reverse / category) ────────

        private const string FeatureCollectionJson = @"{
            ""type"": ""FeatureCollection"",
            ""features"": [
                {
                    ""type"": ""Feature"",
                    ""geometry"": {
                        ""type"": ""Point"",
                        ""coordinates"": [-83.748708, 42.265837]
                    },
                    ""properties"": {
                        ""name"": ""Michigan Stadium"",
                        ""mapbox_id"": ""poi.abc123"",
                        ""feature_type"": ""poi"",
                        ""address"": ""1201 S Main St"",
                        ""full_address"": ""1201 S Main St, Ann Arbor, Michigan 48104, United States"",
                        ""place_formatted"": ""Ann Arbor, Michigan 48104, United States"",
                        ""coordinates"": {
                            ""longitude"": -83.748708,
                            ""latitude"": 42.265837,
                            ""accuracy"": ""rooftop""
                        },
                        ""maki"": ""marker"",
                        ""language"": ""en""
                    }
                }
            ],
            ""attribution"": ""© 2024 Mapbox""
        }";

        [Test]
        public void FeatureCollection_Deserializes_Features()
        {
            var fc = JsonConvert.DeserializeObject<SearchFeatureCollection>(FeatureCollectionJson, JsonConverters.Converters);
            Assert.IsNotNull(fc);
            Assert.AreEqual("FeatureCollection", fc.Type);
            Assert.AreEqual(1, fc.Features.Count);
        }

        [Test]
        public void FeatureCollection_Feature_HasCorrectName()
        {
            var fc = JsonConvert.DeserializeObject<SearchFeatureCollection>(FeatureCollectionJson, JsonConverters.Converters);
            Assert.AreEqual("Michigan Stadium", fc.Features[0].Properties.Name);
        }

        [Test]
        public void FeatureCollection_Geometry_CoordinatesConvertedToVector2d()
        {
            // LonLatToVector2dConverter: x=lat, y=lon
            var fc   = JsonConvert.DeserializeObject<SearchFeatureCollection>(FeatureCollectionJson, JsonConverters.Converters);
            var geom = fc.Features[0].Geometry;
            Assert.IsNotNull(geom);
            Assert.AreEqual("Point", geom.Type);
            Assert.AreEqual(42.265837,  geom.Coordinates.x, 0.00001, "x should be latitude");
            Assert.AreEqual(-83.748708, geom.Coordinates.y, 0.00001, "y should be longitude");
        }

        [Test]
        public void FeatureCollection_Properties_CoordinatesHaveLatLon()
        {
            var fc    = JsonConvert.DeserializeObject<SearchFeatureCollection>(FeatureCollectionJson, JsonConverters.Converters);
            var coords = fc.Features[0].Properties.Coordinates;
            Assert.IsNotNull(coords);
            Assert.AreEqual(42.265837,  coords.Latitude,  0.00001);
            Assert.AreEqual(-83.748708, coords.Longitude, 0.00001);
            Assert.AreEqual("rooftop",  coords.Accuracy);
        }

        // ── CategoryListResponse ───────────────────────────────────────────────

        private const string CategoryListJson = @"{
            ""listItems"": [
                { ""canonical_id"": ""coffee"", ""icon"": ""cafe"", ""name"": ""Coffee"" },
                { ""canonical_id"": ""restaurant"", ""icon"": ""restaurant"", ""name"": ""Restaurant"" }
            ],
            ""attribution"": ""© 2024 Mapbox"",
            ""version"": ""1.0""
        }";

        [Test]
        public void CategoryListResponse_Deserializes_Items()
        {
            var r = JsonConvert.DeserializeObject<CategoryListResponse>(CategoryListJson, JsonConverters.Converters);
            Assert.IsNotNull(r);
            Assert.AreEqual(2, r.ListItems.Count);
        }

        [Test]
        public void CategoryListResponse_FirstItem_HasCorrectFields()
        {
            var r = JsonConvert.DeserializeObject<CategoryListResponse>(CategoryListJson, JsonConverters.Converters);
            Assert.AreEqual("coffee", r.ListItems[0].CanonicalId);
            Assert.AreEqual("Coffee", r.ListItems[0].Name);
            Assert.AreEqual("cafe",   r.ListItems[0].Icon);
        }
    }
}
