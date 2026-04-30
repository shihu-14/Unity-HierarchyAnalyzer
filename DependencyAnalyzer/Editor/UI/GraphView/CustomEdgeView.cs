using DependencyAnalyzer.Editor.Core;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using GraphEdge = UnityEditor.Experimental.GraphView.Edge;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    public sealed class CustomEdgeView : GraphEdge
    {
        private readonly Label memberLabel;

        public CustomEdgeView(DependencyEdgeData edgeData)
        {
            EdgeData = edgeData;
            AddToClassList("dependency-edge");
            AddToClassList("dependency-edge--" + edgeData.ReferenceKind.ToString().ToLowerInvariant());
            tooltip = BuildTooltip(edgeData);

            memberLabel = new Label(Shorten(edgeData.MemberName));
            memberLabel.AddToClassList("dependency-edge-label");
            memberLabel.pickingMode = PickingMode.Ignore;
            Add(memberLabel);

            RegisterCallback<GeometryChangedEvent>(_ => UpdateLabelPosition());
            schedule.Execute(UpdateLabelPosition).Every(250);
        }

        public DependencyEdgeData EdgeData { get; }

        private void UpdateLabelPosition()
        {
            if (output == null || input == null)
            {
                return;
            }

            var outputCenter = output.worldBound.center;
            var inputCenter = input.worldBound.center;
            var parentElement = parent;
            if (parentElement == null)
            {
                return;
            }

            var center = parentElement.WorldToLocal(Vector2.Lerp(outputCenter, inputCenter, 0.5f));
            memberLabel.style.left = center.x;
            memberLabel.style.top = center.y;
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

            return value.Length <= 32 ? value : value.Substring(0, 29) + "...";
        }
    }
}
