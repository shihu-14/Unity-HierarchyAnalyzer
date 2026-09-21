using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
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
                ShowEmptyState();
                return;
            }

            var roots = BuildRenderTree();
            if (roots.Count == 0)
            {
                ShowEmptyState();
                return;
            }

            emptyStateLabel.style.display = DisplayStyle.None;
            var canvasSize = LayoutNodes(roots, previousNodeRects);
            currentCanvasSize = canvasSize;
            SetCanvasSize(currentCanvasSize.x, currentCanvasSize.y);
            var finalNodeRects = new Dictionary<string, Rect>(nodeRects);
            AnimateLayoutTransition(previousNodeRects, previousSnapshots, finalNodeRects);
            RefreshEdges();
            ApplyTransform();
        }

        private void ShowEmptyState()
        {
            emptyStateLabel.style.display = DisplayStyle.Flex;
            currentCanvasSize = Vector2.one;
            SetCanvasSize(currentCanvasSize.x, currentCanvasSize.y);
            ApplyTransform();
        }

        private List<RenderNode> BuildRenderTree()
        {
            var defaultExpandedNodeIds = new HashSet<string>();
            var renderRoots = new List<RenderNode>();
            for (var i = 0; i < graphIndex.RootNodes.Count; i++)
            {
                var root = BuildRenderNode(
                    graphIndex.RootNodes[i].Id,
                    null,
                    null,
                    0,
                    i,
                    "root-" + i,
                    new HashSet<string>(),
                    false,
                    1f,
                    graphIndex.MinimumRegularDepths,
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
            var regularEdges = GetRenderableChildEdges(graphIndex.GetRegularTreeOutgoingEdges(nodeId), childPath);
            var menuEdges = GetRenderableChildEdges(graphIndex.GetMenuTreeOutgoingEdges(nodeId), childPath);

            var isExpanded = expandedViewIds.Contains(viewId) || expandedNodeIds.Contains(nodeId);
            var isCollapsedByRule = ShouldCollapseNode(node, nodeId, depth);
            var isMenuExpanded = expandedMenuViewIds.Contains(viewId)
                || expandedMenuNodeIds.Contains(nodeId);
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
                    graphIndex.MinimumRegularDepths,
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
            HashSet<string> childPath)
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
                    || childPath.Contains(edge.TargetNodeId))
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
            return initialDepth != AllExpansionDepthValue
                && (depth >= initialDepth
                || node.IsHeavyLeafType
                || IsPrefabAssetNode(node)
                || IsPrefabInstanceNode(nodeId));
        }

        private bool IsPrefabInstanceNode(string nodeId)
        {
            var outgoingEdges = graphIndex.GetOutgoingEdges(nodeId);
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
