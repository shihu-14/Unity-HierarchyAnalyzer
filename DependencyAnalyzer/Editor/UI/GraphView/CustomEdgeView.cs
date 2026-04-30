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

        public CustomEdgeView(DependencyEdgeData edgeData)
        {
            EdgeData = edgeData;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0f;
            style.top = 0f;

            AddToClassList("dependency-edge");
            AddToClassList("dependency-edge--" + edgeData.ReferenceKind.ToString().ToLowerInvariant());
            tooltip = BuildTooltip(edgeData);

            generateVisualContent += DrawEdge;
        }

        public DependencyEdgeData EdgeData { get; }

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
            var start = GetSourcePoint();
            var end = GetTargetPoint();
            var direction = sourceRect.xMin <= targetRect.xMin ? 1f : -1f;
            var laneOffset = routeOffset * 1.35f;
            var midX = (start.x + end.x) * 0.5f + laneOffset * direction;
            var firstTurn = new Vector2(midX, start.y);
            var secondTurn = new Vector2(midX, end.y);

            var painter = context.painter2D;
            painter.strokeColor = GetColor(EdgeData.ReferenceKind);
            painter.lineWidth = EdgeData.PointsToMissingReference ? 3f : 2f;
            painter.BeginPath();
            painter.MoveTo(start);
            painter.LineTo(firstTurn);
            painter.LineTo(secondTurn);
            painter.LineTo(end);
            painter.Stroke();
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

        private static Color GetColor(DependencyReferenceKind kind)
        {
            switch (kind)
            {
                case DependencyReferenceKind.Hierarchy:
                    return new Color(0.49f, 0.71f, 0.86f, 0.85f);
                case DependencyReferenceKind.Component:
                    return new Color(0.62f, 0.83f, 0.52f, 0.9f);
                case DependencyReferenceKind.PrefabInstance:
                    return new Color(0.84f, 0.66f, 0.48f, 0.9f);
                case DependencyReferenceKind.SerializedProperty:
                    return new Color(0.56f, 0.78f, 0.64f, 0.9f);
                default:
                    return new Color(0.72f, 0.78f, 0.85f, 0.85f);
            }
        }

        private static string BuildTooltip(DependencyEdgeData edgeData)
        {
            return "Reference: " + edgeData.MemberName
                + "\nKind: " + edgeData.ReferenceKind
                + "\nSource: " + edgeData.SourceNodeId
                + "\nTarget: " + edgeData.TargetNodeId;
        }

    }
}
