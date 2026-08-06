using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    public sealed partial class DependencyGraphView
    {

        private void RebuildSearchIndex(string preferredNodeId)
        {
            searchMatchNodeIds.Clear();
            searchVisibleNodeIds.Clear();
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

            for (var i = 0; i < searchResultNodeIds.Count; i++)
            {
                AddNodeAndAncestors(searchResultNodeIds[i], searchVisibleNodeIds);
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
            if (node == null || string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            var terms = query.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < terms.Length; i++)
            {
                if (!MatchesSearchTerm(node, terms[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesSearchTerm(DependencyNode node, string term)
        {
            var separatorIndex = term.IndexOf(':');
            if (separatorIndex > 0 && separatorIndex < term.Length - 1)
            {
                var key = term.Substring(0, separatorIndex).Trim();
                var value = term.Substring(separatorIndex + 1).Trim();
                if (string.Equals(key, "name", StringComparison.OrdinalIgnoreCase))
                {
                    return ContainsSearchText(node.DisplayName, value);
                }

                if (string.Equals(key, "path", StringComparison.OrdinalIgnoreCase))
                {
                    return ContainsSearchText(node.Path, value);
                }

                if (string.Equals(key, "type", StringComparison.OrdinalIgnoreCase))
                {
                    return ContainsSearchText(node.TypeName, value)
                        || ContainsSearchText(node.NamespaceQualifiedTypeName, value);
                }

                if (string.Equals(key, "label", StringComparison.OrdinalIgnoreCase))
                {
                    return ContainsSearchText(node.LabelsText, value);
                }

                if (string.Equals(key, "kind", StringComparison.OrdinalIgnoreCase))
                {
                    return ContainsSearchText(node.Kind.ToString(), value);
                }

                if (string.Equals(key, "missing", StringComparison.OrdinalIgnoreCase))
                {
                    return MatchesMissingFilter(node, value);
                }
            }

            return ContainsSearchText(BuildSearchText(node), term);
        }

        private static bool MatchesMissingFilter(DependencyNode node, string value)
        {
            var hasMissing = node.Kind == DependencyNodeKind.MissingReference || node.HasMissingReferences;
            if (string.IsNullOrWhiteSpace(value))
            {
                return hasMissing;
            }

            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                || value == "1")
            {
                return hasMissing;
            }

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase)
                || value == "0")
            {
                return !hasMissing;
            }

            return ContainsSearchText(hasMissing ? "missing true" : "missing false", value);
        }

        private static string BuildSearchText(DependencyNode node)
        {
            return node.DisplayName
                + "\n" + node.Path
                + "\n" + node.TypeName
                + "\n" + node.NamespaceQualifiedTypeName
                + "\n" + node.LabelsText
                + "\n" + node.Kind
                + "\n" + (node.HasMissingReferences || node.Kind == DependencyNodeKind.MissingReference ? "missing" : "valid");
        }

        private static bool ContainsSearchText(string source, string value)
        {
            return !string.IsNullOrEmpty(source)
                && !string.IsNullOrEmpty(value)
                && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AddNodeAndAncestors(string nodeId, HashSet<string> target)
        {
            if (graph == null || string.IsNullOrEmpty(nodeId) || target == null)
            {
                return;
            }

            var stack = new Stack<string>();
            stack.Push(nodeId);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (string.IsNullOrEmpty(current) || !target.Add(current))
                {
                    continue;
                }

                foreach (var edge in GetIncomingEdges(current))
                {
                    if (!string.IsNullOrEmpty(edge.SourceNodeId))
                    {
                        stack.Push(edge.SourceNodeId);
                    }
                }
            }
        }

        private void AddForcedVisiblePath(string nodeId)
        {
            if (!IsSearchFilteringActive())
            {
                return;
            }

            AddNodeAndAncestors(nodeId, forcedVisibleNodeIds);
        }

        private bool IsSearchFilteringActive()
        {
            return searchFilterEnabled && !string.IsNullOrWhiteSpace(searchQuery);
        }

        private bool IsSearchVisibleNode(string nodeId)
        {
            return searchVisibleNodeIds.Contains(nodeId) || forcedVisibleNodeIds.Contains(nodeId);
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
