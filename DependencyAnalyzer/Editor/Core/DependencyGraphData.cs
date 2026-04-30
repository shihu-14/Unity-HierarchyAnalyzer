using System;
using System.Collections.Generic;
using System.Linq;

namespace DependencyAnalyzer.Editor.Core
{
    [Serializable]
    public sealed class DependencyGraphData
    {
        private readonly List<DependencyNodeData> nodes = new List<DependencyNodeData>();
        private readonly List<DependencyEdgeData> edges = new List<DependencyEdgeData>();
        private readonly List<DependencyScanIssueData> issues = new List<DependencyScanIssueData>();
        private readonly Dictionary<string, DependencyNodeData> nodeLookup = new Dictionary<string, DependencyNodeData>();
        private readonly HashSet<string> edgeKeys = new HashSet<string>();

        public IReadOnlyList<DependencyNodeData> Nodes => nodes;
        public IReadOnlyList<DependencyEdgeData> Edges => edges;
        public IReadOnlyList<DependencyScanIssueData> Issues => issues;

        public DependencyNodeData AddOrUpdateNode(DependencyNodeData node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            DependencyNodeData existing;
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

        public bool TryGetNode(string nodeId, out DependencyNodeData node)
        {
            return nodeLookup.TryGetValue(nodeId, out node);
        }

        public bool AddEdge(DependencyEdgeData edge)
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

        public void AddIssue(DependencyScanIssueData issue)
        {
            if (issue != null)
            {
                issues.Add(issue);
            }
        }

        public IEnumerable<DependencyEdgeData> GetOutgoingEdges(string nodeId)
        {
            return edges.Where(edge => edge.SourceNodeId == nodeId);
        }

        public IEnumerable<DependencyEdgeData> GetIncomingEdges(string nodeId)
        {
            return edges.Where(edge => edge.TargetNodeId == nodeId);
        }

        public void MergeFrom(DependencyGraphData other)
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
