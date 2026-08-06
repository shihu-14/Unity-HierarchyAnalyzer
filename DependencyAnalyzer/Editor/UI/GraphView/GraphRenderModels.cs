using System.Collections.Generic;
using DependencyAnalyzer.Editor.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    internal readonly struct MiniMapMetrics
    {
        public MiniMapMetrics(Vector2 origin, float scale)
        {
            Origin = origin;
            Scale = scale;
        }

        public Vector2 Origin { get; }
        public float Scale { get; }
    }

    internal sealed class NodeAnimation
    {
        public NodeAnimation(string viewId, DependencyNodeView view, Vector2 startPosition, Vector2 endPosition, bool fadeIn)
        {
            ViewId = viewId;
            View = view;
            StartPosition = startPosition;
            EndPosition = endPosition;
            FadeIn = fadeIn;
        }

        public string ViewId { get; }
        public DependencyNodeView View { get; }
        public Vector2 StartPosition { get; }
        public Vector2 EndPosition { get; }
        public bool FadeIn { get; }
    }

    internal sealed class GhostAnimation
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

    internal sealed class RenderSnapshot
    {
        public RenderSnapshot(RenderNode node)
        {
            ParentViewId = node.Parent == null ? string.Empty : node.Parent.ViewId;
            Node = node.Node;
        }

        public string ParentViewId { get; }
        public DependencyNode Node { get; }
    }

    internal sealed class EdgeRoute
    {
        public EdgeRoute(RenderNode target, RoutePoints points)
        {
            Target = target;
            Points = points;
        }

        public RenderNode Target { get; }
        public RoutePoints Points { get; }
    }

    internal readonly struct RoutePoints
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

    internal sealed class RenderNode
    {
        public RenderNode(
            string viewId,
            string nodeId,
            DependencyNode node,
            DependencyEdge edgeFromParent,
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
        public DependencyNode Node { get; }
        public DependencyEdge EdgeFromParent { get; }
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
        public bool HasPropagatedIssue { get; private set; }
        public DependencyScanIssueSeverity PropagatedIssueSeverity { get; private set; } = DependencyScanIssueSeverity.Warning;
        public string PropagatedIssueMessage { get; private set; } = string.Empty;

        public void SetPropagatedIssue(SubtreeIssueState issue)
        {
            if (!issue.HasIssue)
            {
                return;
            }

            if (HasPropagatedIssue && !IsMoreSevere(issue.Severity, PropagatedIssueSeverity))
            {
                return;
            }

            HasPropagatedIssue = true;
            PropagatedIssueSeverity = issue.Severity;
            PropagatedIssueMessage = issue.Message;
        }

        private static bool IsMoreSevere(DependencyScanIssueSeverity candidate, DependencyScanIssueSeverity current)
        {
            return GetIssueSeverityRank(candidate) > GetIssueSeverityRank(current);
        }

        private static int GetIssueSeverityRank(DependencyScanIssueSeverity severity)
        {
            switch (severity)
            {
                case DependencyScanIssueSeverity.Error:
                    return 3;
                case DependencyScanIssueSeverity.Warning:
                    return 2;
                case DependencyScanIssueSeverity.Info:
                    return 1;
                default:
                    return 0;
            }
        }
    }

    internal readonly struct SubtreeIssueState
    {
        public static readonly SubtreeIssueState None = new SubtreeIssueState(false, DependencyScanIssueSeverity.Warning, string.Empty, string.Empty, string.Empty);

        public SubtreeIssueState(bool hasIssue, DependencyScanIssueSeverity severity, string message, string nodeId, string sourceNodeId)
        {
            HasIssue = hasIssue;
            Severity = severity;
            Message = message ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            SourceNodeId = sourceNodeId ?? string.Empty;
        }

        public bool HasIssue { get; }
        public DependencyScanIssueSeverity Severity { get; }
        public string Message { get; }
        public string NodeId { get; }
        public string SourceNodeId { get; }
    }

    internal readonly struct NodeDepth
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
