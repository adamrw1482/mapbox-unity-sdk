using System;
using Mapbox.SearchApi.Response;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mapbox.SearchApi.Demo
{
    /// <summary>
    /// A single row in the autocomplete suggestions dropdown.
    /// Bind a <see cref="Suggestion"/> via <see cref="Bind"/> and assign an onClick handler.
    /// </summary>
    public class SearchResultRow : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Primary text showing the result name.")]
        private TMP_Text _nameText;

        [SerializeField]
        [Tooltip("Secondary text showing the formatted place context (city, region, country).")]
        private TMP_Text _subtitleText;

        [SerializeField]
        [Tooltip("The button that triggers row selection.")]
        private Button _button;

        private Action _onClick;

        /// <summary>
        /// Populate this row with suggestion data and wire the selection callback.
        /// </summary>
        /// <param name="suggestion">The suggestion data to display.</param>
        /// <param name="onClick">Called when the user taps/clicks this row.</param>
        public void Bind(Suggestion suggestion, Action onClick)
        {
            if (_nameText != null)
                _nameText.text = suggestion.Name ?? string.Empty;

            if (_subtitleText != null)
                _subtitleText.text = suggestion.PlaceFormatted ?? suggestion.FullAddress ?? string.Empty;

            _onClick = onClick;

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(HandleClick);
            }
        }

        private void HandleClick() => _onClick?.Invoke();
    }
}
