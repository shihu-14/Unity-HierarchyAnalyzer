using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.Controls
{
    internal sealed class ChevronIcon : VisualElement
    {
        internal const float IssuePanelVerticalScale = 0.59f;

        private readonly bool pointsUp;
        private readonly float verticalScale;
        private readonly Color strokeColor;

        public ChevronIcon(bool pointsUp, float verticalScale)
            : this(pointsUp, verticalScale, new Color(0.72f, 0.72f, 0.72f, 1f))
        {
        }

        private ChevronIcon(bool pointsUp, float verticalScale, Color strokeColor)
        {
            this.pointsUp = pointsUp;
            this.verticalScale = Mathf.Clamp(verticalScale, 0.25f, 1f);
            this.strokeColor = strokeColor;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawChevron;
        }

        internal float VerticalScale => verticalScale;
        internal Color StrokeColor => strokeColor;
        internal static Color IssuePanelStrokeColor => new Color(0.64f, 0.64f, 0.64f, 0.90f);

        internal static ChevronIcon CreateIssuePanel(bool pointsUp)
        {
            return new ChevronIcon(pointsUp, IssuePanelVerticalScale, IssuePanelStrokeColor);
        }

        internal static void SetIssuePanelButtonIcon(Button button, bool pointsUp)
        {
            if (button == null)
            {
                return;
            }

            button.text = string.Empty;
            button.Clear();
            var icon = CreateIssuePanel(pointsUp);
            icon.StretchToParentSize();
            button.Add(icon);
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
            painter.strokeColor = strokeColor;
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
