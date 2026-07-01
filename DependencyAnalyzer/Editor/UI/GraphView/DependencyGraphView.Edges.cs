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

        private void RefreshEdges()
        {
            edgeLayer.Clear();
            edgeRoutes.Clear();

            var visibleEdges = new List<RenderNode>();
            var totalBySource = new Dictionary<string, int>();
            for (var i = 0; i < renderNodes.Count; i++)
            {
                var node = renderNodes[i];
                if (node.Parent == null
                    || node.EdgeFromParent == null
                    || !nodeRects.ContainsKey(node.Parent.ViewId)
                    || !nodeRects.ContainsKey(node.ViewId))
                {
                    continue;
                }

                visibleEdges.Add(node);
                totalBySource.TryGetValue(node.Parent.ViewId, out var total);
                totalBySource[node.Parent.ViewId] = total + 1;
            }

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
                var visualSourceRect = GetEdgeSourceRect(renderNode.Parent, sourceRect);
                var edgeView = new CustomEdgeView(
                    renderNode.EdgeFromParent,
                    IconUtility.GetNodeAccentColor(renderNode.Parent.Node),
                    IconUtility.GetNodeAccentColor(renderNode.Node));
                var targetRenderNode = renderNode;
                edgeView.SetCanvasSize(currentCanvasSize.x, currentCanvasSize.y);
                edgeView.SetEndpoints(visualSourceRect, targetRect, routeOffset, edgeIndex, total);
                edgeView.ChildJumpRequested += _ => FocusRenderNode(targetRenderNode, true);
                edgeRoutes.Add(new EdgeRoute(targetRenderNode, CreateEdgeRoute(visualSourceRect, targetRect, routeOffset, edgeIndex, total)));
                edgeLayer.Add(edgeView);
            }
        }

        private static Rect GetEdgeSourceRect(RenderNode renderNode, Rect sourceRect)
        {
            if (renderNode == null || !renderNode.HasHiddenChildren)
            {
                return sourceRect;
            }

            var stackOffset = CustomNodeView.GetHiddenStackOffset(renderNode.SizeScale);
            return new Rect(
                sourceRect.x + stackOffset,
                sourceRect.y + stackOffset,
                sourceRect.width,
                sourceRect.height);
        }

        private List<DependencyEdgeData> GetTreeOutgoingEdges(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) && treeOutgoingEdgesByNodeId.TryGetValue(nodeId, out var edges)
                ? edges
                : EmptyEdges;
        }

        private List<DependencyEdgeData> GetRegularTreeOutgoingEdges(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) && regularTreeOutgoingEdgesByNodeId.TryGetValue(nodeId, out var edges)
                ? edges
                : EmptyEdges;
        }

        private List<DependencyEdgeData> GetMenuTreeOutgoingEdges(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) && menuTreeOutgoingEdgesByNodeId.TryGetValue(nodeId, out var edges)
                ? edges
                : EmptyEdges;
        }

        private List<DependencyEdgeData> GetOutgoingEdges(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) && outgoingEdgesByNodeId.TryGetValue(nodeId, out var edges)
                ? edges
                : EmptyEdges;
        }

        private List<DependencyEdgeData> GetIncomingEdges(string nodeId)
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
