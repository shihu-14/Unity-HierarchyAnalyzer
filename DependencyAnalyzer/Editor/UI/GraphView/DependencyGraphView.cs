using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    public sealed class DependencyGraphView : VisualElement
    {
        private const float ColumnSpacing = 380f;
        private const float RowSpacing = 134f;
        private const float CanvasPadding = 40f;
        private const float MinZoom = 0.35f;
        private const float MaxZoom = 1.8f;

        private readonly VisualElement contentLayer;
        private readonly VisualElement edgeLayer;
        private readonly VisualElement nodeLayer;
        private readonly Label emptyStateLabel;
        private readonly Dictionary<string, CustomNodeView> nodeViews = new Dictionary<string, CustomNodeView>();
        private readonly Dictionary<string, Rect> nodeRects = new Dictionary<string, Rect>();
        private readonly HashSet<string> expandedNodeIds = new HashSet<string>();

        private DependencyGraphData graph;
        private Vector2 pan = new Vector2(24f, 24f);
        private Vector2 lastMousePosition;
        private bool isPanning;
        private float zoom = 1f;
        private int initialDepth = 3;

        public DependencyGraphView()
        {
            AddToClassList("dependency-graph-view");
            focusable = true;
            style.flexGrow = 1f;
            style.overflow = Overflow.Hidden;

            contentLayer = new VisualElement { name = "dependency-graph-content" };
            contentLayer.AddToClassList("dependency-graph-content");
            contentLayer.pickingMode = PickingMode.Position;
            Add(contentLayer);

            edgeLayer = new VisualElement { name = "dependency-edge-layer" };
            edgeLayer.AddToClassList("dependency-edge-layer");
            edgeLayer.pickingMode = PickingMode.Ignore;
            contentLayer.Add(edgeLayer);

            nodeLayer = new VisualElement { name = "dependency-node-layer" };
            nodeLayer.AddToClassList("dependency-node-layer");
            contentLayer.Add(nodeLayer);

            emptyStateLabel = new Label("No scene dependencies to display");
            emptyStateLabel.AddToClassList("dependency-empty-state");
            Add(emptyStateLabel);

            RegisterCallback<WheelEvent>(HandleWheel);
            RegisterCallback<MouseDownEvent>(HandleMouseDown);
            RegisterCallback<MouseMoveEvent>(HandleMouseMove);
            RegisterCallback<MouseUpEvent>(HandleMouseUp);
            RegisterCallback<MouseLeaveEvent>(_ => StopPanning());
        }

        public event Action<DependencyNodeData> NodeSelected;

        public void Populate(DependencyGraphData graphData, int depth)
        {
            if (!ReferenceEquals(graph, graphData))
            {
                expandedNodeIds.Clear();
                ResetViewTransform();
            }

            graph = graphData;
            initialDepth = Mathf.Clamp(depth, 3, 4);
            Render();
        }

        public void ExpandNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || graph == null)
            {
                return;
            }

            if (expandedNodeIds.Add(nodeId))
            {
                Render();
            }
        }

        private void Render()
        {
            nodeLayer.Clear();
            edgeLayer.Clear();
            nodeViews.Clear();
            nodeRects.Clear();

            if (graph == null || graph.Nodes.Count == 0)
            {
                emptyStateLabel.style.display = DisplayStyle.Flex;
                SetCanvasSize(1f, 1f);
                ApplyTransform();
                return;
            }

            emptyStateLabel.style.display = DisplayStyle.None;

            var depthByNodeId = new Dictionary<string, int>();
            var visibleNodeIds = BuildVisibleNodeSet(depthByNodeId);
            var visibleNodes = graph.Nodes
                .Where(node => visibleNodeIds.Contains(node.Id))
                .OrderBy(node => depthByNodeId[node.Id])
                .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var canvasSize = LayoutNodes(visibleNodes, visibleNodeIds, depthByNodeId);
            SetCanvasSize(canvasSize.x, canvasSize.y);
            LayoutEdges(visibleNodeIds, canvasSize);
            ApplyTransform();
        }

        private HashSet<string> BuildVisibleNodeSet(Dictionary<string, int> depthByNodeId)
        {
            var visible = new HashSet<string>();
            var queue = new Queue<QueuedNode>();
            var roots = graph.Nodes
                .Where(node => !HasIncomingTraversalEdge(node.Id))
                .OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (roots.Count == 0)
            {
                roots.AddRange(graph.Nodes.OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase));
            }

            for (var i = 0; i < roots.Count; i++)
            {
                queue.Enqueue(new QueuedNode(roots[i].Id, 0));
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (depthByNodeId.TryGetValue(current.NodeId, out var existingDepth) && existingDepth <= current.Depth)
                {
                    continue;
                }

                depthByNodeId[current.NodeId] = current.Depth;
                visible.Add(current.NodeId);

                if (!graph.TryGetNode(current.NodeId, out var node))
                {
                    continue;
                }

                var isExpanded = expandedNodeIds.Contains(current.NodeId);
                if (!isExpanded && (current.Depth >= initialDepth || node.IsHeavyLeafType))
                {
                    continue;
                }

                foreach (var edge in graph.GetOutgoingEdges(current.NodeId))
                {
                    queue.Enqueue(new QueuedNode(edge.TargetNodeId, current.Depth + 1));
                }
            }

            return visible;
        }

        private bool HasIncomingTraversalEdge(string nodeId)
        {
            return graph.GetIncomingEdges(nodeId).Any();
        }

        private Vector2 LayoutNodes(
            IReadOnlyList<DependencyNodeData> visibleNodes,
            HashSet<string> visibleNodeIds,
            Dictionary<string, int> depthByNodeId)
        {
            var rowByNodeId = new Dictionary<string, float>();
            var visiting = new HashSet<string>();
            var nextRow = 0f;
            var maxDepth = 0;
            var maxRow = 0f;
            var roots = visibleNodes
                .Where(node => !graph.GetIncomingEdges(node.Id).Any(edge => visibleNodeIds.Contains(edge.SourceNodeId)))
                .OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (roots.Count == 0)
            {
                roots.AddRange(visibleNodes.OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase));
            }

            for (var i = 0; i < roots.Count; i++)
            {
                PlaceNode(roots[i].Id, visibleNodeIds, rowByNodeId, visiting, ref nextRow);
            }

            for (var i = 0; i < visibleNodes.Count; i++)
            {
                var node = visibleNodes[i];
                if (!rowByNodeId.ContainsKey(node.Id))
                {
                    PlaceNode(node.Id, visibleNodeIds, rowByNodeId, visiting, ref nextRow);
                }
            }

            for (var i = 0; i < visibleNodes.Count; i++)
            {
                var node = visibleNodes[i];
                var depth = depthByNodeId[node.Id];
                var row = rowByNodeId[node.Id];
                maxDepth = Mathf.Max(maxDepth, depth);
                maxRow = Mathf.Max(maxRow, row);

                var position = new Vector2(CanvasPadding + depth * ColumnSpacing, CanvasPadding + row * RowSpacing);
                var nodeView = new CustomNodeView(node, HasHiddenChildren(node.Id, visibleNodeIds));
                nodeView.SetGraphPosition(position);
                nodeView.NodeSelected += HandleNodeSelected;

                nodeViews.Add(node.Id, nodeView);
                nodeRects.Add(node.Id, nodeView.GetGraphRect());
                nodeLayer.Add(nodeView);
            }

            return new Vector2(
                CanvasPadding * 2f + (maxDepth + 1) * ColumnSpacing,
                CanvasPadding * 2f + (maxRow + 1) * RowSpacing);
        }

        private float PlaceNode(
            string nodeId,
            HashSet<string> visibleNodeIds,
            Dictionary<string, float> rowByNodeId,
            HashSet<string> visiting,
            ref float nextRow)
        {
            if (rowByNodeId.TryGetValue(nodeId, out var placedRow))
            {
                return placedRow;
            }

            if (!visiting.Add(nodeId))
            {
                var cycleRow = nextRow;
                rowByNodeId[nodeId] = cycleRow;
                nextRow += 1f;
                return cycleRow;
            }

            var childRows = new List<float>();
            var childEdges = GetVisibleOutgoingEdges(nodeId, visibleNodeIds);
            for (var i = 0; i < childEdges.Count; i++)
            {
                var targetId = childEdges[i].TargetNodeId;
                if (targetId == nodeId)
                {
                    continue;
                }

                childRows.Add(PlaceNode(targetId, visibleNodeIds, rowByNodeId, visiting, ref nextRow));
            }

            float row;
            if (childRows.Count == 0)
            {
                row = nextRow;
                nextRow += 1f;
            }
            else
            {
                row = (childRows[0] + childRows[childRows.Count - 1]) * 0.5f;
            }

            visiting.Remove(nodeId);
            rowByNodeId[nodeId] = row;
            return row;
        }

        private void LayoutEdges(HashSet<string> visibleNodeIds, Vector2 canvasSize)
        {
            var visibleEdges = graph.Edges
                .Where(edge => visibleNodeIds.Contains(edge.SourceNodeId)
                    && visibleNodeIds.Contains(edge.TargetNodeId)
                    && nodeRects.ContainsKey(edge.SourceNodeId)
                    && nodeRects.ContainsKey(edge.TargetNodeId))
                .OrderBy(edge => depthSort(edge.ReferenceKind))
                .ThenBy(edge => edge.MemberName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var totalBySource = visibleEdges
                .GroupBy(edge => edge.SourceNodeId)
                .ToDictionary(group => group.Key, group => group.Count());
            var indexBySource = new Dictionary<string, int>();

            for (var i = 0; i < visibleEdges.Count; i++)
            {
                var edge = visibleEdges[i];
                if (!nodeRects.TryGetValue(edge.SourceNodeId, out var sourceRect)
                    || !nodeRects.TryGetValue(edge.TargetNodeId, out var targetRect))
                {
                    continue;
                }

                indexBySource.TryGetValue(edge.SourceNodeId, out var edgeIndex);
                indexBySource[edge.SourceNodeId] = edgeIndex + 1;
                var total = totalBySource[edge.SourceNodeId];
                var routeOffset = (edgeIndex - (total - 1) * 0.5f) * 10f;
                var edgeView = new CustomEdgeView(edge);
                edgeView.SetCanvasSize(canvasSize.x, canvasSize.y);
                edgeView.SetEndpoints(sourceRect, targetRect, routeOffset);
                edgeLayer.Add(edgeView);
            }

            int depthSort(DependencyReferenceKind kind)
            {
                return GetEdgeSortPriority(kind);
            }
        }

        private List<DependencyEdgeData> GetVisibleOutgoingEdges(string nodeId, HashSet<string> visibleNodeIds)
        {
            return graph.GetOutgoingEdges(nodeId)
                .Where(edge => visibleNodeIds.Contains(edge.TargetNodeId))
                .OrderBy(edge => GetEdgeSortPriority(edge.ReferenceKind))
                .ThenBy(edge => edge.MemberName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.TargetNodeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private bool HasHiddenChildren(string nodeId, HashSet<string> visibleNodeIds)
        {
            return graph.GetOutgoingEdges(nodeId).Any(edge => !visibleNodeIds.Contains(edge.TargetNodeId));
        }

        private static int GetEdgeSortPriority(DependencyReferenceKind kind)
        {
            switch (kind)
            {
                case DependencyReferenceKind.Hierarchy:
                    return 0;
                case DependencyReferenceKind.SerializedProperty:
                    return 1;
                case DependencyReferenceKind.PrefabInstance:
                    return 2;
                default:
                    return 3;
            }
        }

        private void SetCanvasSize(float width, float height)
        {
            contentLayer.style.width = Mathf.Max(1f, width);
            contentLayer.style.height = Mathf.Max(1f, height);
            edgeLayer.style.width = Mathf.Max(1f, width);
            edgeLayer.style.height = Mathf.Max(1f, height);
            nodeLayer.style.width = Mathf.Max(1f, width);
            nodeLayer.style.height = Mathf.Max(1f, height);
        }

        private void ResetViewTransform()
        {
            pan = new Vector2(24f, 24f);
            zoom = 1f;
        }

        private void ApplyTransform()
        {
            contentLayer.transform.position = new Vector3(pan.x, pan.y, 0f);
            contentLayer.transform.scale = new Vector3(zoom, zoom, 1f);
        }

        private void HandleNodeSelected(DependencyNodeData node)
        {
            NodeSelected?.Invoke(node);
        }

        private void HandleWheel(WheelEvent evt)
        {
            var previousZoom = zoom;
            var delta = evt.delta.y > 0f ? -0.08f : 0.08f;
            zoom = Mathf.Clamp(zoom + delta, MinZoom, MaxZoom);
            var pointer = evt.localMousePosition;
            var graphPoint = (pointer - pan) / previousZoom;
            pan = pointer - graphPoint * zoom;
            ApplyTransform();
            evt.StopPropagation();
        }

        private void HandleMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 && evt.button != 2)
            {
                return;
            }

            isPanning = true;
            lastMousePosition = evt.localMousePosition;
            evt.StopPropagation();
        }

        private void HandleMouseMove(MouseMoveEvent evt)
        {
            if (!isPanning)
            {
                return;
            }

            var currentPosition = evt.localMousePosition;
            pan += currentPosition - lastMousePosition;
            lastMousePosition = currentPosition;
            ApplyTransform();
            evt.StopPropagation();
        }

        private void HandleMouseUp(MouseUpEvent evt)
        {
            if (!isPanning)
            {
                return;
            }

            StopPanning();
            evt.StopPropagation();
        }

        private void StopPanning()
        {
            if (!isPanning)
            {
                return;
            }

            isPanning = false;
        }

        private readonly struct QueuedNode
        {
            public QueuedNode(string nodeId, int depth)
            {
                NodeId = nodeId;
                Depth = depth;
            }

            public string NodeId { get; }
            public int Depth { get; }
        }
    }
}
