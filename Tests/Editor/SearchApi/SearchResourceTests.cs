using System;
using Mapbox.SearchApi;
using NUnit.Framework;

namespace Mapbox.SearchApiTests
{
    [TestFixture]
    public class SearchResourceTests
    {
        private const string ExpectedBase = "https://api.mapbox.com/search/searchbox/v1/";

        // ── SuggestResource ────────────────────────────────────────────────────

        [Test]
        public void Suggest_GetUrl_ContainsCorrectEndpoint()
        {
            var r = new SuggestResource("coffee", "session-abc");
            StringAssert.Contains("search/searchbox/v1/suggest", r.GetUrl());
        }

        [Test]
        public void Suggest_GetUrl_ContainsQuery()
        {
            var r = new SuggestResource("Michigan Stadium", "session-abc");
            StringAssert.Contains("q=", r.GetUrl());
            StringAssert.Contains("Michigan", r.GetUrl());
        }

        [Test]
        public void Suggest_GetUrl_ContainsSessionToken()
        {
            var r = new SuggestResource("coffee", "my-token-123");
            StringAssert.Contains("session_token=my-token-123", r.GetUrl());
        }

        [Test]
        public void Suggest_GetUrl_OmitsOptionalParamsWhenNotSet()
        {
            var r = new SuggestResource("coffee", "session-abc");
            var url = r.GetUrl();
            StringAssert.DoesNotContain("limit=", url);
            StringAssert.DoesNotContain("language=", url);
            StringAssert.DoesNotContain("country=", url);
            StringAssert.DoesNotContain("proximity=", url);
        }

        [Test]
        public void Suggest_GetUrl_IncludesOptionalParams()
        {
            var r = new SuggestResource("coffee", "session-abc")
            {
                Language = "fr",
                Limit    = 3,
                Country  = new[] { "fr" }
            };
            var url = r.GetUrl();
            StringAssert.Contains("language=fr", url);
            StringAssert.Contains("limit=3", url);
            StringAssert.Contains("country=fr", url);
        }

        [Test]
        public void Suggest_ThrowsOnNullQuery()
        {
            Assert.Throws<ArgumentException>(() => new SuggestResource(null, "session-abc"));
        }

        [Test]
        public void Suggest_ThrowsOnEmptySessionToken()
        {
            Assert.Throws<ArgumentException>(() => new SuggestResource("coffee", ""));
        }

        // ── RetrieveResource ───────────────────────────────────────────────────

        [Test]
        public void Retrieve_GetUrl_ContainsMapboxIdInPath()
        {
            var r = new RetrieveResource("poi.abc123", "session-abc");
            var url = r.GetUrl();
            StringAssert.Contains("search/searchbox/v1/retrieve/", url);
            StringAssert.Contains("poi.abc123", url);
        }

        [Test]
        public void Retrieve_GetUrl_ContainsSessionToken()
        {
            var r = new RetrieveResource("poi.abc123", "my-token");
            StringAssert.Contains("session_token=my-token", r.GetUrl());
        }

        [Test]
        public void Retrieve_ThrowsOnNullMapboxId()
        {
            Assert.Throws<ArgumentException>(() => new RetrieveResource(null, "session-abc"));
        }

        [Test]
        public void Retrieve_ThrowsOnNullSessionToken()
        {
            Assert.Throws<ArgumentException>(() => new RetrieveResource("poi.abc123", null));
        }

        // ── ForwardSearchResource ──────────────────────────────────────────────

        [Test]
        public void Forward_GetUrl_ContainsCorrectEndpoint()
        {
            var r = new ForwardSearchResource("1201 S Main St");
            StringAssert.Contains("search/searchbox/v1/forward", r.GetUrl());
        }

        [Test]
        public void Forward_GetUrl_ContainsQuery()
        {
            var r = new ForwardSearchResource("San Francisco");
            StringAssert.Contains("q=", r.GetUrl());
        }

        [Test]
        public void Forward_GetUrl_IncludesAutoComplete()
        {
            var r = new ForwardSearchResource("coffee") { AutoComplete = true };
            StringAssert.Contains("auto_complete=true", r.GetUrl());
        }

        [Test]
        public void Forward_ThrowsOnEmptyQuery()
        {
            Assert.Throws<ArgumentException>(() => new ForwardSearchResource(""));
        }

        // ── ReverseSearchResource ──────────────────────────────────────────────

        [Test]
        public void Reverse_GetUrl_ContainsLongitudeAndLatitude()
        {
            var r = new ReverseSearchResource(-83.748708, 42.265837);
            var url = r.GetUrl();
            StringAssert.Contains("search/searchbox/v1/reverse", url);
            StringAssert.Contains("longitude=", url);
            StringAssert.Contains("latitude=", url);
        }

        [Test]
        public void Reverse_LongitudeLatitudeAreQueryParams_NotPath()
        {
            var r = new ReverseSearchResource(-83.748708, 42.265837);
            var url = r.GetUrl();
            // path part should just be "reverse", coords only in query string
            var pathPart = url.Split('?')[0];
            StringAssert.EndsWith("reverse", pathPart);
        }

        [Test]
        public void Reverse_ThrowsOnOutOfRangeLongitude()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReverseSearchResource(200, 0));
        }

        [Test]
        public void Reverse_ThrowsOnOutOfRangeLatitude()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReverseSearchResource(0, 100));
        }

        // ── CategorySearchResource ─────────────────────────────────────────────

        [Test]
        public void Category_GetUrl_ContainsCategoryIdInPath()
        {
            var r = new CategorySearchResource("coffee");
            var url = r.GetUrl();
            StringAssert.Contains("search/searchbox/v1/category/", url);
            StringAssert.Contains("coffee", url);
        }

        [Test]
        public void Category_ThrowsOnNullCategoryId()
        {
            Assert.Throws<ArgumentException>(() => new CategorySearchResource(null));
        }

        // ── ListCategoryResource ───────────────────────────────────────────────

        [Test]
        public void ListCategory_GetUrl_ContainsCorrectEndpoint()
        {
            var r = new ListCategoryResource();
            StringAssert.Contains("search/searchbox/v1/list/category", r.GetUrl());
        }

        [Test]
        public void ListCategory_GetUrl_IncludesLanguageWhenSet()
        {
            var r = new ListCategoryResource { Language = "de" };
            StringAssert.Contains("language=de", r.GetUrl());
        }

        [Test]
        public void ListCategory_GetUrl_OmitsLanguageWhenNotSet()
        {
            var r = new ListCategoryResource();
            StringAssert.DoesNotContain("language=", r.GetUrl());
        }

        // ── Shared validation ──────────────────────────────────────────────────

        [Test]
        public void SearchBoxResource_ThrowsOnInvalidCountryCode()
        {
            var r = new SuggestResource("coffee", "session-abc");
            Assert.Throws<ArgumentException>(() => r.Country = new[] { "USA" }); // must be 2-char
        }

        [Test]
        public void SearchBoxResource_ThrowsOnInvalidType()
        {
            var r = new SuggestResource("coffee", "session-abc");
            Assert.Throws<ArgumentException>(() => r.Types = new[] { "invalid_type" });
        }
    }
}
