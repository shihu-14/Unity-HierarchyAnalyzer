using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using UnityEngine;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    internal sealed class GraphViewIndex
    {
        private readonly Dictionary<string, List<DependencyEdge>> outgoingEdgesByNodeId = new Dictionary<string, List<DependencyEdge>>();
        private readonly Dictionary<string, List<DependencyEdge>> incomingEdgesByNodeId = new Dictionary<string, List<DependencyEdge>>();
        private readonly Dictionary<string, List<DependencyEdge>> treeOutgoingEdgesByNodeId = new Dictionary<string, List<DependencyEdge>>();
        private readonly Dictionary<string, List<DependencyEdge>> regularTreeOutgoingEdgesByNodeId = new Dictionary<string, List<DependencyEdge>>();
        private readonly Dictionary<string, List<DependencyEdge>> menuTreeOutgoingEdgesByNodeId = new Dictionary<string, List<DependencyEdge>>();
        private readonly Dictionary<int, DependencyNode> nodeByInstanceId = new Dictionary<int, DependencyNode>();
        private readonly Dictionary<string, int> minimumRegularDepths = new Dictionary<string, int>();
        private readonly List<DependencyNode> rootNodes = new List<DependencyNode>();
        private static readonly IReadOnlyList<DependencyEdge> EmptyEdges = Array.Empty<DependencyEdge>();
        private DependencyGraph graph;

        public IReadOnlyList<DependencyNode> RootNodes => rootNodes;
        public IReadOnlyDictionary<string, int> MinimumRegularDepths => minimumRegularDepths;

        public bool TryGetNodeByInstanceId(int instanceId, out DependencyNode node)
        {
            return nodeByInstanceId.TryGetValue(instanceId, out node);
        }

        public void Rebuild(DependencyGraph graph)
        {
            this.graph = graph;
            outgoingEdgesByNodeId.Clear();
            incomingEdgesByNodeId.Clear();
            treeOutgoingEdgesByNodeId.Clear();
            regularTreeOutgoingEdgesByNodeId.Clear();
            menuTreeOutgoingEdgesByNodeId.Clear();
            nodeByInstanceId.Clear();
            minimumRegularDepths.Clear();
            rootNodes.Clear();

            if (graph == null)
            {
                return;
            }

            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                var node = graph.Nodes[i];
                if (node.InstanceId != 0 && !nodeByInstanceId.ContainsKey(node.InstanceId))
                {
                    nodeByInstanceId.Add(node.InstanceId, node);
                }
            }

            for (var i = 0; i < graph.Edges.Count; i++)
            {
                var edge = graph.Edges[i];
                if (edge == null)
                {
                    continue;
                }

                if (!outgoingEdgesByNodeId.TryGetValue(edge.SourceNodeId, out var outgoingEdges))
                {
                    outgoingEdges = new List<DependencyEdge>();
                    outgoingEdgesByNodeId.Add(edge.SourceNodeId, outgoingEdges);
                }

                outgoingEdges.Add(edge);

                if (!incomingEdgesByNodeId.TryGetValue(edge.TargetNodeId, out var incomingEdges))
                {
                    incomingEdges = new List<DependencyEdge>();
                    incomingEdgesByNodeId.Add(edge.TargetNodeId, incomingEdges);
                }

                incomingEdges.Add(edge);
            }

            foreach (var pair in outgoingEdgesByNodeId)
            {
                var treeEdges = pair.Value
                    .Where(edge => graph.TryGetNode(edge.TargetNodeId, out _))
                    .GroupBy(edge => edge.TargetNodeId)
                    .Select(group => group
                        .OrderBy(edge => GetEdgeSortPriority(edge.ReferenceKind))
                        .ThenBy(edge => edge.MemberName, StringComparer.OrdinalIgnoreCase)
                        .First())
                    .OrderBy(edge => GetNodeSortName(edge.TargetNodeId), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(edge => GetNodeSortType(edge.TargetNodeId), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(edge => GetEdgeSortPriority(edge.ReferenceKind))
                    .ThenBy(edge => edge.MemberName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(edge => edge.TargetNodeId, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                treeOutgoingEdgesByNodeId[pair.Key] = treeEdges;
                var regularEdges = new List<DependencyEdge>();
                var menuEdges = new List<DependencyEdge>();
                for (var i = 0; i < treeEdges.Count; i++)
                {
                    var edge = treeEdges[i];
                    if (IsMenuEdge(edge))
                    {
                        menuEdges.Add(edge);
                    }
                    else
                    {
                        regularEdges.Add(edge);
                    }
                }

                regularTreeOutgoingEdgesByNodeId[pair.Key] = regularEdges;
                menuTreeOutgoingEdgesByNodeId[pair.Key] = menuEdges;
            }

            rootNodes.AddRange(graph.Nodes
                .Where(IsRootNodeFromCache)
                .OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase));

            foreach (var pair in ComputeMinimumRegularDepths(rootNodes))
            {
                minimumRegularDepths[pair.Key] = pair.Value;
            }
        }

        private bool IsRootNodeFromCache(DependencyNode node)
        {
            if (node.Kind != DependencyNodeKind.SceneObject
                || node.IsMissingTarget
                || node.NamespaceQualifiedTypeName != typeof(GameObject).FullName
                || node.InstanceId == 0)
            {
                return false;
            }

            var incomingEdges = GetIncomingEdges(node.Id);
            for (var i = 0; i < incomingEdges.Count; i++)
            {
                if (incomingEdges[i].ReferenceKind == DependencyReferenceKind.Hierarchy)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsMenuEdge(DependencyEdge edge)
        {
            return edge != null && edge.ReferenceKind == DependencyReferenceKind.SerializedProperty;
        }

        private Dictionary<string, int> ComputeMinimumRegularDepths(IReadOnlyList<DependencyNode> roots)
        {
            var minimumDepths = new Dictionary<string, int>();
            var queue = new Queue<NodeDepth>();
            for (var i = 0; i < roots.Count; i++)
            {
                if (roots[i] != null)
                {
                    queue.Enqueue(new NodeDepth(roots[i].Id, 0));
                }
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (minimumDepths.TryGetValue(current.NodeId, out var knownDepth) && knownDepth <= current.Depth)
                {
                    continue;
                }

                minimumDepths[current.NodeId] = current.Depth;
                foreach (var edge in GetRegularTreeOutgoingEdges(current.NodeId))
                {
                    queue.Enqueue(new NodeDepth(edge.TargetNodeId, current.Depth + 1));
                }
            }

            return minimumDepths;
        }

        public IReadOnlyList<DependencyEdge> GetTreeOutgoingEdges(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) && treeOutgoingEdgesByNodeId.TryGetValue(nodeId, out var edges)
                ? edges
                : EmptyEdges;
        }

        public IReadOnlyList<DependencyEdge> GetRegularTreeOutgoingEdges(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) && regularTreeOutgoingEdgesByNodeId.TryGetValue(nodeId, out var edges)
                ? edges
                : EmptyEdges;
        }

        public IReadOnlyList<DependencyEdge> GetMenuTreeOutgoingEdges(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) && menuTreeOutgoingEdgesByNodeId.TryGetValue(nodeId, out var edges)
                ? edges
                : EmptyEdges;
        }

        public IReadOnlyList<DependencyEdge> GetOutgoingEdges(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) && outgoingEdgesByNodeId.TryGetValue(nodeId, out var edges)
                ? edges
                : EmptyEdges;
        }

        public IReadOnlyList<DependencyEdge> GetIncomingEdges(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) && incomingEdgesByNodeId.TryGetValue(nodeId, out var edges)
                ? edges
                : EmptyEdges;
        }

        private string GetNodeSortName(string nodeId)
        {
            return graph != null && graph.TryGetNode(nodeId, out var node)
                ? node.DisplayName
                : string.Empty;
        }

        private string GetNodeSortType(string nodeId)
        {
            return graph != null && graph.TryGetNode(nodeId, out var node)
                ? node.TypeName
                : string.Empty;
        }

        public static int GetEdgeSortPriority(DependencyReferenceKind kind)
        {
            switch (kind)
            {
                case DependencyReferenceKind.Hierarchy:
                    return 0;
                case DependencyReferenceKind.Component:
                    return 1;
                case DependencyReferenceKind.SerializedProperty:
                    return 2;
                case DependencyReferenceKind.Issue:
                    return 3;
                case DependencyReferenceKind.PrefabInstance:
                    return 4;
                default:
                    return 5;
            }
        }

    }
}
