using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    public sealed partial class DependencyGraphView : VisualElement
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
        private const float AnimationDurationSeconds = 0.3f;
        private const int MaxAnimatedLayoutNodeCount = 600;
        private const int MaxAnimatedLayoutNodeDelta = 400;
        private const int MaxSearchSuggestions = int.MaxValue;

        private readonly VisualElement contentLayer;
        private readonly VisualElement edgeLayer;
        private readonly VisualElement animationLayer;
        private readonly VisualElement nodeLayer;
        private readonly VisualElement miniMap;
        private readonly Label emptyStateLabel;
        private readonly Dictionary<string, CustomNodeView> nodeViews = new Dictionary<string, CustomNodeView>();
        private readonly Dictionary<string, Rect> nodeRects = new Dictionary<string, Rect>();
        private readonly Dictionary<string, RenderNode> renderNodeByViewId = new Dictionary<string, RenderNode>();
        private readonly Dictionary<string, List<RenderNode>> renderNodesByNodeId = new Dictionary<string, List<RenderNode>>();
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
        private readonly Dictionary<string, List<DependencyEdge>> outgoingEdgesByNodeId = new Dictionary<string, List<DependencyEdge>>();
        private readonly Dictionary<string, List<DependencyEdge>> incomingEdgesByNodeId = new Dictionary<string, List<DependencyEdge>>();
        private readonly Dictionary<string, List<DependencyEdge>> treeOutgoingEdgesByNodeId = new Dictionary<string, List<DependencyEdge>>();
        private readonly Dictionary<string, List<DependencyEdge>> regularTreeOutgoingEdgesByNodeId = new Dictionary<string, List<DependencyEdge>>();
        private readonly Dictionary<string, List<DependencyEdge>> menuTreeOutgoingEdgesByNodeId = new Dictionary<string, List<DependencyEdge>>();
        private readonly Dictionary<int, DependencyNode> nodeByInstanceId = new Dictionary<int, DependencyNode>();
        private readonly Dictionary<string, bool> missingReferenceSubtreeCache = new Dictionary<string, bool>();
        private readonly Dictionary<string, SubtreeIssueState> issueSubtreeCache = new Dictionary<string, SubtreeIssueState>();
        private readonly Dictionary<string, int> minimumRegularDepths = new Dictionary<string, int>();
        private readonly List<DependencyNode> rootNodes = new List<DependencyNode>();
        private static readonly List<DependencyEdge> EmptyEdges = new List<DependencyEdge>();

        private DependencyGraph graph;
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

        public event Action<DependencyNode> NodeSelected;

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

        public void Populate(DependencyGraph graphData, int depth)
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
            RebuildGraphCaches();
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

            if (!nodeByInstanceId.TryGetValue(instanceId, out var node))
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

            var targetRect = Rect.zero;
            if (renderNodesByNodeId.TryGetValue(nodeId, out var visibleNodes))
            {
                for (var i = 0; i < visibleNodes.Count; i++)
                {
                    if (nodeRects.TryGetValue(visibleNodes[i].ViewId, out targetRect))
                    {
                        break;
                    }
                }
            }

            if (targetRect.width <= 0f || targetRect.height <= 0f)
            {
                return false;
            }

            var movedView = CenterViewOnIfNeeded(targetRect);
            FlashFocusedNode(movedView);
            return true;
        }

        public struct SearchResultState
        {
            public SearchResultState(int currentIndex, int total)
                : this(currentIndex, total, Array.Empty<SearchSuggestion>())
            {
            }

            public SearchResultState(int currentIndex, int total, IReadOnlyList<SearchSuggestion> suggestions)
            {
                CurrentIndex = currentIndex;
                Total = total;
                Suggestions = suggestions ?? Array.Empty<SearchSuggestion>();
            }

            public int CurrentIndex { get; }
            public int Total { get; }
            public IReadOnlyList<SearchSuggestion> Suggestions { get; }
            public int DisplayIndex => CurrentIndex < 0 || Total <= 0 ? 0 : CurrentIndex + 1;
        }

        public readonly struct SearchSuggestion
        {
            public SearchSuggestion(string nodeId, string displayName, string detail, string path)
            {
                NodeId = nodeId ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                Detail = detail ?? string.Empty;
                Path = path ?? string.Empty;
            }

            public string NodeId { get; }
            public string DisplayName { get; }
            public string Detail { get; }
            public string Path { get; }
        }

    }
}
