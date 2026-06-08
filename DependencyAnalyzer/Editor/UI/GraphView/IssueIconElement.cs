using System.Collections.Generic;
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
            var bounds = GetSquareBounds(rect, 0.04f);
            var center = bounds.center;
            var points = new[]
            {
                new Vector2(center.x, bounds.yMin + bounds.height * 0.03f),
                new Vector2(bounds.xMax - bounds.width * 0.02f, bounds.yMax - bounds.height * 0.03f),
                new Vector2(bounds.xMin + bounds.width * 0.02f, bounds.yMax - bounds.height * 0.03f)
            };

            painter.fillColor = new Color(1f, 0.72f, 0.08f, 1f);
            FillRoundedPolygon(painter, points, bounds.width * 0.13f, 5);
            DrawExclamation(
                painter,
                center,
                bounds.height,
                new Color(0.20f, 0.23f, 0.24f, 1f),
                0.22f,
                0.05f,
                0.26f,
                0.13f,
                0.085f);
        }

        private static void DrawError(Painter2D painter, Rect rect)
        {
            var bounds = GetSquareBounds(rect, 0.03f);
            var center = bounds.center;
            var cut = bounds.width * 0.23f;
            var points = new[]
            {
                new Vector2(bounds.xMin + cut, bounds.yMin),
                new Vector2(bounds.xMax - cut, bounds.yMin),
                new Vector2(bounds.xMax, bounds.yMin + cut),
                new Vector2(bounds.xMax, bounds.yMax - cut),
                new Vector2(bounds.xMax - cut, bounds.yMax),
                new Vector2(bounds.xMin + cut, bounds.yMax),
                new Vector2(bounds.xMin, bounds.yMax - cut),
                new Vector2(bounds.xMin, bounds.yMin + cut)
            };

            painter.fillColor = new Color(1f, 0.40f, 0.25f, 1f);
            FillRoundedPolygon(painter, points, bounds.width * 0.04f, 3);
            DrawExclamation(
                painter,
                center,
                bounds.height,
                new Color(0.20f, 0.23f, 0.24f, 1f),
                0.24f,
                0.07f,
                0.27f,
                0.13f,
                0.085f);
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
                0.24f,
                0.07f,
                0.27f,
                0.13f,
                0.085f);
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
            float dotRatio,
            float lineWidthRatio,
            float dotRadiusRatio)
        {
            var lineWidth = Mathf.Max(1.15f, size * lineWidthRatio);
            painter.strokeColor = color;
            painter.lineWidth = lineWidth;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            painter.MoveTo(new Vector2(center.x, center.y - size * topRatio));
            painter.LineTo(new Vector2(center.x, center.y + size * bottomRatio));
            painter.Stroke();

            painter.fillColor = color;
            FillRegularPolygon(
                painter,
                new Vector2(center.x, center.y + size * dotRatio),
                Mathf.Max(1.2f, size * dotRadiusRatio),
                8,
                -90f);
        }

        private static void FillRoundedPolygon(Painter2D painter, Vector2[] corners, float cornerDistance, int steps)
        {
            if (corners == null || corners.Length == 0)
            {
                return;
            }

            var points = new List<Vector2>(corners.Length * Mathf.Max(2, steps));
            for (var i = 0; i < corners.Length; i++)
            {
                var previous = corners[(i - 1 + corners.Length) % corners.Length];
                var current = corners[i];
                var next = corners[(i + 1) % corners.Length];
                var distanceToPrevious = Mathf.Min(cornerDistance, Vector2.Distance(current, previous) * 0.45f);
                var distanceToNext = Mathf.Min(cornerDistance, Vector2.Distance(current, next) * 0.45f);
                var start = current + (previous - current).normalized * distanceToPrevious;
                var end = current + (next - current).normalized * distanceToNext;

                for (var step = 0; step <= steps; step++)
                {
                    var t = step / (float)steps;
                    var a = Vector2.Lerp(start, current, t);
                    var b = Vector2.Lerp(current, end, t);
                    points.Add(Vector2.Lerp(a, b, t));
                }
            }

            FillPolygon(painter, points.ToArray());
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
