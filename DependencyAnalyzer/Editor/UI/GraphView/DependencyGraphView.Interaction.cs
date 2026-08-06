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

        private void HandleNodeSelected(DependencyNode node)
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

            if (!renderNodeByViewId.TryGetValue(nodeView.ViewId, out var renderNode) || renderNode.Parent == null)
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
                var parentEdge = GetPrimaryIncomingEdge(current);
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

        private DependencyEdge GetPrimaryIncomingEdge(string nodeId)
        {
            var incomingEdges = GetIncomingEdges(nodeId);
            DependencyEdge bestEdge = null;
            var bestPriority = int.MaxValue;
            for (var i = 0; i < incomingEdges.Count; i++)
            {
                var edge = incomingEdges[i];
                var priority = GetEdgeSortPriority(edge.ReferenceKind);
                if (priority >= bestPriority)
                {
                    continue;
                }

                bestPriority = priority;
                bestEdge = edge;
            }

            return bestEdge;
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
            if (renderNodeByViewId.TryGetValue(viewId, out var renderNode))
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

            RenderNode renderNode = null;
            if (!string.IsNullOrEmpty(focusedNodeId)
                && renderNodesByNodeId.TryGetValue(focusedNodeId, out var nodes)
                && nodes.Count > 0)
            {
                renderNode = nodes[0];
            }

            if (renderNode != null)
            {
                FlashView(renderNode.ViewId, delayUntilViewSettles);
            }
        }

        private static void AddSearchPulseHighlight(CustomNodeView view, bool isCurrent)
        {
            if (view == null)
            {
                return;
            }

            var nodeColor = IconUtility.GetNodeAccentColor(view.Data);
            var borderColor = new Color(nodeColor.r, nodeColor.g, nodeColor.b, 0.92f);
            var fillColor = new Color(nodeColor.r, nodeColor.g, nodeColor.b, 0.08f);
            var ring = new VisualElement();
            ring.AddToClassList("dependency-node-search-ring");
            ring.pickingMode = PickingMode.Ignore;
            SetHighlightRingBounds(view, ring, isCurrent ? 5f : 4f);
            var borderWidth = isCurrent ? 3f : 2f;
            ring.style.borderTopWidth = borderWidth;
            ring.style.borderRightWidth = borderWidth;
            ring.style.borderBottomWidth = borderWidth;
            ring.style.borderLeftWidth = borderWidth;
            ring.style.borderTopColor = borderColor;
            ring.style.borderRightColor = borderColor;
            ring.style.borderBottomColor = borderColor;
            ring.style.borderLeftColor = borderColor;
            ring.style.backgroundColor = fillColor;
            ring.style.opacity = 1f;
            view.Insert(0, ring);

            const float cycleSeconds = 1.35f;
            const float holdSeconds = 0.18f;
            var startTime = Time.realtimeSinceStartup;
            ring.schedule.Execute(() =>
            {
                var elapsed = Time.realtimeSinceStartup - startTime;
                var cycle = elapsed % cycleSeconds;
                var t = Mathf.Clamp01((cycle - holdSeconds) / (cycleSeconds - holdSeconds));
                ring.style.opacity = 1f - SmoothStep(t);
            }).Every(16);
        }

        private static void SetHighlightRingBounds(CustomNodeView view, VisualElement ring, float padding)
        {
            if (view == null || ring == null)
            {
                return;
            }

            var stackOffset = view.HiddenStackOffset;
            ring.style.left = -padding;
            ring.style.top = -padding;
            ring.style.right = -padding - stackOffset;
            ring.style.bottom = -padding - stackOffset;
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

            const float durationSeconds = 0.95f;
            const float holdSeconds = 0.16f;
            var nodeColor = IconUtility.GetNodeAccentColor(view.Data);
            var borderColor = new Color(nodeColor.r, nodeColor.g, nodeColor.b, 1f);
            var fillColor = new Color(nodeColor.r, nodeColor.g, nodeColor.b, 0.10f);
            var ring = new VisualElement();
            ring.AddToClassList("dependency-node-flash-ring");
            ring.pickingMode = PickingMode.Ignore;
            SetHighlightRingBounds(view, ring, 4f);
            ring.style.borderTopColor = borderColor;
            ring.style.borderRightColor = borderColor;
            ring.style.borderBottomColor = borderColor;
            ring.style.borderLeftColor = borderColor;
            ring.style.backgroundColor = fillColor;
            ring.style.opacity = 1f;
            view.Add(ring);

            var startTime = Time.realtimeSinceStartup;
            IVisualElementScheduledItem animation = null;
            animation = ring.schedule.Execute(() =>
            {
                var elapsed = Time.realtimeSinceStartup - startTime;
                var t = Mathf.Clamp01((elapsed - holdSeconds) / (durationSeconds - holdSeconds));
                ring.style.opacity = 1f - SmoothStep(t);
                if (t < 1f)
                {
                    return;
                }

                animation?.Pause();
                ring.RemoveFromHierarchy();
            }).Every(16);
        }
    }
}
