using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.Controls
{
    internal sealed class ChevronIcon : VisualElement
    {
        private readonly bool pointsUp;
        private readonly float verticalScale;

        public ChevronIcon(bool pointsUp, float verticalScale)
        {
            this.pointsUp = pointsUp;
            this.verticalScale = Mathf.Clamp(verticalScale, 0.25f, 1f);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawChevron;
        }

        private void DrawChevron(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var centerX = rect.center.x;
            var centerY = rect.center.y;
            var halfWidth = Mathf.Min(rect.width * 0.23f, 4.7f);
            var halfHeight = Mathf.Min(rect.height * 0.16f, 4f) * verticalScale;
            var left = new Vector2(centerX - halfWidth, pointsUp ? centerY + halfHeight : centerY - halfHeight);
            var peak = new Vector2(centerX, pointsUp ? centerY - halfHeight : centerY + halfHeight);
            var right = new Vector2(centerX + halfWidth, pointsUp ? centerY + halfHeight : centerY - halfHeight);

            var painter = context.painter2D;
            painter.strokeColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            painter.lineWidth = 3f;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            painter.MoveTo(left);
            painter.LineTo(peak);
            painter.LineTo(right);
            painter.Stroke();
        }
    }
}
