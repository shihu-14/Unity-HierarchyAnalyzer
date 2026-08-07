using System;
using System.Collections.Generic;
using System.Linq;

namespace DependencyAnalyzer.Editor.Core
{
    [Serializable]
    public sealed class DependencyGraph
    {
        private readonly List<DependencyNode> nodes = new List<DependencyNode>();
        private readonly List<DependencyEdge> edges = new List<DependencyEdge>();
        private readonly List<DependencyScanIssue> issues = new List<DependencyScanIssue>();
        private readonly Dictionary<string, DependencyNode> nodeLookup = new Dictionary<string, DependencyNode>();
        private readonly HashSet<string> edgeKeys = new HashSet<string>();

        public IReadOnlyList<DependencyNode> Nodes => nodes;
        public IReadOnlyList<DependencyEdge> Edges => edges;
        /// <summary>
        /// Gets Analyzer diagnostics collected while building this graph.
        /// Broken project references are represented by missing-reference edges instead.
        /// </summary>
        public IReadOnlyList<DependencyScanIssue> Issues => issues;

        public DependencyNode AddOrUpdateNode(DependencyNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            DependencyNode existing;
            if (nodeLookup.TryGetValue(node.Id, out existing))
            {
                if (node.HasMissingReferences)
                {
                    existing.MarkMissingReferences();
                }

                return existing;
            }

            nodeLookup.Add(node.Id, node);
            nodes.Add(node);
            return node;
        }

        public bool TryGetNode(string nodeId, out DependencyNode node)
        {
            return nodeLookup.TryGetValue(nodeId, out node);
        }

        public bool AddEdge(DependencyEdge edge)
        {
            if (edge == null || string.IsNullOrEmpty(edge.SourceNodeId) || string.IsNullOrEmpty(edge.TargetNodeId))
            {
                return false;
            }

            if (!edgeKeys.Add(edge.StableKey))
            {
                return false;
            }

            edges.Add(edge);
            if (edge.PointsToMissingReference && nodeLookup.TryGetValue(edge.SourceNodeId, out var source))
            {
                source.MarkMissingReferences();
            }

            return true;
        }

        /// <summary>
        /// Adds an Analyzer diagnostic without converting it into a user-facing project issue.
        /// </summary>
        public void AddIssue(DependencyScanIssue issue)
        {
            if (issue != null)
            {
                issues.Add(issue);
            }
        }

        public IEnumerable<DependencyEdge> GetOutgoingEdges(string nodeId)
        {
            return edges.Where(edge => edge.SourceNodeId == nodeId);
        }

        public IEnumerable<DependencyEdge> GetIncomingEdges(string nodeId)
        {
            return edges.Where(edge => edge.TargetNodeId == nodeId);
        }

        public void MergeFrom(DependencyGraph other)
        {
            if (other == null)
            {
                return;
            }

            foreach (var node in other.Nodes)
            {
                AddOrUpdateNode(node);
            }

            foreach (var edge in other.Edges)
            {
                AddEdge(edge);
            }

            foreach (var issue in other.Issues)
            {
                AddIssue(issue);
            }
        }

        public void RecalculateReferenceCounts()
        {
            var dependencyCounts = edges
                .GroupBy(edge => edge.SourceNodeId)
                .ToDictionary(group => group.Key, group => group.Select(edge => edge.TargetNodeId).Distinct().Count());
            var usedByCounts = edges
                .GroupBy(edge => edge.TargetNodeId)
                .ToDictionary(group => group.Key, group => group.Select(edge => edge.SourceNodeId).Distinct().Count());

            foreach (var node in nodes)
            {
                dependencyCounts.TryGetValue(node.Id, out var dependencyCount);
                usedByCounts.TryGetValue(node.Id, out var usedByCount);
                node.SetReferenceCounts(dependencyCount, usedByCount);
            }
        }
    }
}
