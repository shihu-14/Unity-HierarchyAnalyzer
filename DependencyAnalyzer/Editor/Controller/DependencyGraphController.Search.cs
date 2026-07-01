using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.Editor.Controller.Issues;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Settings;
using DependencyAnalyzer.Editor.UI.Controls;
using DependencyAnalyzer.Editor.UI.GraphView;
using DependencyAnalyzer.Editor.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Controller
{
    public sealed partial class DependencyGraphController
    {
        private void HandleSearchChanged(ChangeEvent<string> evt)
        {
            UpdateSearchState(graphView.SetSearch(evt.newValue, IsSearchFilterEnabled(), true));
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

        private void HandleSearchFilterChanged(ChangeEvent<bool> evt)
        {
            UpdateSearchState(graphView.SetSearch(GetSearchQuery(), evt.newValue, true));
        }

        private void HandleSearchPreviousClicked()
        {
            UpdateSearchState(graphView.FocusNextSearchResult(true));
        }

        private void HandleSearchNextClicked()
        {
            UpdateSearchState(graphView.FocusNextSearchResult(false));
        }

        private void HandleSearchKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                UpdateSearchState(graphView.FocusNextSearchResult((evt.modifiers & EventModifiers.Shift) != 0));
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

        private bool IsSearchFilterEnabled()
        {
            return searchFilterToggle != null && searchFilterToggle.value;
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

        private void UpdateSearchState(DependencyGraphView.SearchResultState state)
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
            UpdateSearchState(graphView.SetSearch(suggestion.DisplayName, IsSearchFilterEnabled(), false));
            graphView.FocusNode(suggestion.NodeId, true);
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
