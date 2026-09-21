using System;
using System.Collections.Generic;
using DependencyAnalyzer.Editor.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    public sealed partial class DependencyGraphView
    {

        private void RebuildSearchIndex(string preferredNodeId)
        {
            searchMatchNodeIds.Clear();
            searchResultNodeIds.Clear();

            if (graph == null || string.IsNullOrWhiteSpace(searchQuery))
            {
                currentSearchResultIndex = -1;
                return;
            }

            var query = searchQuery.Trim();
            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                var node = graph.Nodes[i];
                if (!MatchesSearchQuery(node, query))
                {
                    continue;
                }

                searchResultNodeIds.Add(node.Id);
                searchMatchNodeIds.Add(node.Id);
            }

            if (searchResultNodeIds.Count == 0)
            {
                currentSearchResultIndex = -1;
                return;
            }

            var preferredIndex = string.IsNullOrEmpty(preferredNodeId)
                ? -1
                : searchResultNodeIds.IndexOf(preferredNodeId);
            if (preferredIndex >= 0)
            {
                currentSearchResultIndex = preferredIndex;
                return;
            }

            currentSearchResultIndex = Mathf.Clamp(currentSearchResultIndex, 0, searchResultNodeIds.Count - 1);
        }

        private static bool MatchesSearchQuery(DependencyNode node, string query)
        {
            return node != null
                && !string.IsNullOrEmpty(node.DisplayName)
                && !string.IsNullOrEmpty(query)
                && node.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void FocusCurrentSearchResult()
        {
            var nodeId = GetCurrentSearchNodeId();
            if (string.IsNullOrEmpty(nodeId))
            {
                Render();
                return;
            }

            FocusNode(nodeId, true);
        }

        private string GetCurrentSearchNodeId()
        {
            return currentSearchResultIndex >= 0 && currentSearchResultIndex < searchResultNodeIds.Count
                ? searchResultNodeIds[currentSearchResultIndex]
                : string.Empty;
        }

        private bool IsCurrentSearchNode(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId)
                && string.Equals(nodeId, GetCurrentSearchNodeId(), StringComparison.Ordinal);
        }

        private SearchResultState GetSearchResultState()
        {
            return new SearchResultState(currentSearchResultIndex, searchResultNodeIds.Count, BuildSearchSuggestions(MaxSearchSuggestions));
        }

        private IReadOnlyList<SearchSuggestion> BuildSearchSuggestions(int maxCount)
        {
            if (graph == null || searchResultNodeIds.Count == 0 || maxCount <= 0)
            {
                return Array.Empty<SearchSuggestion>();
            }

            var suggestions = new List<SearchSuggestion>(Mathf.Min(maxCount, searchResultNodeIds.Count));
            for (var i = 0; i < searchResultNodeIds.Count && suggestions.Count < maxCount; i++)
            {
                if (!graph.TryGetNode(searchResultNodeIds[i], out var node))
                {
                    continue;
                }

                suggestions.Add(new SearchSuggestion(
                    node.Id,
                    node.DisplayName,
                    BuildSuggestionDetail(node),
                    node.Path));
            }

            return suggestions;
        }

        private static string BuildSuggestionDetail(DependencyNode node)
        {
            var typeName = string.IsNullOrEmpty(node.TypeName) ? node.Kind.ToString() : node.TypeName;
            if (string.IsNullOrEmpty(node.Path))
            {
                return typeName;
            }

            return typeName + " - " + node.Path;
        }
    }
}
