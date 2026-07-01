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
    }
}
