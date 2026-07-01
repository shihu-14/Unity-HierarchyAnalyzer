using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.Controls
{
    internal sealed class SearchIconElement : VisualElement
    {
        public SearchIconElement()
        {
            pickingMode = PickingMode.Position;
            generateVisualContent += DrawSearchIcon;
        }

        private void DrawSearchIcon(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var painter = context.painter2D;
            painter.strokeColor = Color.white;
            painter.lineWidth = 1.15f;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            var center = new Vector2(rect.x + rect.width * 0.43f, rect.y + rect.height * 0.42f);
            var radius = Mathf.Min(rect.width, rect.height) * 0.25f;
            const int segments = 36;
            painter.BeginPath();
            for (var i = 0; i <= segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (i == 0)
                {
                    painter.MoveTo(point);
                }
                else
                {
                    painter.LineTo(point);
                }
            }

            painter.Stroke();

            var handleStart = center + new Vector2(radius * 0.68f, radius * 0.68f);
            var handleEnd = new Vector2(rect.x + rect.width * 0.78f, rect.y + rect.height * 0.78f);
            painter.BeginPath();
            painter.MoveTo(handleStart);
            painter.LineTo(handleEnd);
            painter.Stroke();
        }
    }
}
