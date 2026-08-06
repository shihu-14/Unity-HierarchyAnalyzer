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

        private void Render()
        {
            var previousNodeRects = new Dictionary<string, Rect>(nodeRects);
            var previousSnapshots = new Dictionary<string, RenderSnapshot>();
            for (var i = 0; i < renderNodes.Count; i++)
            {
                var renderNode = renderNodes[i];
                previousSnapshots[renderNode.ViewId] = new RenderSnapshot(renderNode);
            }

            StopActiveAnimation();
            nodeLayer.Clear();
            edgeLayer.Clear();
            animationLayer.Clear();
            nodeViews.Clear();
            nodeRects.Clear();
            renderNodeByViewId.Clear();
            renderNodesByNodeId.Clear();
            renderNodes.Clear();

            if (graph == null || graph.Nodes.Count == 0)
            {
                emptyStateLabel.style.display = DisplayStyle.Flex;
                currentCanvasSize = Vector2.one;
                SetCanvasSize(currentCanvasSize.x, currentCanvasSize.y);
                ApplyTransform();
                return;
            }

            emptyStateLabel.style.display = DisplayStyle.None;

            var roots = BuildRenderTree();
            var canvasSize = LayoutNodes(roots, previousNodeRects);
            currentCanvasSize = canvasSize;
            SetCanvasSize(currentCanvasSize.x, currentCanvasSize.y);
            var finalNodeRects = new Dictionary<string, Rect>(nodeRects);
            AnimateLayoutTransition(previousNodeRects, previousSnapshots, finalNodeRects);
            RefreshEdges();
            ApplyTransform();
        }

        private void RebuildGraphCaches()
        {
            outgoingEdgesByNodeId.Clear();
            incomingEdgesByNodeId.Clear();
            treeOutgoingEdgesByNodeId.Clear();
            regularTreeOutgoingEdgesByNodeId.Clear();
            menuTreeOutgoingEdgesByNodeId.Clear();
            nodeByInstanceId.Clear();
            missingReferenceSubtreeCache.Clear();
            issueSubtreeCache.Clear();
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

            if (rootNodes.Count == 0)
            {
                rootNodes.AddRange(graph.Nodes
                    .Where(node => !GetIncomingEdges(node.Id).Any())
                    .OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase));
            }

            if (rootNodes.Count == 0)
            {
                rootNodes.AddRange(graph.Nodes.OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase));
            }

            AddDisconnectedIssueSourceRoots();

            foreach (var pair in ComputeMinimumRegularDepths(rootNodes))
            {
                minimumRegularDepths[pair.Key] = pair.Value;
            }
        }

        private bool IsRootNodeFromCache(DependencyNode node)
        {
            if (node.Kind != DependencyNodeKind.SceneObject)
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

        private void AddDisconnectedIssueSourceRoots()
        {
            if (graph == null || rootNodes.Count == 0)
            {
                return;
            }

            var rootIds = new HashSet<string>(rootNodes.Select(node => node.Id), StringComparer.Ordinal);
            var reachableNodeIds = GetReachableNodeIds(rootNodes);
            var additionalRoots = new List<DependencyNode>();
            for (var i = 0; i < graph.Edges.Count; i++)
            {
                var edge = graph.Edges[i];
                if (edge == null
                    || edge.ReferenceKind != DependencyReferenceKind.Issue
                    || string.IsNullOrEmpty(edge.SourceNodeId)
                    || rootIds.Contains(edge.SourceNodeId)
                    || reachableNodeIds.Contains(edge.SourceNodeId)
                    || !graph.TryGetNode(edge.SourceNodeId, out var sourceNode)
                    || sourceNode.Kind == DependencyNodeKind.Issue)
                {
                    continue;
                }

                additionalRoots.Add(sourceNode);
                rootIds.Add(sourceNode.Id);
                reachableNodeIds.Add(sourceNode.Id);
            }

            rootNodes.AddRange(additionalRoots
                .OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(node => node.Path, StringComparer.OrdinalIgnoreCase));
        }

        private HashSet<string> GetReachableNodeIds(IReadOnlyList<DependencyNode> roots)
        {
            var reachableNodeIds = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>();
            for (var i = 0; i < roots.Count; i++)
            {
                if (roots[i] != null && !string.IsNullOrEmpty(roots[i].Id))
                {
                    stack.Push(roots[i].Id);
                }
            }

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!reachableNodeIds.Add(current))
                {
                    continue;
                }

                foreach (var edge in GetTreeOutgoingEdges(current))
                {
                    if (!string.IsNullOrEmpty(edge.TargetNodeId))
                    {
                        stack.Push(edge.TargetNodeId);
                    }
                }
            }

            return reachableNodeIds;
        }

        private List<RenderNode> BuildRenderTree()
        {
            var defaultExpandedNodeIds = new HashSet<string>();
            var renderRoots = new List<RenderNode>();
            for (var i = 0; i < rootNodes.Count; i++)
            {
                var root = BuildRenderNode(
                    rootNodes[i].Id,
                    null,
                    null,
                    0,
                    i,
                    "root-" + i,
                    new HashSet<string>(),
                    false,
                    1f,
                    minimumRegularDepths,
                    defaultExpandedNodeIds);
                if (root != null)
                {
                    renderRoots.Add(root);
                }
            }

            return renderRoots;
        }

        private RenderNode BuildRenderNode(
            string nodeId,
            RenderNode parent,
            DependencyEdge edgeFromParent,
            int depth,
            int siblingIndex,
            string viewId,
            HashSet<string> path,
            bool forceCollapseThisNode,
            float siblingCountScale,
            IReadOnlyDictionary<string, int> minimumRegularDepths,
            HashSet<string> defaultExpandedNodeIds)
        {
            if (!graph.TryGetNode(nodeId, out var node))
            {
                return null;
            }

            var isSearchFiltering = IsSearchFilteringActive();
            if (isSearchFiltering && !IsSearchVisibleNode(nodeId))
            {
                return null;
            }

            var renderNode = new RenderNode(
                viewId,
                nodeId,
                node,
                edgeFromParent,
                depth,
                siblingIndex,
                parent,
                GetDepthNodeScale(depth) * siblingCountScale);
            renderNode.Size = DependencyNodeView.GetPreferredSize(node, renderNode.SizeScale);
            var childPath = new HashSet<string>(path) { nodeId };
            var regularEdges = GetRenderableChildEdges(GetRegularTreeOutgoingEdges(nodeId), childPath, isSearchFiltering);
            var menuEdges = GetRenderableChildEdges(GetMenuTreeOutgoingEdges(nodeId), childPath, isSearchFiltering);

            var isExpanded = expandedViewIds.Contains(viewId) || expandedNodeIds.Contains(nodeId);
            var isCollapsedByRule = ShouldCollapseNode(node, nodeId, depth);
            var isMenuExpanded = expandedMenuViewIds.Contains(viewId)
                || expandedMenuNodeIds.Contains(nodeId)
                || (isSearchFiltering && menuEdges.Count > 0);
            var isDuplicateDefaultCollapsed = ShouldCollapseDuplicateByDefault(
                nodeId,
                depth,
                isExpanded,
                minimumRegularDepths,
                defaultExpandedNodeIds);
            renderNode.CanToggleChildren = regularEdges.Count > 0;
            renderNode.HasMenuChildren = menuEdges.Count > 0;
            renderNode.IsMenuExpanded = renderNode.HasMenuChildren && isMenuExpanded;
            var isCollapsed = renderNode.CanToggleChildren
                && !isSearchFiltering
                && (collapsedViewIds.Contains(viewId)
                    || collapsedNodeIds.Contains(nodeId)
                    || isDuplicateDefaultCollapsed
                    || (isCollapsedByRule && !isExpanded)
                    || (forceCollapseThisNode && !isExpanded));
            renderNode.IsExpanded = renderNode.CanToggleChildren && !isCollapsed;
            renderNode.HasHiddenChildren = (renderNode.CanToggleChildren && isCollapsed)
                || (renderNode.HasMenuChildren && !renderNode.IsMenuExpanded);

            var visibleChildCount = (isCollapsed ? 0 : regularEdges.Count)
                + (renderNode.IsMenuExpanded ? menuEdges.Count : 0);
            if (visibleChildCount == 0)
            {
                return renderNode;
            }

            var childIndex = 0;
            var childSiblingScale = GetSiblingCountScale(visibleChildCount);
            if (!isCollapsed)
            {
                AddRenderChildren(
                    renderNode,
                    regularEdges,
                    "r",
                    depth,
                    childPath,
                    isExpanded,
                    childSiblingScale,
                    minimumRegularDepths,
                    defaultExpandedNodeIds,
                    ref childIndex);
            }

            if (renderNode.IsMenuExpanded)
            {
                AddRenderChildren(
                    renderNode,
                    menuEdges,
                    "m",
                    depth,
                    childPath,
                    isExpanded,
                    childSiblingScale,
                    minimumRegularDepths,
                    defaultExpandedNodeIds,
                    ref childIndex);
            }

            return renderNode;
        }

        private void AddRenderChildren(
            RenderNode renderNode,
            IReadOnlyList<DependencyEdge> edges,
            string viewIdPrefix,
            int depth,
            HashSet<string> childPath,
            bool forceCollapseChildren,
            float childSiblingScale,
            IReadOnlyDictionary<string, int> minimumRegularDepths,
            HashSet<string> defaultExpandedNodeIds,
            ref int childIndex)
        {
            if (renderNode == null || edges == null)
            {
                return;
            }

            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                var child = BuildRenderNode(
                    edge.TargetNodeId,
                    renderNode,
                    edge,
                    depth + 1,
                    childIndex,
                    renderNode.ViewId + "-" + viewIdPrefix + i,
                    childPath,
                    forceCollapseChildren,
                    childSiblingScale,
                    minimumRegularDepths,
                    defaultExpandedNodeIds);
                if (child == null)
                {
                    continue;
                }

                renderNode.Children.Add(child);
                childIndex++;
            }
        }

        private List<DependencyEdge> GetRenderableChildEdges(
            IReadOnlyList<DependencyEdge> sourceEdges,
            HashSet<string> childPath,
            bool isSearchFiltering)
        {
            if (sourceEdges == null || sourceEdges.Count == 0)
            {
                return EmptyEdges;
            }

            var edges = new List<DependencyEdge>();
            for (var i = 0; i < sourceEdges.Count; i++)
            {
                var edge = sourceEdges[i];
                if (edge == null
                    || childPath.Contains(edge.TargetNodeId)
                    || (isSearchFiltering && !IsSearchVisibleNode(edge.TargetNodeId)))
                {
                    continue;
                }

                edges.Add(edge);
            }

            return edges;
        }

        private static float GetDepthNodeScale(int depth)
        {
            return Mathf.Max(MinimumDepthNodeScale, 1f - Mathf.Max(0, depth) * DepthNodeScaleStep);
        }

        private static float GetSiblingCountScale(int childCount)
        {
            if (childCount <= 1)
            {
                return 1f;
            }

            return Mathf.Max(MinimumDepthNodeScale, 1f - (childCount - 1) * 0.04f);
        }

        private static bool IsScriptNode(DependencyNode node)
        {
            return string.Equals(node.IconContentName, "cs Script Icon", StringComparison.Ordinal)
                || node.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
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

        private static bool ShouldCollapseDuplicateByDefault(
            string nodeId,
            int depth,
            bool isExplicitlyExpanded,
            IReadOnlyDictionary<string, int> minimumRegularDepths,
            HashSet<string> defaultExpandedNodeIds)
        {
            if (isExplicitlyExpanded || string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            if (minimumRegularDepths != null
                && minimumRegularDepths.TryGetValue(nodeId, out var minimumDepth)
                && depth > minimumDepth)
            {
                return true;
            }

            return defaultExpandedNodeIds != null && !defaultExpandedNodeIds.Add(nodeId);
        }

        private bool ShouldCollapseNode(DependencyNode node, string nodeId, int depth)
        {
            return depth >= initialDepth
                || node.IsHeavyLeafType
                || IsPrefabAssetNode(node)
                || IsPrefabInstanceNode(nodeId);
        }

        private bool IsPrefabInstanceNode(string nodeId)
        {
            var outgoingEdges = GetOutgoingEdges(nodeId);
            for (var i = 0; i < outgoingEdges.Count; i++)
            {
                if (outgoingEdges[i].ReferenceKind == DependencyReferenceKind.PrefabInstance)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPrefabAssetNode(DependencyNode node)
        {
            return node.Kind == DependencyNodeKind.Asset
                && (node.TypeName.Contains("Prefab") || node.Path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase));
        }
    }
}
