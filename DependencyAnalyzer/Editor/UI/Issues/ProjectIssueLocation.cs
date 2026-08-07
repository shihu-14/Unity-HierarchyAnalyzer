using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DependencyAnalyzer.Editor.UI.Issues
{
    internal sealed class ProjectIssueLocation
    {
        private readonly List<string> parentSegments;

        public ProjectIssueLocation(
            IEnumerable<string> parentSegments,
            string label,
            string objectName,
            string targetNodeId,
            Color accentColor)
        {
            this.parentSegments = parentSegments == null
                ? new List<string>()
                : parentSegments
                    .Where(segment => !string.IsNullOrWhiteSpace(segment))
                    .Select(segment => segment.Trim())
                    .ToList();
            Label = string.IsNullOrWhiteSpace(label) ? "No related node" : label.Trim();
            ObjectName = string.IsNullOrWhiteSpace(objectName) ? "No related node" : objectName.Trim();
            TargetNodeId = targetNodeId ?? string.Empty;
            AccentColor = accentColor;
        }

        public IReadOnlyList<string> ParentSegments => parentSegments;
        public string Label { get; }
        public string ObjectName { get; }
        public string DisplayPath => "Path: " + string.Join("/", parentSegments.Concat(new[] { Label }));
        public string TargetNodeId { get; }
        public bool HasRelatedNode => !string.IsNullOrEmpty(TargetNodeId);
        public Color AccentColor { get; }
        public string SortKey => string.Join("/", parentSegments) + "\u001f" + Label;
    }
}
