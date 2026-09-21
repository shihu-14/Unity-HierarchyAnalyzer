using System;
using System.Collections.Generic;
using DependencyAnalyzer.Editor.UI.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.Controls
{
    internal sealed class GraphSearchView : IDisposable
    {
        private readonly VisualElement root;
        private readonly VisualElement searchControl;
        private readonly TextField searchField;
        private readonly SearchIconElement searchIcon;
        private readonly VisualElement searchSuggestionList;
        private readonly Button searchPreviousButton;
        private readonly Button searchNextButton;
        private readonly Label searchCountLabel;
        private bool isSearchFieldFocused;

        public GraphSearchView(VisualElement root)
        {
            this.root = root;
            searchControl = root.Q("search-control");
            searchField = root.Q<TextField>("search-field");
            searchIcon = EnsureSearchIcon(root.Q("search-field-wrap"));
            searchSuggestionList = root.Q("search-suggestion-list");
            searchPreviousButton = root.Q<Button>("search-previous-button");
            searchNextButton = root.Q<Button>("search-next-button");
            searchCountLabel = root.Q<Label>("search-count-label");
            if (searchField != null)
            {
                searchField.RegisterValueChangedCallback(HandleSearchChanged);
                searchField.RegisterCallback<KeyDownEvent>(HandleSearchKeyDown, TrickleDown.TrickleDown);
                searchField.RegisterCallback<FocusInEvent>(HandleSearchFocusIn);
                searchField.RegisterCallback<FocusOutEvent>(HandleSearchFocusOut);
            }

            searchIcon?.RegisterCallback<MouseDownEvent>(HandleSearchIconMouseDown);
            ConfigureArrow(searchPreviousButton, true);
            ConfigureArrow(searchNextButton, false);
            if (searchPreviousButton != null)
            {
                searchPreviousButton.clicked += HandleSearchPreviousClicked;
            }

            if (searchNextButton != null)
            {
                searchNextButton.clicked += HandleSearchNextClicked;
            }

            root.RegisterCallback<KeyDownEvent>(HandleGlobalKeyDown, TrickleDown.TrickleDown);
            root.RegisterCallback<MouseDownEvent>(HandleRootMouseDown, TrickleDown.TrickleDown);
            UpdateSearchIconVisibility();
            SetSearchState(new DependencyGraphView.SearchResultState(-1, 0));
        }

        public event Action<string> QueryChanged;
        public event Action<bool> NavigationRequested;
        public event Action<DependencyGraphView.SearchSuggestion> SuggestionSelected;
        public string Query => GetSearchQuery();

        public void Dispose()
        {
            if (searchField != null)
            {
                searchField.UnregisterValueChangedCallback(HandleSearchChanged);
                searchField.UnregisterCallback<KeyDownEvent>(HandleSearchKeyDown, TrickleDown.TrickleDown);
                searchField.UnregisterCallback<FocusInEvent>(HandleSearchFocusIn);
                searchField.UnregisterCallback<FocusOutEvent>(HandleSearchFocusOut);
            }

            searchIcon?.UnregisterCallback<MouseDownEvent>(HandleSearchIconMouseDown);
            if (searchPreviousButton != null)
            {
                searchPreviousButton.clicked -= HandleSearchPreviousClicked;
            }

            if (searchNextButton != null)
            {
                searchNextButton.clicked -= HandleSearchNextClicked;
            }

            root.UnregisterCallback<KeyDownEvent>(HandleGlobalKeyDown, TrickleDown.TrickleDown);
            root.UnregisterCallback<MouseDownEvent>(HandleRootMouseDown, TrickleDown.TrickleDown);
            searchSuggestionList?.Clear();
            QueryChanged = null;
            NavigationRequested = null;
            SuggestionSelected = null;
        }

        private static void ConfigureArrow(Button button, bool previous)
        {
            if (button == null)
            {
                return;
            }

            button.text = string.Empty;
            button.Clear();
            var icon = new ChevronIcon(previous, 0.45f);
            icon.StretchToParentSize();
            button.Add(icon);
            button.tooltip = previous ? "Previous" : "Next";
        }

        private void HandleSearchChanged(ChangeEvent<string> evt)
        {
            QueryChanged?.Invoke(evt.newValue);
            UpdateSearchIconVisibility();
        }

        private void HandleSearchFocusIn(FocusInEvent evt)
        {
            isSearchFieldFocused = true;
            UpdateSearchIconVisibility();
        }

        private void HandleSearchFocusOut(FocusOutEvent evt)
        {
            isSearchFieldFocused = false;
            UpdateSearchIconVisibility();
        }

        private void HandleSearchIconMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            searchField?.Focus();
            evt.PreventDefault();
            evt.StopPropagation();
        }

        private void HandleSearchPreviousClicked()
        {
            NavigationRequested?.Invoke(true);
        }

        private void HandleSearchNextClicked()
        {
            NavigationRequested?.Invoke(false);
        }

        private void HandleSearchKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                NavigationRequested?.Invoke((evt.modifiers & EventModifiers.Shift) != 0);
                evt.PreventDefault();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Escape)
            {
                if (searchField != null)
                {
                    searchField.value = string.Empty;
                }

                HideSearchSuggestions();
                evt.PreventDefault();
                evt.StopPropagation();
            }
        }

        private void HandleGlobalKeyDown(KeyDownEvent evt)
        {
            var isFindShortcut = evt.keyCode == KeyCode.F
                && ((evt.modifiers & EventModifiers.Command) != 0 || (evt.modifiers & EventModifiers.Control) != 0);
            if (!isFindShortcut || searchField == null)
            {
                return;
            }

            searchField.Focus();
            evt.PreventDefault();
            evt.StopPropagation();
        }

        private string GetSearchQuery()
        {
            return searchField == null ? string.Empty : searchField.value;
        }

        private static SearchIconElement EnsureSearchIcon(VisualElement searchFieldWrap)
        {
            if (searchFieldWrap == null)
            {
                return null;
            }

            var existing = searchFieldWrap.Q<SearchIconElement>("search-icon");
            if (existing != null)
            {
                return existing;
            }

            var icon = new SearchIconElement { name = "search-icon" };
            icon.AddToClassList("dependency-search-icon");
            icon.tooltip = "Focus search";
            searchFieldWrap.Add(icon);
            icon.BringToFront();
            return icon;
        }

        private void UpdateSearchIconVisibility()
        {
            if (searchIcon == null)
            {
                return;
            }

            searchIcon.style.display = !isSearchFieldFocused && string.IsNullOrEmpty(GetSearchQuery())
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        public void SetSearchState(DependencyGraphView.SearchResultState state)
        {
            if (searchCountLabel != null)
            {
                searchCountLabel.text = state.DisplayIndex + " / " + state.Total;
            }

            var hasResults = state.Total > 0;
            searchPreviousButton?.SetEnabled(hasResults);
            searchNextButton?.SetEnabled(hasResults);
            UpdateSearchSuggestions(state.Suggestions);
        }

        private void UpdateSearchSuggestions(IReadOnlyList<DependencyGraphView.SearchSuggestion> suggestions)
        {
            if (searchSuggestionList == null)
            {
                return;
            }

            searchSuggestionList.Clear();
            if (string.IsNullOrWhiteSpace(GetSearchQuery()) || suggestions == null || suggestions.Count == 0)
            {
                HideSearchSuggestions();
                return;
            }

            for (var i = 0; i < suggestions.Count; i++)
            {
                var suggestion = suggestions[i];
                var row = new VisualElement();
                row.AddToClassList("dependency-search-suggestion-row");
                row.tooltip = suggestion.Path;
                row.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0)
                    {
                        return;
                    }

                    SelectSearchSuggestion(suggestion);
                    evt.PreventDefault();
                    evt.StopPropagation();
                });

                var title = new Label(suggestion.DisplayName);
                title.AddToClassList("dependency-search-suggestion-title");
                var detail = new Label(suggestion.Detail);
                detail.AddToClassList("dependency-search-suggestion-detail");
                row.Add(title);
                row.Add(detail);
                searchSuggestionList.Add(row);
            }

            searchSuggestionList.style.display = DisplayStyle.Flex;
        }

        private void SelectSearchSuggestion(DependencyGraphView.SearchSuggestion suggestion)
        {
            if (searchField == null)
            {
                return;
            }

            searchField.SetValueWithoutNotify(suggestion.DisplayName);
            SuggestionSelected?.Invoke(suggestion);
            HideSearchSuggestions();
            searchField.Focus();
        }

        private void HideSearchSuggestions()
        {
            if (searchSuggestionList != null)
            {
                searchSuggestionList.style.display = DisplayStyle.None;
            }
        }

        private void HandleRootMouseDown(MouseDownEvent evt)
        {
            if (searchField == null || searchControl == null)
            {
                return;
            }

            var target = evt.target as VisualElement;
            if (IsDescendantOf(target, searchControl))
            {
                return;
            }

            HideSearchSuggestions();
            searchField.Blur();
        }

        private static bool IsDescendantOf(VisualElement element, VisualElement ancestor)
        {
            var current = element;
            while (current != null)
            {
                if (current == ancestor)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

    }
}
