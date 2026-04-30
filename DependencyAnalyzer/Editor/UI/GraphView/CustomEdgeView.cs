using DependencyAnalyzer.Editor.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    public sealed class CustomEdgeView : VisualElement
    {
        private readonly Label memberLabel;
        private Rect sourceRect;
        private Rect targetRect;
        private float routeOffset;

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

            memberLabel = new Label(Shorten(edgeData.MemberName));
            memberLabel.tooltip = BuildTooltip(edgeData);
            memberLabel.pickingMode = PickingMode.Ignore;
            memberLabel.AddToClassList("dependency-edge-label");
            Add(memberLabel);

            generateVisualContent += DrawEdge;
        }

        public DependencyEdgeData EdgeData { get; }

        public void SetCanvasSize(float width, float height)
        {
            style.width = Mathf.Max(1f, width);
            style.height = Mathf.Max(1f, height);
        }

        public void SetEndpoints(Rect source, Rect target, float offset)
        {
            sourceRect = source;
            targetRect = target;
            routeOffset = offset;
            UpdateLabelPosition();
            MarkDirtyRepaint();
        }

        private void DrawEdge(MeshGenerationContext context)
        {
            var start = GetSourcePoint();
            var end = GetTargetPoint();
            var tangent = Mathf.Max(80f, Mathf.Abs(end.x - start.x) * 0.45f);
            var controlYOffset = routeOffset * 2f;

            var painter = context.painter2D;
            painter.strokeColor = GetColor(EdgeData.ReferenceKind);
            painter.lineWidth = EdgeData.PointsToMissingReference ? 3f : 2f;
            painter.BeginPath();
            painter.MoveTo(start);
            painter.BezierCurveTo(
                new Vector2(start.x + tangent, start.y + controlYOffset),
                new Vector2(end.x - tangent, end.y + controlYOffset),
                end);
            painter.Stroke();
        }

        private Vector2 GetSourcePoint()
        {
            if (sourceRect.xMin <= targetRect.xMin)
            {
                return new Vector2(sourceRect.xMax, sourceRect.center.y + routeOffset);
            }

            return new Vector2(sourceRect.xMin, sourceRect.center.y + routeOffset);
        }

        private Vector2 GetTargetPoint()
        {
            if (sourceRect.xMin <= targetRect.xMin)
            {
                return new Vector2(targetRect.xMin, targetRect.center.y + routeOffset * 0.35f);
            }

            return new Vector2(targetRect.xMax, targetRect.center.y + routeOffset * 0.35f);
        }

        private void UpdateLabelPosition()
        {
            var center = Vector2.Lerp(GetSourcePoint(), GetTargetPoint(), 0.5f);
            memberLabel.style.left = Mathf.Max(0f, center.x - 84f);
            memberLabel.style.top = Mathf.Max(0f, center.y + routeOffset - 10f);
        }

        private static Color GetColor(DependencyReferenceKind kind)
        {
            switch (kind)
            {
                case DependencyReferenceKind.Hierarchy:
                    return new Color(0.49f, 0.71f, 0.86f, 0.85f);
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

        private static string Shorten(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= 34 ? value : value.Substring(0, 31) + "...";
        }
    }
}
