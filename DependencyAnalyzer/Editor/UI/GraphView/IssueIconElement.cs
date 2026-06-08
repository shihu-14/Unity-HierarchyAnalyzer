using DependencyAnalyzer.Editor.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    internal sealed class IssueIconElement : VisualElement
    {
        private readonly DependencyScanIssueSeverity severity;

        public IssueIconElement(DependencyScanIssueSeverity severity)
        {
            this.severity = severity;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawIcon;
        }

        private void DrawIcon(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            switch (severity)
            {
                case DependencyScanIssueSeverity.Error:
                    DrawError(context.painter2D, rect);
                    break;
                case DependencyScanIssueSeverity.Info:
                    DrawInfo(context.painter2D, rect);
                    break;
                default:
                    DrawWarning(context.painter2D, rect);
                    break;
            }
        }

        private static void DrawWarning(Painter2D painter, Rect rect)
        {
            var bounds = GetSquareBounds(rect, 0.09f);
            var center = bounds.center;
            var points = new[]
            {
                new Vector2(center.x, bounds.yMin),
                new Vector2(bounds.xMax, bounds.yMax),
                new Vector2(bounds.xMin, bounds.yMax)
            };

            painter.fillColor = new Color(1f, 0.73f, 0.10f, 1f);
            FillPolygon(painter, points);
            DrawExclamation(
                painter,
                center,
                bounds.height,
                new Color(0.22f, 0.17f, 0.08f, 1f),
                0.28f,
                0.58f,
                0.76f);
        }

        private static void DrawError(Painter2D painter, Rect rect)
        {
            var bounds = GetSquareBounds(rect, 0.07f);
            var center = bounds.center;
            var radius = bounds.width * 0.5f;
            var points = new Vector2[8];
            for (var i = 0; i < points.Length; i++)
            {
                var angle = Mathf.Deg2Rad * (22.5f + i * 45f);
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            painter.fillColor = new Color(1f, 0.38f, 0.24f, 1f);
            FillPolygon(painter, points);
            DrawExclamation(
                painter,
                center,
                bounds.height,
                Color.white,
                0.25f,
                0.57f,
                0.76f);
        }

        private static void DrawInfo(Painter2D painter, Rect rect)
        {
            var bounds = GetSquareBounds(rect, 0.08f);
            var center = bounds.center;
            var radius = bounds.width * 0.5f;
            var points = new Vector2[16];
            for (var i = 0; i < points.Length; i++)
            {
                var angle = Mathf.Deg2Rad * (-90f + i * 360f / points.Length);
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            painter.fillColor = new Color(0.38f, 0.68f, 1f, 1f);
            FillPolygon(painter, points);
            DrawExclamation(
                painter,
                center,
                bounds.height,
                Color.white,
                0.34f,
                0.61f,
                0.25f);
        }

        private static Rect GetSquareBounds(Rect rect, float insetRatio)
        {
            var size = Mathf.Min(rect.width, rect.height);
            var inset = size * insetRatio;
            size -= inset * 2f;
            return new Rect(
                rect.center.x - size * 0.5f,
                rect.center.y - size * 0.5f,
                size,
                size);
        }

        private static void DrawExclamation(
            Painter2D painter,
            Vector2 center,
            float size,
            Color color,
            float topRatio,
            float bottomRatio,
            float dotRatio)
        {
            var lineWidth = Mathf.Max(1.15f, size * 0.12f);
            painter.strokeColor = color;
            painter.lineWidth = lineWidth;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            painter.MoveTo(new Vector2(center.x, center.y - size * topRatio));
            painter.LineTo(new Vector2(center.x, center.y + size * bottomRatio * 0.18f));
            painter.Stroke();

            painter.fillColor = color;
            FillRegularPolygon(
                painter,
                new Vector2(center.x, center.y + size * dotRatio * 0.5f),
                Mathf.Max(1.2f, size * 0.085f),
                8,
                -90f);
        }

        private static void FillRegularPolygon(
            Painter2D painter,
            Vector2 center,
            float radius,
            int sides,
            float startAngle)
        {
            var points = new Vector2[sides];
            for (var i = 0; i < sides; i++)
            {
                var angle = Mathf.Deg2Rad * (startAngle + i * 360f / sides);
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            FillPolygon(painter, points);
        }

        private static void FillPolygon(Painter2D painter, Vector2[] points)
        {
            if (points == null || points.Length == 0)
            {
                return;
            }

            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (var i = 1; i < points.Length; i++)
            {
                painter.LineTo(points[i]);
            }

            painter.ClosePath();
            painter.Fill();
        }
    }
}
