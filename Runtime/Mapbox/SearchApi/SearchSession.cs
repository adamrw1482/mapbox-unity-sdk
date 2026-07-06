//-----------------------------------------------------------------------
// <copyright file="SearchSession.cs" company="Mapbox">
//     Copyright (c) 2024 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using Mapbox.BaseModule.Data.Platform;

namespace Mapbox.SearchApi
{
    /// <summary>
    /// Manages a Search Box API session: session token lifecycle, debounce, and
    /// in-flight request cancellation.
    ///
    /// Per the API spec, a session ends (and the token rotates) when:
    /// <list type="bullet">
    ///   <item><c>/suggest</c> is followed by <c>/retrieve</c></item>
    ///   <item>180 seconds elapse with no <c>/suggest</c> call</item>
    ///   <item>50 consecutive <c>/suggest</c> calls share the same token</item>
    /// </list>
    /// Each completed session is billed as one unit.
    /// </summary>
    public class SearchSession
    {
        private const int MaxSuggestionsPerSession = 50;
        private const float SessionIdleTimeoutSeconds = 180f;

        private string _sessionToken;
        private int    _suggestCount;
        private float  _lastSuggestTime;
        private IAsyncRequest _pendingRequest;

        /// <summary>The current UUIDv4 session token. Passed to suggest and retrieve calls.</summary>
        public string SessionToken => _sessionToken;

        /// <summary>Creates a new session and generates the first session token.</summary>
        public SearchSession()
        {
            Rotate();
        }

        /// <summary>
        /// Call this after a successful <c>/suggest</c> response to update session state.
        /// Rotates the token if the per-session suggest limit is reached.
        /// </summary>
        public void OnSuggestIssued(float currentTimeSec)
        {
            _suggestCount++;
            _lastSuggestTime = currentTimeSec;
            if (_suggestCount >= MaxSuggestionsPerSession)
                Rotate();
        }

        /// <summary>
        /// Call this after a successful <c>/retrieve</c> response.
        /// Ends the current session and rotates the token.
        /// </summary>
        public void RotateAfterRetrieve() => Rotate();

        /// <summary>
        /// Checks whether the idle timeout has expired. Call once per frame (or at debounce time)
        /// and rotate if <c>true</c> is returned.
        /// </summary>
        public bool IsIdleTimeoutExpired(float currentTimeSec)
            => _suggestCount > 0
            && (currentTimeSec - _lastSuggestTime) >= SessionIdleTimeoutSeconds;

        /// <summary>Cancel the currently in-flight request, if any.</summary>
        public void CancelPendingRequest()
        {
            if (_pendingRequest != null && !_pendingRequest.IsCompleted)
                _pendingRequest.Cancel();
            _pendingRequest = null;
        }

        /// <summary>Store a reference to the latest in-flight request for later cancellation.</summary>
        public void SetPendingRequest(IAsyncRequest request)
        {
            CancelPendingRequest();
            _pendingRequest = request;
        }

        /// <summary>Generate a new UUIDv4 session token and reset session counters.</summary>
        public void Rotate()
        {
            _sessionToken    = Guid.NewGuid().ToString();
            _suggestCount    = 0;
            _lastSuggestTime = 0f;
        }
    }
}
