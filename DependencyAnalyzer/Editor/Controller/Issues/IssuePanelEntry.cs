using DependencyAnalyzer.Editor.Core;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Controller.Issues
{
    internal sealed class IssuePanelEntry
    {
        public IssuePanelEntry(
            string title,
            string detail,
            DependencyScanIssueSeverity severity,
            string targetNodeId,
            Texture nodeIcon,
            Color nodeColor)
        {
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            Severity = severity;
            TargetNodeId = targetNodeId ?? string.Empty;
            NodeIcon = nodeIcon;
            NodeColor = nodeColor;
        }

        public string Title { get; }
        public string Detail { get; }
        public DependencyScanIssueSeverity Severity { get; }
        public string TargetNodeId { get; }
        public Texture NodeIcon { get; }
        public Color NodeColor { get; }
    }
}
