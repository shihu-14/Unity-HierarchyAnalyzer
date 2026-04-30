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
        private const float NodeCollisionPadding = 22f;

        private readonly VisualElement contentLayer;
        private readonly VisualElement edgeLayer;
        private readonly VisualElement nodeLayer;
        private readonly VisualElement miniMap;
        private readonly Label emptyStateLabel;
        private readonly Dictionary<string, CustomNodeView> nodeViews = new Dictionary<string, CustomNodeView>();
        private readonly Dictionary<string, Rect> nodeRects = new Dictionary<string, Rect>();
        private readonly List<RenderNode> renderNodes = new List<RenderNode>();
        private readonly HashSet<string> expandedNodeIds = new HashSet<string>();

        private DependencyGraphData graph;
        private Vector2 currentCanvasSize = Vector2.one;
        private Vector2 pan = new Vector2(24f, 24f);
        private Vector2 lastMousePosition;
        private bool isPanning;
        private float zoom = 1f;
        private float minZoom = 0.1f;
        private float maxZoom = 2f;
        private float zoomStep = 0.05f;
        private int initialDepth = 3;
        private string focusedNodeId;

        public DependencyGraphView()
        {
            AddToClassList("dependency-graph-view");
            focusable = true;
            style.flexGrow = 1f;
            style.overflow = Overflow.Hidden;

            contentLayer = new VisualElement { name = "dependency-graph-content" };
            contentLayer.AddToClassList("dependency-graph-content");
            contentLayer.pickingMode = PickingMode.Position;
            contentLayer.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            Add(contentLayer);

            edgeLayer = new VisualElement { name = "dependency-edge-layer" };
            edgeLayer.AddToClassList("dependency-edge-layer");
            edgeLayer.pickingMode = PickingMode.Ignore;
            contentLayer.Add(edgeLayer);

            nodeLayer = new VisualElement { name = "dependency-node-layer" };
            nodeLayer.AddToClassList("dependency-node-layer");
            contentLayer.Add(nodeLayer);

            miniMap = new VisualElement { name = "dependency-minimap" };
            miniMap.AddToClassList("dependency-minimap");
            miniMap.generateVisualContent += DrawMiniMap;
            miniMap.RegisterCallback<MouseDownEvent>(HandleMiniMapMouseDown);
            Add(miniMap);

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

        public void ConfigureZoom(float minimum, float maximum, float step)
        {
            minZoom = Mathf.Clamp(minimum, 0.05f, 1f);
            maxZoom = Mathf.Max(minZoom + 0.05f, Mathf.Clamp(maximum, 1f, 10f));
            zoomStep = Mathf.Clamp(step, 0.001f, 0.03f);
            zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            ApplyTransform();
        }

        public void Populate(DependencyGraphData graphData, int depth)
        {
            if (!ReferenceEquals(graph, graphData))
            {
                expandedNodeIds.Clear();
                nodeRects.Clear();
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

        public bool FocusNodeByInstanceId(int instanceId)
        {
            if (instanceId == 0 || graph == null)
            {
                return false;
            }

            var node = graph.Nodes.FirstOrDefault(candidate => candidate.InstanceId == instanceId);
            if (node == null)
            {
                return false;
            }

            return FocusNode(node.Id, true);
        }

        public bool FocusNode(string nodeId, bool centerView)
        {
            if (string.IsNullOrEmpty(nodeId) || graph == null)
            {
                return false;
            }

            focusedNodeId = nodeId;
            ExpandAncestors(nodeId);
            Render();
            UpdateFocusedNodeClass();

            if (!centerView)
            {
                return true;
            }

            var targetRect = nodeRects
                .Where(pair => renderNodes.Any(node => node.ViewId == pair.Key && node.NodeId == nodeId))
                .Select(pair => pair.Value)
                .FirstOrDefault();
            if (targetRect.width <= 0f || targetRect.height <= 0f)
            {
                return false;
            }

            CenterViewOn(targetRect.center);
            return true;
        }

        private void Render()
        {
            var previousNodeRects = new Dictionary<string, Rect>(nodeRects);
            nodeLayer.Clear();
            edgeLayer.Clear();
            nodeViews.Clear();
            nodeRects.Clear();
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
            RefreshEdges();
            ApplyTransform();
        }

        private List<RenderNode> BuildRenderTree()
        {
            var roots = graph.Nodes
                .Where(IsRootNode)
                .OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (roots.Count == 0)
            {
                roots.AddRange(graph.Nodes
                    .Where(node => !graph.GetIncomingEdges(node.Id).Any())
                    .OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase));
            }

            if (roots.Count == 0)
            {
                roots.AddRange(graph.Nodes.OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase));
            }

            var renderRoots = new List<RenderNode>();
            for (var i = 0; i < roots.Count; i++)
            {
                var root = BuildRenderNode(roots[i].Id, null, null, 0, i, "root-" + i, new HashSet<string>());
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
            DependencyEdgeData edgeFromParent,
            int depth,
            int siblingIndex,
            string viewId,
            HashSet<string> path)
        {
            if (!graph.TryGetNode(nodeId, out var node))
            {
                return null;
            }

            var renderNode = new RenderNode(viewId, nodeId, node, edgeFromParent, depth, siblingIndex, parent);
            var childPath = new HashSet<string>(path) { nodeId };
            var outgoingEdges = GetTreeOutgoingEdges(nodeId)
                .Where(edge => graph.TryGetNode(edge.TargetNodeId, out _))
                .ToList();

            var isExpanded = expandedNodeIds.Contains(nodeId);
            var isCollapsed = ShouldCollapseNode(node, nodeId, depth, isExpanded);
            renderNode.HasHiddenChildren = outgoingEdges.Any(edge => !childPath.Contains(edge.TargetNodeId)) && isCollapsed;

            if (isCollapsed)
            {
                return renderNode;
            }

            var childIndex = 0;
            for (var i = 0; i < outgoingEdges.Count; i++)
            {
                var edge = outgoingEdges[i];
                if (childPath.Contains(edge.TargetNodeId))
                {
                    continue;
                }

                var child = BuildRenderNode(
                    edge.TargetNodeId,
                    renderNode,
                    edge,
                    depth + 1,
                    childIndex,
                    viewId + "-" + childIndex,
                    childPath);
                if (child != null)
                {
                    renderNode.Children.Add(child);
                    childIndex++;
                }
            }

            return renderNode;
        }

        private bool IsRootNode(DependencyNodeData node)
        {
            return node.Kind == DependencyNodeKind.SceneObject
                && !graph.GetIncomingEdges(node.Id).Any(edge => edge.ReferenceKind == DependencyReferenceKind.Hierarchy);
        }

        private bool ShouldCollapseNode(DependencyNodeData node, string nodeId, int depth, bool isExpanded)
        {
            if (isExpanded)
            {
                return false;
            }

            return depth >= initialDepth
                || node.IsHeavyLeafType
                || IsPrefabAssetNode(node)
                || IsPrefabInstanceNode(nodeId);
        }

        private bool IsPrefabInstanceNode(string nodeId)
        {
            return graph.GetOutgoingEdges(nodeId).Any(edge => edge.ReferenceKind == DependencyReferenceKind.PrefabInstance);
        }

        private static bool IsPrefabAssetNode(DependencyNodeData node)
        {
            return node.Kind == DependencyNodeKind.Asset
                && (node.TypeName.Contains("Prefab") || node.Path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase));
        }

        private Vector2 LayoutNodes(IReadOnlyList<RenderNode> roots, IReadOnlyDictionary<string, Rect> previousNodeRects)
        {
            var nextRow = 0f;

            for (var i = 0; i < roots.Count; i++)
            {
                PlaceNode(roots[i], ref nextRow);
            }

            for (var i = 0; i < roots.Count; i++)
            {
                CollectRenderNodes(roots[i]);
            }

            var occupiedRects = previousNodeRects
                .Where(pair => renderNodes.Any(node => node.ViewId == pair.Key))
                .Select(pair => Inflate(pair.Value, NodeCollisionPadding))
                .ToList();
            var maxX = CanvasPadding;
            var maxY = CanvasPadding;

            for (var i = 0; i < renderNodes.Count; i++)
            {
                var renderNode = renderNodes[i];
                renderNode.Size = CustomNodeView.GetPreferredSize(renderNode.Node);
                var layoutPosition = new Vector2(CanvasPadding + renderNode.Depth * ColumnSpacing, CanvasPadding + renderNode.Row * RowSpacing);
                var position = layoutPosition;

                if (previousNodeRects.TryGetValue(renderNode.ViewId, out var previousRect))
                {
                    position = previousRect.position;
                }
                else if (renderNode.Parent != null && nodeRects.TryGetValue(renderNode.Parent.ViewId, out var parentRect))
                {
                    var preferredPosition = new Vector2(parentRect.xMax + 96f, parentRect.y + renderNode.SiblingIndex * (renderNode.Size.y + 48f));
                    position = FindOpenPosition(preferredPosition, renderNode.Size, occupiedRects);
                }
                else
                {
                    position = FindOpenPosition(layoutPosition, renderNode.Size, occupiedRects);
                }

                var nodeView = new CustomNodeView(renderNode.ViewId, renderNode.Node, renderNode.HasHiddenChildren, () => zoom);
                nodeView.SetGraphPosition(position);
                if (renderNode.NodeId == focusedNodeId)
                {
                    nodeView.AddToClassList("dependency-node--focused");
                }

                nodeView.NodeSelected += HandleNodeSelected;
                nodeView.NodeMoved += HandleNodeMoved;

                nodeViews.Add(renderNode.ViewId, nodeView);
                var rect = nodeView.GetGraphRect();
                nodeRects.Add(renderNode.ViewId, rect);
                if (!previousNodeRects.ContainsKey(renderNode.ViewId))
                {
                    occupiedRects.Add(Inflate(rect, NodeCollisionPadding));
                }

                maxX = Mathf.Max(maxX, rect.xMax);
                maxY = Mathf.Max(maxY, rect.yMax);
                nodeLayer.Add(nodeView);
            }

            return new Vector2(
                Mathf.Max(CanvasPadding * 2f + ColumnSpacing, maxX + CanvasPadding),
                Mathf.Max(CanvasPadding * 2f + RowSpacing, maxY + CanvasPadding));
        }

        private static Vector2 FindOpenPosition(Vector2 preferredPosition, Vector2 nodeSize, IReadOnlyList<Rect> occupiedRects)
        {
            var candidate = new Rect(
                Mathf.Max(CanvasPadding, preferredPosition.x),
                Mathf.Max(CanvasPadding, preferredPosition.y),
                nodeSize.x,
                nodeSize.y);
            var verticalStep = nodeSize.y + NodeCollisionPadding * 2f;
            var horizontalStep = ColumnSpacing * 0.55f;

            for (var attempt = 0; attempt < 240; attempt++)
            {
                var paddedCandidate = Inflate(candidate, NodeCollisionPadding);
                if (!occupiedRects.Any(rect => rect.Overlaps(paddedCandidate)))
                {
                    return candidate.position;
                }

                candidate.y += verticalStep;
                if (attempt > 0 && attempt % 24 == 0)
                {
                    candidate.x += horizontalStep;
                    candidate.y = CanvasPadding;
                }
            }

            return candidate.position;
        }

        private static Rect Inflate(Rect rect, float padding)
        {
            return new Rect(
                rect.xMin - padding,
                rect.yMin - padding,
                rect.width + padding * 2f,
                rect.height + padding * 2f);
        }

        private float PlaceNode(RenderNode node, ref float nextRow)
        {
            var childRows = new List<float>();
            for (var i = 0; i < node.Children.Count; i++)
            {
                childRows.Add(PlaceNode(node.Children[i], ref nextRow));
            }

            if (childRows.Count == 0)
            {
                node.Row = nextRow;
                nextRow += 1f;
            }
            else
            {
                node.Row = (childRows[0] + childRows[childRows.Count - 1]) * 0.5f;
            }

            return node.Row;
        }

        private void CollectRenderNodes(RenderNode root)
        {
            renderNodes.Add(root);
            for (var i = 0; i < root.Children.Count; i++)
            {
                CollectRenderNodes(root.Children[i]);
            }
        }

        private void RefreshEdges()
        {
            edgeLayer.Clear();

            var visibleEdges = renderNodes
                .Where(node => node.Parent != null
                    && node.EdgeFromParent != null
                    && nodeRects.ContainsKey(node.Parent.ViewId)
                    && nodeRects.ContainsKey(node.ViewId))
                .ToList();
            var totalBySource = visibleEdges
                .GroupBy(node => node.Parent.ViewId)
                .ToDictionary(group => group.Key, group => group.Count());
            var indexBySource = new Dictionary<string, int>();

            for (var i = 0; i < visibleEdges.Count; i++)
            {
                var renderNode = visibleEdges[i];
                var parentViewId = renderNode.Parent.ViewId;
                if (!nodeRects.TryGetValue(parentViewId, out var sourceRect)
                    || !nodeRects.TryGetValue(renderNode.ViewId, out var targetRect))
                {
                    continue;
                }

                indexBySource.TryGetValue(parentViewId, out var edgeIndex);
                indexBySource[parentViewId] = edgeIndex + 1;
                var total = totalBySource[parentViewId];
                var routeOffset = (edgeIndex - (total - 1) * 0.5f) * 22f;
                var edgeView = new CustomEdgeView(renderNode.EdgeFromParent);
                edgeView.SetCanvasSize(currentCanvasSize.x, currentCanvasSize.y);
                edgeView.SetEndpoints(sourceRect, targetRect, routeOffset, edgeIndex, total);
                edgeLayer.Add(edgeView);
            }
        }

        private List<DependencyEdgeData> GetTreeOutgoingEdges(string nodeId)
        {
            return graph.GetOutgoingEdges(nodeId)
                .GroupBy(edge => edge.TargetNodeId)
                .Select(group => group
                    .OrderBy(edge => GetEdgeSortPriority(edge.ReferenceKind))
                    .ThenBy(edge => edge.MemberName, StringComparer.OrdinalIgnoreCase)
                    .First())
                .OrderBy(edge => GetEdgeSortPriority(edge.ReferenceKind))
                .ThenBy(edge => edge.MemberName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.TargetNodeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static int GetEdgeSortPriority(DependencyReferenceKind kind)
        {
            switch (kind)
            {
                case DependencyReferenceKind.Hierarchy:
                    return 0;
                case DependencyReferenceKind.Component:
                    return 1;
                case DependencyReferenceKind.SerializedProperty:
                    return 2;
                case DependencyReferenceKind.PrefabInstance:
                    return 3;
                default:
                    return 4;
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
            zoom = Mathf.Clamp(1f, minZoom, maxZoom);
        }

        private void ApplyTransform()
        {
            contentLayer.style.left = pan.x;
            contentLayer.style.top = pan.y;
            contentLayer.transform.position = Vector3.zero;
            contentLayer.transform.scale = new Vector3(zoom, zoom, 1f);
            miniMap.MarkDirtyRepaint();
        }

        private void HandleNodeSelected(DependencyNodeData node)
        {
            focusedNodeId = node.Id;
            UpdateFocusedNodeClass();
            NodeSelected?.Invoke(node);
        }

        private void HandleNodeMoved(CustomNodeView nodeView, Vector2 nextPosition)
        {
            if (nodeView == null || !nodeRects.ContainsKey(nodeView.ViewId))
            {
                return;
            }

            nextPosition = new Vector2(Mathf.Max(CanvasPadding, nextPosition.x), Mathf.Max(CanvasPadding, nextPosition.y));
            nodeView.SetGraphPosition(nextPosition);
            nodeRects[nodeView.ViewId] = nodeView.GetGraphRect();

            var movedRect = nodeView.GetGraphRect();
            var width = Mathf.Max(currentCanvasSize.x, movedRect.xMax + CanvasPadding);
            var height = Mathf.Max(currentCanvasSize.y, movedRect.yMax + CanvasPadding);
            currentCanvasSize = new Vector2(width, height);
            SetCanvasSize(currentCanvasSize.x, currentCanvasSize.y);
            RefreshEdges();
            miniMap.MarkDirtyRepaint();
        }

        private void HandleWheel(WheelEvent evt)
        {
            var previousZoom = zoom;
            var delta = evt.delta.y > 0f ? -zoomStep : zoomStep;
            zoom = Mathf.Clamp(zoom + delta, minZoom, maxZoom);
            if (Mathf.Approximately(previousZoom, zoom))
            {
                evt.PreventDefault();
                evt.StopPropagation();
                return;
            }

            var pivot = evt.localMousePosition;
            if (float.IsNaN(pivot.x) || float.IsNaN(pivot.y))
            {
                pivot = GetViewportSize() * 0.5f;
            }

            var graphPoint = ViewportToGraph(pivot, previousZoom);
            pan = pivot - graphPoint * zoom;
            ApplyTransform();
            evt.PreventDefault();
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
            evt.PreventDefault();
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
            evt.PreventDefault();
            evt.StopPropagation();
        }

        private void HandleMouseUp(MouseUpEvent evt)
        {
            if (!isPanning)
            {
                return;
            }

            StopPanning();
            evt.PreventDefault();
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

        private Vector2 GetViewportSize()
        {
            var width = resolvedStyle.width;
            var height = resolvedStyle.height;
            if (float.IsNaN(width) || width <= 0f)
            {
                width = layout.width;
            }

            if (float.IsNaN(height) || height <= 0f)
            {
                height = layout.height;
            }

            return new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
        }

        private void DrawMiniMap(MeshGenerationContext context)
        {
            if (currentCanvasSize.x <= 1f || currentCanvasSize.y <= 1f || nodeRects.Count == 0)
            {
                return;
            }

            var metrics = GetMiniMapMetrics(miniMap.contentRect);
            var painter = context.painter2D;
            painter.fillColor = new Color(0.34f, 0.48f, 0.61f, 0.72f);
            foreach (var rect in nodeRects.Values)
            {
                FillRect(painter, GraphRectToMiniMap(rect, metrics));
            }

            var viewportSize = GetViewportSize();
            var graphViewportTopLeft = ViewportToGraph(Vector2.zero, zoom);
            var graphViewport = new Rect(graphViewportTopLeft.x, graphViewportTopLeft.y, viewportSize.x / zoom, viewportSize.y / zoom);
            painter.strokeColor = new Color(0.92f, 0.96f, 1f, 0.95f);
            painter.lineWidth = 1.4f;
            StrokeRect(painter, GraphRectToMiniMap(graphViewport, metrics));
        }

        private void HandleMiniMapMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || currentCanvasSize.x <= 1f || currentCanvasSize.y <= 1f)
            {
                return;
            }

            var metrics = GetMiniMapMetrics(miniMap.contentRect);
            var graphPoint = MiniMapPointToGraph(evt.localMousePosition, metrics);
            CenterViewOn(graphPoint);
            evt.PreventDefault();
            evt.StopPropagation();
        }

        private void CenterViewOn(Vector2 graphPoint)
        {
            var viewportCenter = GetViewportSize() * 0.5f;
            pan = viewportCenter - graphPoint * zoom;
            ApplyTransform();
        }

        private Vector2 ViewportToGraph(Vector2 viewportPoint, float scale)
        {
            return (viewportPoint - pan) / Mathf.Max(0.0001f, scale);
        }

        private void ExpandAncestors(string nodeId)
        {
            var visited = new HashSet<string>();
            var current = nodeId;
            while (visited.Add(current))
            {
                var parentEdge = graph.GetIncomingEdges(current)
                    .OrderBy(edge => GetEdgeSortPriority(edge.ReferenceKind))
                    .FirstOrDefault();
                if (parentEdge == null || string.IsNullOrEmpty(parentEdge.SourceNodeId))
                {
                    return;
                }

                expandedNodeIds.Add(parentEdge.SourceNodeId);
                current = parentEdge.SourceNodeId;
            }
        }

        private void UpdateFocusedNodeClass()
        {
            foreach (var view in nodeViews.Values)
            {
                if (view.Data.Id == focusedNodeId)
                {
                    view.AddToClassList("dependency-node--focused");
                }
                else
                {
                    view.RemoveFromClassList("dependency-node--focused");
                }
            }
        }

        private MiniMapMetrics GetMiniMapMetrics(Rect rect)
        {
            const float padding = 6f;
            var availableWidth = Mathf.Max(1f, rect.width - padding * 2f);
            var availableHeight = Mathf.Max(1f, rect.height - padding * 2f);
            var scale = Mathf.Min(availableWidth / currentCanvasSize.x, availableHeight / currentCanvasSize.y);
            scale = Mathf.Max(0.0001f, scale);
            var graphSize = currentCanvasSize * scale;
            var origin = new Vector2(
                rect.xMin + (rect.width - graphSize.x) * 0.5f,
                rect.yMin + (rect.height - graphSize.y) * 0.5f);
            return new MiniMapMetrics(origin, scale);
        }

        private static Rect GraphRectToMiniMap(Rect graphRect, MiniMapMetrics metrics)
        {
            return new Rect(
                metrics.Origin.x + graphRect.x * metrics.Scale,
                metrics.Origin.y + graphRect.y * metrics.Scale,
                Mathf.Max(1.5f, graphRect.width * metrics.Scale),
                Mathf.Max(1.5f, graphRect.height * metrics.Scale));
        }

        private Vector2 MiniMapPointToGraph(Vector2 point, MiniMapMetrics metrics)
        {
            var graphPoint = (point - metrics.Origin) / metrics.Scale;
            return new Vector2(
                Mathf.Clamp(graphPoint.x, 0f, currentCanvasSize.x),
                Mathf.Clamp(graphPoint.y, 0f, currentCanvasSize.y));
        }

        private static void FillRect(Painter2D painter, Rect rect)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        private static void StrokeRect(Painter2D painter, Rect rect)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Stroke();
        }

        private readonly struct MiniMapMetrics
        {
            public MiniMapMetrics(Vector2 origin, float scale)
            {
                Origin = origin;
                Scale = scale;
            }

            public Vector2 Origin { get; }
            public float Scale { get; }
        }

        private sealed class RenderNode
        {
            public RenderNode(
                string viewId,
                string nodeId,
                DependencyNodeData node,
                DependencyEdgeData edgeFromParent,
                int depth,
                int siblingIndex,
                RenderNode parent)
            {
                ViewId = viewId;
                NodeId = nodeId;
                Node = node;
                EdgeFromParent = edgeFromParent;
                Depth = depth;
                SiblingIndex = siblingIndex;
                Parent = parent;
            }

            public string ViewId { get; }
            public string NodeId { get; }
            public DependencyNodeData Node { get; }
            public DependencyEdgeData EdgeFromParent { get; }
            public int Depth { get; }
            public int SiblingIndex { get; }
            public RenderNode Parent { get; }
            public List<RenderNode> Children { get; } = new List<RenderNode>();
            public Vector2 Size { get; set; }
            public float Row { get; set; }
            public bool HasHiddenChildren { get; set; }
        }
    }
}
