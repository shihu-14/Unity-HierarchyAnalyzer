using System;
using System.Collections.Generic;
using DependencyAnalyzer.Editor.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    public sealed class CustomEdgeView : VisualElement
    {
        private Rect sourceRect;
        private Rect targetRect;
        private float routeOffset;
        private int sourceSlotIndex;
        private int sourceSlotCount = 1;
        private readonly Color edgeColor;

        public CustomEdgeView(DependencyEdgeData edgeData, Color childNodeColor)
        {
            EdgeData = edgeData;
            edgeColor = new Color(childNodeColor.r, childNodeColor.g, childNodeColor.b, edgeData.PointsToMissingReference ? 0.95f : 0.9f);
            pickingMode = PickingMode.Position;
            style.position = Position.Absolute;
            style.left = 0f;
            style.top = 0f;

            AddToClassList("dependency-edge");
            AddToClassList("dependency-edge--" + edgeData.ReferenceKind.ToString().ToLowerInvariant());
            tooltip = BuildTooltip(edgeData);

            generateVisualContent += DrawEdge;
            RegisterCallback<MouseDownEvent>(HandleMouseDown);
        }

        public DependencyEdgeData EdgeData { get; }
        public event Action<CustomEdgeView> ChildJumpRequested;

        public override bool ContainsPoint(Vector2 localPoint)
        {
            var points = GetRoutePoints();
            const float pickDistance = 7f;
            return GetDistanceToCurve(localPoint, points) <= pickDistance;
        }

        public void SetCanvasSize(float width, float height)
        {
            style.width = Mathf.Max(1f, width);
            style.height = Mathf.Max(1f, height);
        }

        public void SetEndpoints(Rect source, Rect target, float offset, int slotIndex, int slotCount)
        {
            sourceRect = source;
            targetRect = target;
            routeOffset = offset;
            sourceSlotIndex = Mathf.Max(0, slotIndex);
            sourceSlotCount = Mathf.Max(1, slotCount);
            MarkDirtyRepaint();
        }

        private void DrawEdge(MeshGenerationContext context)
        {
            var points = GetRoutePoints();

            var painter = context.painter2D;
            painter.strokeColor = edgeColor;
            painter.lineWidth = EdgeData.PointsToMissingReference ? 3f : 2f;
            if (IsDottedEdge(EdgeData.ReferenceKind))
            {
                DrawDottedCurve(painter, points);
                return;
            }

            painter.BeginPath();
            painter.MoveTo(points.Start);
            painter.BezierCurveTo(points.FirstTurn, points.SecondTurn, points.End);
            painter.Stroke();
        }

        private RoutePoints GetRoutePoints()
        {
            var start = GetSourcePoint();
            var end = GetTargetPoint();
            var direction = sourceRect.xMin <= targetRect.xMin ? 1f : -1f;
            var controlDistance = Mathf.Clamp(Mathf.Abs(end.x - start.x) * 0.52f, 96f, 320f);
            var verticalBend = Mathf.Clamp(routeOffset * 0.32f, -80f, 80f);
            return new RoutePoints(
                start,
                start + new Vector2(controlDistance * direction, verticalBend),
                end - new Vector2(controlDistance * direction, verticalBend),
                end);
        }

        private Vector2 GetSourcePoint()
        {
            var y = GetDistributedPortY(sourceRect, sourceSlotIndex, sourceSlotCount);
            if (sourceRect.xMin <= targetRect.xMin)
            {
                return new Vector2(sourceRect.xMax, y);
            }

            return new Vector2(sourceRect.xMin, y);
        }

        private Vector2 GetTargetPoint()
        {
            if (sourceRect.xMin <= targetRect.xMin)
            {
                return new Vector2(targetRect.xMin, ClampPortY(targetRect, targetRect.center.y - routeOffset * 0.35f));
            }

            return new Vector2(targetRect.xMax, ClampPortY(targetRect, targetRect.center.y - routeOffset * 0.35f));
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

        private static bool IsDottedEdge(DependencyReferenceKind kind)
        {
            return kind == DependencyReferenceKind.SerializedProperty
                || kind == DependencyReferenceKind.Issue;
        }

        private static void DrawDottedCurve(Painter2D painter, RoutePoints points)
        {
            var samples = SampleCurve(points, 36);
            for (var i = 0; i < samples.Count - 1; i += 2)
            {
                painter.BeginPath();
                painter.MoveTo(samples[i]);
                painter.LineTo(samples[i + 1]);
                painter.Stroke();
            }
        }

        private static string BuildTooltip(DependencyEdgeData edgeData)
        {
            return "Reference: " + edgeData.MemberName
                + "\nKind: " + edgeData.ReferenceKind
                + "\nSource: " + edgeData.SourceNodeId
                + "\nTarget: " + edgeData.TargetNodeId;
        }

        private void HandleMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            ChildJumpRequested?.Invoke(this);
            evt.StopPropagation();
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

        private static float GetDistanceToCurve(Vector2 point, RoutePoints points)
        {
            var samples = SampleCurve(points, 28);
            var bestDistance = float.MaxValue;
            for (var i = 0; i < samples.Count - 1; i++)
            {
                bestDistance = Mathf.Min(bestDistance, DistanceToSegment(point, samples[i], samples[i + 1]));
            }

            return bestDistance;
        }

        private static List<Vector2> SampleCurve(RoutePoints points, int segments)
        {
            var samples = new List<Vector2>(segments + 1);
            for (var i = 0; i <= segments; i++)
            {
                samples.Add(EvaluateCubic(points, i / (float)segments));
            }

            return samples;
        }

        private static Vector2 EvaluateCubic(RoutePoints points, float t)
        {
            var inverse = 1f - t;
            return inverse * inverse * inverse * points.Start
                + 3f * inverse * inverse * t * points.FirstTurn
                + 3f * inverse * t * t * points.SecondTurn
                + t * t * t * points.End;
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
    }
}
