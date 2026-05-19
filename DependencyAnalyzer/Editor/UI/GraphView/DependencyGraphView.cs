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
        private const float ColumnSpacing = 540f;
        private const float RowSpacing = 154f;
        private const float CanvasPadding = 40f;
        private const float NodeCollisionPadding = 5f;
        private const float ChildHorizontalOffset = 168f;
        private const float ShallowNodeGap = 28f;
        private const float DeepNodeGap = 12f;
        private const float DepthNodeScaleStep = 0.08f;
        private const float MinimumDepthNodeScale = 0.58f;
        private const float AnimationDurationSeconds = 0.22f;

        private readonly VisualElement contentLayer;
        private readonly VisualElement edgeLayer;
        private readonly VisualElement animationLayer;
        private readonly VisualElement nodeLayer;
        private readonly VisualElement miniMap;
        private readonly Label emptyStateLabel;
        private readonly Dictionary<string, CustomNodeView> nodeViews = new Dictionary<string, CustomNodeView>();
        private readonly Dictionary<string, Rect> nodeRects = new Dictionary<string, Rect>();
        private readonly List<RenderNode> renderNodes = new List<RenderNode>();
        private readonly List<EdgeRoute> edgeRoutes = new List<EdgeRoute>();
        private readonly HashSet<string> expandedNodeIds = new HashSet<string>();
        private readonly HashSet<string> collapsedNodeIds = new HashSet<string>();
        private readonly HashSet<string> expandedViewIds = new HashSet<string>();
        private readonly HashSet<string> collapsedViewIds = new HashSet<string>();
        private readonly HashSet<string> expandedMenuViewIds = new HashSet<string>();
        private readonly HashSet<string> expandedMenuNodeIds = new HashSet<string>();
        private readonly HashSet<string> manuallyMovedViewIds = new HashSet<string>();
        private readonly HashSet<string> searchMatchNodeIds = new HashSet<string>();
        private readonly HashSet<string> searchVisibleNodeIds = new HashSet<string>();
        private readonly HashSet<string> forcedVisibleNodeIds = new HashSet<string>();
        private readonly List<string> searchResultNodeIds = new List<string>();

        private DependencyGraphData graph;
        private Vector2 currentCanvasSize = Vector2.one;
        private Vector2 pan = new Vector2(24f, 24f);
        private Vector2 lastMousePosition;
        private bool isPanning;
        private float zoom = 1f;
        private float minZoom = 0.1f;
        private float maxZoom = 2f;
        private float zoomStep = 0.05f;
        private int initialDepth = 2;
        private string focusedNodeId;
        private string focusedViewId;
        private string searchQuery = string.Empty;
        private bool searchFilterEnabled;
        private int currentSearchResultIndex = -1;
        private IVisualElementScheduledItem activeAnimation;

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

            animationLayer = new VisualElement { name = "dependency-animation-layer" };
            animationLayer.AddToClassList("dependency-animation-layer");
            animationLayer.pickingMode = PickingMode.Ignore;
            contentLayer.Add(animationLayer);

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

        public SearchResultState SetSearch(string query, bool filterEnabled, bool focusCurrent)
        {
            var previousCurrentNodeId = GetCurrentSearchNodeId();
            searchQuery = query ?? string.Empty;
            searchFilterEnabled = filterEnabled;
            forcedVisibleNodeIds.Clear();
            RebuildSearchIndex(previousCurrentNodeId);
            Render();

            if (focusCurrent && searchResultNodeIds.Count > 0)
            {
                FocusCurrentSearchResult();
            }

            return GetSearchResultState();
        }

        public SearchResultState FocusNextSearchResult(bool reverse)
        {
            if (searchResultNodeIds.Count == 0)
            {
                currentSearchResultIndex = -1;
                Render();
                return GetSearchResultState();
            }

            if (currentSearchResultIndex < 0)
            {
                currentSearchResultIndex = reverse ? searchResultNodeIds.Count - 1 : 0;
            }
            else
            {
                currentSearchResultIndex += reverse ? -1 : 1;
                if (currentSearchResultIndex < 0)
                {
                    currentSearchResultIndex = searchResultNodeIds.Count - 1;
                }
                else if (currentSearchResultIndex >= searchResultNodeIds.Count)
                {
                    currentSearchResultIndex = 0;
                }
            }

            FocusCurrentSearchResult();
            return GetSearchResultState();
        }

        public void Populate(DependencyGraphData graphData, int depth)
        {
            if (!ReferenceEquals(graph, graphData))
            {
                expandedNodeIds.Clear();
                collapsedNodeIds.Clear();
                expandedViewIds.Clear();
                collapsedViewIds.Clear();
                expandedMenuViewIds.Clear();
                expandedMenuNodeIds.Clear();
                manuallyMovedViewIds.Clear();
                nodeRects.Clear();
                ResetViewTransform();
            }

            graph = graphData;
            initialDepth = Mathf.Clamp(depth, 1, 4);
            RebuildSearchIndex(GetCurrentSearchNodeId());
            Render();
        }

        public void ExpandNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || graph == null)
            {
                return;
            }

            var changed = expandedNodeIds.Add(nodeId);
            changed |= collapsedNodeIds.Remove(nodeId);
            if (changed)
            {
                ClearExplicitDescendantStates(nodeId);
                Render();
            }
        }

        public void CollapseNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || graph == null)
            {
                return;
            }

            var changed = collapsedNodeIds.Add(nodeId);
            changed |= expandedNodeIds.Remove(nodeId);
            if (changed)
            {
                ClearExplicitDescendantStates(nodeId);
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
            focusedViewId = null;
            AddForcedVisiblePath(nodeId);
            ExpandAncestors(nodeId);
            Render();

            if (!centerView)
            {
                FlashFocusedNode(false);
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

            var movedView = CenterViewOnIfNeeded(targetRect);
            FlashFocusedNode(movedView);
            return true;
        }

        private void Render()
        {
            var previousNodeRects = new Dictionary<string, Rect>(nodeRects);
            var previousSnapshots = renderNodes.ToDictionary(node => node.ViewId, node => new RenderSnapshot(node));
            StopActiveAnimation();
            nodeLayer.Clear();
            edgeLayer.Clear();
            animationLayer.Clear();
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
            var finalNodeRects = new Dictionary<string, Rect>(nodeRects);
            AnimateLayoutTransition(previousNodeRects, previousSnapshots, finalNodeRects);
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

            var minimumRegularDepths = ComputeMinimumRegularDepths(roots);
            var defaultExpandedNodeIds = new HashSet<string>();
            var renderRoots = new List<RenderNode>();
            for (var i = 0; i < roots.Count; i++)
            {
                var root = BuildRenderNode(
                    roots[i].Id,
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
            DependencyEdgeData edgeFromParent,
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
            renderNode.Size = CustomNodeView.GetPreferredSize(node, renderNode.SizeScale);
            var childPath = new HashSet<string>(path) { nodeId };
            var outgoingEdges = GetTreeOutgoingEdges(nodeId)
                .Where(edge => graph.TryGetNode(edge.TargetNodeId, out _))
                .ToList();
            if (isSearchFiltering)
            {
                outgoingEdges = outgoingEdges
                    .Where(edge => IsSearchVisibleNode(edge.TargetNodeId))
                    .ToList();
            }

            var menuEdges = outgoingEdges
                .Where(edge => !childPath.Contains(edge.TargetNodeId) && IsMenuEdge(edge))
                .ToList();
            var regularEdges = outgoingEdges
                .Where(edge => !childPath.Contains(edge.TargetNodeId) && !IsMenuEdge(edge))
                .ToList();

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

            var visibleEdges = new List<DependencyEdgeData>();
            if (!isCollapsed)
            {
                visibleEdges.AddRange(regularEdges);
            }

            if (renderNode.IsMenuExpanded)
            {
                visibleEdges.AddRange(menuEdges);
            }

            if (visibleEdges.Count == 0)
            {
                return renderNode;
            }

            var childIndex = 0;
            var childSiblingScale = GetSiblingCountScale(visibleEdges.Count);
            for (var i = 0; i < visibleEdges.Count; i++)
            {
                var edge = visibleEdges[i];

                var child = BuildRenderNode(
                    edge.TargetNodeId,
                    renderNode,
                    edge,
                    depth + 1,
                    childIndex,
                    viewId + "-" + childIndex,
                    childPath,
                    isExpanded,
                    childSiblingScale,
                    minimumRegularDepths,
                    defaultExpandedNodeIds);
                if (child != null)
                {
                    renderNode.Children.Add(child);
                    childIndex++;
                }
            }

            return renderNode;
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

        private static bool IsScriptNode(DependencyNodeData node)
        {
            return string.Equals(node.IconContentName, "cs Script Icon", StringComparison.Ordinal)
                || node.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsMenuEdge(DependencyEdgeData edge)
        {
            return edge != null && edge.ReferenceKind == DependencyReferenceKind.SerializedProperty;
        }

        private Dictionary<string, int> ComputeMinimumRegularDepths(IReadOnlyList<DependencyNodeData> roots)
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
                foreach (var edge in GetTreeOutgoingEdges(current.NodeId))
                {
                    if (IsMenuEdge(edge) || !graph.TryGetNode(edge.TargetNodeId, out _))
                    {
                        continue;
                    }

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

        private bool IsRootNode(DependencyNodeData node)
        {
            return node.Kind == DependencyNodeKind.SceneObject
                && !graph.GetIncomingEdges(node.Id).Any(edge => edge.ReferenceKind == DependencyReferenceKind.Hierarchy);
        }

        private bool ShouldCollapseNode(DependencyNodeData node, string nodeId, int depth)
        {
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

            MarkNearestVisibleMissingReferences();

            var occupiedRects = previousNodeRects
                .Where(pair => manuallyMovedViewIds.Contains(pair.Key) && renderNodes.Any(node => node.ViewId == pair.Key))
                .Select(pair => Inflate(pair.Value, NodeCollisionPadding))
                .ToList();
            var maxX = CanvasPadding;
            var maxY = CanvasPadding;

            for (var i = 0; i < renderNodes.Count; i++)
            {
                var renderNode = renderNodes[i];
                renderNode.Size = CustomNodeView.GetPreferredSize(renderNode.Node, renderNode.SizeScale);
                var layoutPosition = new Vector2(CanvasPadding + renderNode.Depth * ColumnSpacing, CanvasPadding + renderNode.Row);
                var position = layoutPosition;

                if (manuallyMovedViewIds.Contains(renderNode.ViewId)
                    && previousNodeRects.TryGetValue(renderNode.ViewId, out var previousRect))
                {
                    position = previousRect.position;
                }
                else if (renderNode.Parent != null && nodeRects.TryGetValue(renderNode.Parent.ViewId, out var parentRect))
                {
                    var preferredPosition = new Vector2(
                        parentRect.xMax + ChildHorizontalOffset,
                        CanvasPadding + renderNode.Row);
                    position = FindOpenPosition(preferredPosition, renderNode.Size, occupiedRects);
                }
                else
                {
                    position = FindOpenPosition(layoutPosition, renderNode.Size, occupiedRects);
                }

                var nodeView = new CustomNodeView(
                    renderNode.ViewId,
                    renderNode.Node,
                    renderNode.HasHiddenChildren,
                    renderNode.CanToggleChildren,
                    renderNode.IsExpanded,
                    renderNode.HasMenuChildren,
                    renderNode.IsMenuExpanded,
                    renderNode.Parent != null,
                    renderNode.HasPropagatedMissingReference,
                    renderNode.SizeScale,
                    () => zoom);
                nodeView.SetGraphPosition(position);
                nodeView.NodeSelected += HandleNodeSelected;
                nodeView.NodeMoved += HandleNodeMoved;
                nodeView.ToggleRequested += HandleNodeToggleRequested;
                nodeView.MenuToggleRequested += HandleMenuToggleRequested;
                nodeView.ParentJumpRequested += HandleParentJumpRequested;
                if (searchMatchNodeIds.Contains(renderNode.NodeId))
                {
                    nodeView.AddToClassList("dependency-node--search-match");
                }

                if (IsCurrentSearchNode(renderNode.NodeId))
                {
                    nodeView.AddToClassList("dependency-node--search-current");
                }

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
                nextRow += node.Size.y + GetNodeGap(node);
            }
            else
            {
                node.Row = (childRows[0] + childRows[childRows.Count - 1]) * 0.5f;
            }

            return node.Row;
        }

        private static float GetNodeGap(RenderNode node)
        {
            return node != null && node.Depth >= 3 ? DeepNodeGap : ShallowNodeGap;
        }

        private void CollectRenderNodes(RenderNode root)
        {
            renderNodes.Add(root);
            for (var i = 0; i < root.Children.Count; i++)
            {
                CollectRenderNodes(root.Children[i]);
            }
        }

        private void MarkNearestVisibleMissingReferences()
        {
            for (var i = 0; i < renderNodes.Count; i++)
            {
                var renderNode = renderNodes[i];
                if (renderNode.Node.HasMissingReferences || renderNode.Node.Kind == DependencyNodeKind.MissingReference)
                {
                    continue;
                }

                var visibleChildTargets = new HashSet<string>(renderNode.Children.Select(child => child.NodeId));
                var outgoingEdges = GetTreeOutgoingEdges(renderNode.NodeId);
                for (var edgeIndex = 0; edgeIndex < outgoingEdges.Count; edgeIndex++)
                {
                    var edge = outgoingEdges[edgeIndex];
                    if (visibleChildTargets.Contains(edge.TargetNodeId))
                    {
                        continue;
                    }

                    if (SubtreeContainsMissingReference(edge.TargetNodeId, new HashSet<string> { renderNode.NodeId }))
                    {
                        renderNode.HasPropagatedMissingReference = true;
                        break;
                    }
                }
            }
        }

        private bool SubtreeContainsMissingReference(string nodeId, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(nodeId) || !visited.Add(nodeId) || !graph.TryGetNode(nodeId, out var node))
            {
                return false;
            }

            if (node.Kind == DependencyNodeKind.MissingReference || node.HasMissingReferences)
            {
                return true;
            }

            foreach (var edge in GetTreeOutgoingEdges(nodeId))
            {
                if (SubtreeContainsMissingReference(edge.TargetNodeId, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshEdges()
        {
            edgeLayer.Clear();
            edgeRoutes.Clear();

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
                var targetRenderNode = renderNode;
                edgeView.SetCanvasSize(currentCanvasSize.x, currentCanvasSize.y);
                edgeView.SetEndpoints(sourceRect, targetRect, routeOffset, edgeIndex, total);
                edgeView.ChildJumpRequested += _ => FocusRenderNode(targetRenderNode, true);
                edgeRoutes.Add(new EdgeRoute(targetRenderNode, CreateEdgeRoute(sourceRect, targetRect, routeOffset, edgeIndex, total)));
                edgeLayer.Add(edgeView);
            }
        }

        private void AnimateLayoutTransition(
            IReadOnlyDictionary<string, Rect> previousRects,
            IReadOnlyDictionary<string, RenderSnapshot> previousSnapshots,
            IReadOnlyDictionary<string, Rect> finalRects)
        {
            var nodeAnimations = new List<NodeAnimation>();
            foreach (var pair in finalRects)
            {
                if (!nodeViews.TryGetValue(pair.Key, out var nodeView))
                {
                    continue;
                }

                var finalRect = pair.Value;
                var startRect = finalRect;
                if (previousRects.TryGetValue(pair.Key, out var previousRect))
                {
                    startRect = previousRect;
                }
                else
                {
                    var parentRect = FindCurrentParentRect(pair.Key, finalRects, previousRects);
                    if (parentRect.HasValue)
                    {
                        startRect = new Rect(
                            parentRect.Value.center.x - finalRect.width * 0.5f,
                            parentRect.Value.center.y - finalRect.height * 0.5f,
                            finalRect.width,
                            finalRect.height);
                    }
                }

                if ((startRect.position - finalRect.position).sqrMagnitude <= 0.5f)
                {
                    nodeView.SetGraphPosition(finalRect.position);
                    nodeRects[pair.Key] = finalRect;
                    continue;
                }

                nodeView.SetGraphPosition(startRect.position);
                nodeRects[pair.Key] = new Rect(startRect.position, finalRect.size);
                nodeAnimations.Add(new NodeAnimation(pair.Key, nodeView, startRect.position, finalRect.position));
            }

            var ghostAnimations = CreateGhostAnimations(previousRects, previousSnapshots, finalRects);
            if (nodeAnimations.Count == 0 && ghostAnimations.Count == 0)
            {
                return;
            }

            var startTime = Time.realtimeSinceStartup;
            activeAnimation = schedule.Execute(() =>
            {
                var t = Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / AnimationDurationSeconds);
                var eased = SmoothStep(t);

                for (var i = 0; i < nodeAnimations.Count; i++)
                {
                    var animation = nodeAnimations[i];
                    var position = Vector2.Lerp(animation.StartPosition, animation.EndPosition, eased);
                    animation.View.SetGraphPosition(position);
                    if (finalRects.TryGetValue(animation.ViewId, out var finalRect))
                    {
                        nodeRects[animation.ViewId] = new Rect(position, finalRect.size);
                    }
                }

                for (var i = 0; i < ghostAnimations.Count; i++)
                {
                    var animation = ghostAnimations[i];
                    var position = Vector2.Lerp(animation.StartRect.position, animation.EndPosition, eased);
                    animation.View.style.left = position.x;
                    animation.View.style.top = position.y;
                    animation.View.style.opacity = 1f - eased;
                }

                RefreshEdges();
                miniMap.MarkDirtyRepaint();

                if (t < 1f)
                {
                    return;
                }

                for (var i = 0; i < nodeAnimations.Count; i++)
                {
                    var animation = nodeAnimations[i];
                    animation.View.SetGraphPosition(animation.EndPosition);
                    if (finalRects.TryGetValue(animation.ViewId, out var finalRect))
                    {
                        nodeRects[animation.ViewId] = finalRect;
                    }
                }

                for (var i = 0; i < ghostAnimations.Count; i++)
                {
                    ghostAnimations[i].View.RemoveFromHierarchy();
                }

                activeAnimation?.Pause();
                activeAnimation = null;
                RefreshEdges();
                miniMap.MarkDirtyRepaint();
            }).Every(16);
        }

        private List<GhostAnimation> CreateGhostAnimations(
            IReadOnlyDictionary<string, Rect> previousRects,
            IReadOnlyDictionary<string, RenderSnapshot> previousSnapshots,
            IReadOnlyDictionary<string, Rect> finalRects)
        {
            var ghosts = new List<GhostAnimation>();
            foreach (var pair in previousRects)
            {
                if (finalRects.ContainsKey(pair.Key) || !previousSnapshots.TryGetValue(pair.Key, out var snapshot))
                {
                    continue;
                }

                var targetRect = FindVisibleAncestorRect(snapshot.ParentViewId, previousSnapshots, finalRects);
                var endPosition = targetRect.HasValue
                    ? targetRect.Value.center - pair.Value.size * 0.5f
                    : pair.Value.position;
                var ghost = new VisualElement();
                ghost.AddToClassList("dependency-node-ghost");
                ghost.style.position = Position.Absolute;
                ghost.style.left = pair.Value.x;
                ghost.style.top = pair.Value.y;
                ghost.style.width = pair.Value.width;
                ghost.style.height = pair.Value.height;
                animationLayer.Add(ghost);
                ghosts.Add(new GhostAnimation(ghost, pair.Value, endPosition));
            }

            return ghosts;
        }

        private Rect? FindCurrentParentRect(
            string viewId,
            IReadOnlyDictionary<string, Rect> finalRects,
            IReadOnlyDictionary<string, Rect> previousRects)
        {
            var renderNode = renderNodes.FirstOrDefault(node => node.ViewId == viewId);
            if (renderNode?.Parent == null)
            {
                return null;
            }

            if (finalRects.TryGetValue(renderNode.Parent.ViewId, out var finalParentRect))
            {
                return finalParentRect;
            }

            if (previousRects.TryGetValue(renderNode.Parent.ViewId, out var previousParentRect))
            {
                return previousParentRect;
            }

            return null;
        }

        private static Rect? FindVisibleAncestorRect(
            string parentViewId,
            IReadOnlyDictionary<string, RenderSnapshot> previousSnapshots,
            IReadOnlyDictionary<string, Rect> finalRects)
        {
            var current = parentViewId;
            while (!string.IsNullOrEmpty(current))
            {
                if (finalRects.TryGetValue(current, out var rect))
                {
                    return rect;
                }

                if (!previousSnapshots.TryGetValue(current, out var snapshot))
                {
                    return null;
                }

                current = snapshot.ParentViewId;
            }

            return null;
        }

        private void StopActiveAnimation()
        {
            if (activeAnimation == null)
            {
                return;
            }

            activeAnimation.Pause();
            activeAnimation = null;
        }

        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private List<DependencyEdgeData> GetTreeOutgoingEdges(string nodeId)
        {
            return graph.GetOutgoingEdges(nodeId)
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

        private bool TryFocusEdgeAt(Vector2 viewportPosition)
        {
            if (edgeRoutes.Count == 0)
            {
                return false;
            }

            var graphPosition = ViewportToGraph(viewportPosition, zoom);
            var pickDistance = Mathf.Max(6f, 10f / Mathf.Max(0.01f, zoom));
            EdgeRoute bestRoute = null;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < edgeRoutes.Count; i++)
            {
                var route = edgeRoutes[i];
                var distance = GetDistanceToRoute(graphPosition, route.Points);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestRoute = route;
                }
            }

            if (bestRoute == null || bestDistance > pickDistance)
            {
                return false;
            }

            FocusRenderNode(bestRoute.Target, true);
            return true;
        }

        private static RoutePoints CreateEdgeRoute(Rect sourceRect, Rect targetRect, float routeOffset, int slotIndex, int slotCount)
        {
            var sourceY = GetDistributedPortY(sourceRect, slotIndex, slotCount);
            var start = sourceRect.xMin <= targetRect.xMin
                ? new Vector2(sourceRect.xMax, sourceY)
                : new Vector2(sourceRect.xMin, sourceY);
            var targetY = ClampPortY(targetRect, targetRect.center.y - routeOffset * 0.35f);
            var end = sourceRect.xMin <= targetRect.xMin
                ? new Vector2(targetRect.xMin, targetY)
                : new Vector2(targetRect.xMax, targetY);
            var direction = sourceRect.xMin <= targetRect.xMin ? 1f : -1f;
            var controlDistance = Mathf.Clamp(Mathf.Abs(end.x - start.x) * 0.52f, 96f, 320f);
            var verticalBend = Mathf.Clamp(routeOffset * 0.32f, -80f, 80f);
            return new RoutePoints(
                start,
                start + new Vector2(controlDistance * direction, verticalBend),
                end - new Vector2(controlDistance * direction, verticalBend),
                end);
        }

        private static float GetDistanceToRoute(Vector2 point, RoutePoints route)
        {
            var bestDistance = float.MaxValue;
            var previous = route.Start;
            for (var i = 1; i <= 28; i++)
            {
                var current = EvaluateCubic(route, i / 28f);
                bestDistance = Mathf.Min(bestDistance, DistanceToSegment(point, previous, current));
                previous = current;
            }

            return bestDistance;
        }

        private static float GetDistributedPortY(Rect rect, int index, int count)
        {
            if (count <= 1)
            {
                return rect.center.y;
            }

            var usableTop = rect.yMin + 18f;
            var usableBottom = rect.yMax - 18f;
            var t = (index + 1f) / (count + 1f);
            return Mathf.Lerp(usableTop, usableBottom, t);
        }

        private static float ClampPortY(Rect rect, float y)
        {
            return Mathf.Clamp(y, rect.yMin + 14f, rect.yMax - 14f);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, start);
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            var projection = start + segment * t;
            return Vector2.Distance(point, projection);
        }

        private static Vector2 EvaluateCubic(RoutePoints points, float t)
        {
            var inverse = 1f - t;
            return inverse * inverse * inverse * points.Start
                + 3f * inverse * inverse * t * points.FirstTurn
                + 3f * inverse * t * t * points.SecondTurn
                + t * t * t * points.End;
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
            animationLayer.style.width = Mathf.Max(1f, width);
            animationLayer.style.height = Mathf.Max(1f, height);
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
            focusedViewId = null;
            NodeSelected?.Invoke(node);
        }

        private void HandleNodeToggleRequested(CustomNodeView nodeView)
        {
            if (nodeView == null || string.IsNullOrEmpty(nodeView.Data.Id))
            {
                return;
            }

            focusedNodeId = nodeView.Data.Id;
            focusedViewId = nodeView.ViewId;
            if (nodeView.IsExpanded)
            {
                CollapseViewNode(nodeView.ViewId);
            }
            else
            {
                ExpandViewNode(nodeView.ViewId);
            }

            EnsureViewVisible(nodeView.ViewId);
        }

        private void HandleMenuToggleRequested(CustomNodeView nodeView)
        {
            if (nodeView == null || string.IsNullOrEmpty(nodeView.ViewId))
            {
                return;
            }

            focusedNodeId = nodeView.Data.Id;
            focusedViewId = nodeView.ViewId;
            if (expandedMenuViewIds.Contains(nodeView.ViewId) || expandedMenuNodeIds.Contains(nodeView.Data.Id))
            {
                CollapseMenuViewNode(nodeView.ViewId);
            }
            else
            {
                ExpandMenuViewNode(nodeView.ViewId);
            }

            EnsureViewVisible(nodeView.ViewId);
        }

        private void HandleParentJumpRequested(CustomNodeView nodeView)
        {
            if (nodeView == null)
            {
                return;
            }

            var renderNode = renderNodes.FirstOrDefault(node => node.ViewId == nodeView.ViewId);
            if (renderNode?.Parent == null)
            {
                return;
            }

            FocusRenderNode(renderNode.Parent, true);
        }

        private void FocusRenderNode(RenderNode renderNode, bool centerView)
        {
            if (renderNode == null)
            {
                return;
            }

            focusedNodeId = renderNode.NodeId;
            focusedViewId = renderNode.ViewId;

            if (!centerView || !nodeRects.TryGetValue(renderNode.ViewId, out var targetRect))
            {
                FlashView(renderNode.ViewId, false);
                return;
            }

            var movedView = CenterViewOnIfNeeded(targetRect);
            FlashView(renderNode.ViewId, movedView);
        }

        private void HandleNodeMoved(CustomNodeView nodeView, Vector2 nextPosition)
        {
            if (nodeView == null || !nodeRects.ContainsKey(nodeView.ViewId))
            {
                return;
            }

            nextPosition = new Vector2(Mathf.Max(CanvasPadding, nextPosition.x), Mathf.Max(CanvasPadding, nextPosition.y));
            manuallyMovedViewIds.Add(nodeView.ViewId);
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

            if (evt.button == 0 && TryFocusEdgeAt(evt.localMousePosition))
            {
                evt.PreventDefault();
                evt.StopPropagation();
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

        private bool CenterViewOnIfNeeded(Rect graphRect)
        {
            if (IsGraphRectVisible(graphRect))
            {
                return false;
            }

            CenterViewOn(graphRect.center);
            return true;
        }

        private void EnsureViewVisible(string viewId)
        {
            if (string.IsNullOrEmpty(viewId) || !nodeRects.TryGetValue(viewId, out var rect))
            {
                return;
            }

            CenterViewOnIfNeeded(rect);
        }

        private Vector2 ViewportToGraph(Vector2 viewportPoint, float scale)
        {
            return (viewportPoint - pan) / Mathf.Max(0.0001f, scale);
        }

        private bool IsGraphRectVisible(Rect graphRect)
        {
            var viewportSize = GetViewportSize();
            var viewportTopLeft = ViewportToGraph(Vector2.zero, zoom);
            var viewportRect = new Rect(
                viewportTopLeft.x,
                viewportTopLeft.y,
                viewportSize.x / Mathf.Max(0.0001f, zoom),
                viewportSize.y / Mathf.Max(0.0001f, zoom));
            var marginX = Mathf.Min(80f, viewportRect.width * 0.12f);
            var marginY = Mathf.Min(60f, viewportRect.height * 0.12f);
            var safeRect = new Rect(
                viewportRect.xMin + marginX,
                viewportRect.yMin + marginY,
                Mathf.Max(1f, viewportRect.width - marginX * 2f),
                Mathf.Max(1f, viewportRect.height - marginY * 2f));
            return safeRect.Overlaps(graphRect);
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
                collapsedNodeIds.Remove(parentEdge.SourceNodeId);
                if (parentEdge.ReferenceKind == DependencyReferenceKind.SerializedProperty)
                {
                    expandedMenuNodeIds.Add(parentEdge.SourceNodeId);
                }

                current = parentEdge.SourceNodeId;
            }
        }

        private void ClearExplicitDescendantStates(string nodeId)
        {
            if (graph == null || string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            var visited = new HashSet<string> { nodeId };
            var stack = new Stack<string>();
            foreach (var edge in GetTreeOutgoingEdges(nodeId))
            {
                stack.Push(edge.TargetNodeId);
            }

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!visited.Add(current))
                {
                    continue;
                }

                expandedNodeIds.Remove(current);
                collapsedNodeIds.Remove(current);

                foreach (var edge in GetTreeOutgoingEdges(current))
                {
                    stack.Push(edge.TargetNodeId);
                }
            }
        }

        private void ExpandViewNode(string viewId)
        {
            if (string.IsNullOrEmpty(viewId) || graph == null)
            {
                return;
            }

            var changed = expandedViewIds.Add(viewId);
            changed |= collapsedViewIds.Remove(viewId);
            if (changed)
            {
                ClearExplicitDescendantViewStates(viewId);
                Render();
            }
        }

        private void CollapseViewNode(string viewId)
        {
            if (string.IsNullOrEmpty(viewId) || graph == null)
            {
                return;
            }

            var changed = collapsedViewIds.Add(viewId);
            changed |= expandedViewIds.Remove(viewId);
            if (changed)
            {
                ClearExplicitDescendantViewStates(viewId);
                Render();
            }
        }

        private void ExpandMenuViewNode(string viewId)
        {
            if (string.IsNullOrEmpty(viewId) || graph == null)
            {
                return;
            }

            if (expandedMenuViewIds.Add(viewId))
            {
                ClearExplicitDescendantViewStates(viewId);
                Render();
            }
        }

        private void CollapseMenuViewNode(string viewId)
        {
            if (string.IsNullOrEmpty(viewId) || graph == null)
            {
                return;
            }

            var changed = expandedMenuViewIds.Remove(viewId);
            var renderNode = renderNodes.FirstOrDefault(node => node.ViewId == viewId);
            if (renderNode != null)
            {
                changed |= expandedMenuNodeIds.Remove(renderNode.NodeId);
            }

            if (changed)
            {
                ClearExplicitDescendantViewStates(viewId);
                Render();
            }
        }

        private void ClearExplicitDescendantViewStates(string viewId)
        {
            if (string.IsNullOrEmpty(viewId))
            {
                return;
            }

            var descendantPrefix = viewId + "-";
            expandedViewIds.RemoveWhere(id => id.StartsWith(descendantPrefix, StringComparison.Ordinal));
            collapsedViewIds.RemoveWhere(id => id.StartsWith(descendantPrefix, StringComparison.Ordinal));
            expandedMenuViewIds.RemoveWhere(id => id.StartsWith(descendantPrefix, StringComparison.Ordinal));
        }

        private void FlashFocusedNode(bool delayUntilViewSettles)
        {
            if (!string.IsNullOrEmpty(focusedViewId))
            {
                FlashView(focusedViewId, delayUntilViewSettles);
                return;
            }

            var renderNode = renderNodes.FirstOrDefault(node => node.NodeId == focusedNodeId);
            if (renderNode != null)
            {
                FlashView(renderNode.ViewId, delayUntilViewSettles);
            }
        }

        private void FlashView(string viewId, bool delayUntilViewSettles)
        {
            if (delayUntilViewSettles)
            {
                schedule.Execute(() => FlashView(viewId, false)).ExecuteLater(34);
                return;
            }

            if (string.IsNullOrEmpty(viewId) || !nodeViews.TryGetValue(viewId, out var view))
            {
                return;
            }

            const float durationSeconds = 0.17f;
            var rect = view.GetGraphRect();
            var ring = new VisualElement();
            ring.AddToClassList("dependency-node-flash-ring");
            ring.pickingMode = PickingMode.Ignore;
            ring.style.left = -5f;
            ring.style.top = -5f;
            ring.style.width = rect.width + 10f;
            ring.style.height = rect.height + 10f;
            ring.style.opacity = 1f;
            view.Add(ring);

            var startTime = Time.realtimeSinceStartup;
            IVisualElementScheduledItem animation = null;
            animation = ring.schedule.Execute(() =>
            {
                var t = Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / durationSeconds);
                ring.style.opacity = 1f - SmoothStep(t);
                if (t < 1f)
                {
                    return;
                }

                animation?.Pause();
                ring.RemoveFromHierarchy();
            }).Every(16);
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

        private static bool MatchesSearchQuery(DependencyNodeData node, string query)
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

        private static bool MatchesSearchTerm(DependencyNodeData node, string term)
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

        private static bool MatchesMissingFilter(DependencyNodeData node, string value)
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

        private static string BuildSearchText(DependencyNodeData node)
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

                foreach (var edge in graph.GetIncomingEdges(current))
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
            return new SearchResultState(currentSearchResultIndex, searchResultNodeIds.Count);
        }

        public struct SearchResultState
        {
            public SearchResultState(int currentIndex, int total)
            {
                CurrentIndex = currentIndex;
                Total = total;
            }

            public int CurrentIndex { get; }
            public int Total { get; }
            public int DisplayIndex => CurrentIndex < 0 || Total <= 0 ? 0 : CurrentIndex + 1;
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

        private sealed class NodeAnimation
        {
            public NodeAnimation(string viewId, CustomNodeView view, Vector2 startPosition, Vector2 endPosition)
            {
                ViewId = viewId;
                View = view;
                StartPosition = startPosition;
                EndPosition = endPosition;
            }

            public string ViewId { get; }
            public CustomNodeView View { get; }
            public Vector2 StartPosition { get; }
            public Vector2 EndPosition { get; }
        }

        private sealed class GhostAnimation
        {
            public GhostAnimation(VisualElement view, Rect startRect, Vector2 endPosition)
            {
                View = view;
                StartRect = startRect;
                EndPosition = endPosition;
            }

            public VisualElement View { get; }
            public Rect StartRect { get; }
            public Vector2 EndPosition { get; }
        }

        private sealed class RenderSnapshot
        {
            public RenderSnapshot(RenderNode node)
            {
                ParentViewId = node.Parent == null ? string.Empty : node.Parent.ViewId;
            }

            public string ParentViewId { get; }
        }

        private sealed class EdgeRoute
        {
            public EdgeRoute(RenderNode target, RoutePoints points)
            {
                Target = target;
                Points = points;
            }

            public RenderNode Target { get; }
            public RoutePoints Points { get; }
        }

        private readonly struct RoutePoints
        {
            public RoutePoints(Vector2 start, Vector2 firstTurn, Vector2 secondTurn, Vector2 end)
            {
                Start = start;
                FirstTurn = firstTurn;
                SecondTurn = secondTurn;
                End = end;
            }

            public Vector2 Start { get; }
            public Vector2 FirstTurn { get; }
            public Vector2 SecondTurn { get; }
            public Vector2 End { get; }
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
                RenderNode parent,
                float sizeScale)
            {
                ViewId = viewId;
                NodeId = nodeId;
                Node = node;
                EdgeFromParent = edgeFromParent;
                Depth = depth;
                SiblingIndex = siblingIndex;
                Parent = parent;
                SizeScale = sizeScale;
            }

            public string ViewId { get; }
            public string NodeId { get; }
            public DependencyNodeData Node { get; }
            public DependencyEdgeData EdgeFromParent { get; }
            public int Depth { get; }
            public int SiblingIndex { get; }
            public RenderNode Parent { get; }
            public float SizeScale { get; }
            public List<RenderNode> Children { get; } = new List<RenderNode>();
            public Vector2 Size { get; set; }
            public float Row { get; set; }
            public bool CanToggleChildren { get; set; }
            public bool IsExpanded { get; set; }
            public bool HasHiddenChildren { get; set; }
            public bool HasMenuChildren { get; set; }
            public bool IsMenuExpanded { get; set; }
            public bool HasPropagatedMissingReference { get; set; }
        }

        private readonly struct NodeDepth
        {
            public NodeDepth(string nodeId, int depth)
            {
                NodeId = nodeId;
                Depth = depth;
            }

            public string NodeId { get; }
            public int Depth { get; }
        }
    }
}
