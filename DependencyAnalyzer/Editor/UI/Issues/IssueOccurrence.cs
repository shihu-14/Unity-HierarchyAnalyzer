using DependencyAnalyzer.Editor.Core;
using UnityEngine;

namespace DependencyAnalyzer.Editor.UI.Issues
{
    internal enum IssueOrigin
    {
        Analyzer,
        Console
    }

    internal sealed class IssueOccurrence
    {
        public IssueOccurrence(
            string groupCategory,
            string normalizedMessage,
            string groupTitle,
            string detail,
            DependencyScanIssueSeverity severity,
            string targetNodeId,
            Texture nodeIcon,
            Color nodeColor,
            Texture groupIcon,
            IssueOrigin origin,
            IssueLocation location,
            int reportedOccurrenceCount)
        {
            GroupCategory = groupCategory ?? string.Empty;
            NormalizedMessage = normalizedMessage ?? string.Empty;
            GroupTitle = string.IsNullOrEmpty(groupTitle) ? "Issue" : groupTitle;
            Detail = detail ?? string.Empty;
            Severity = severity;
            TargetNodeId = targetNodeId ?? string.Empty;
            NodeIcon = nodeIcon;
            NodeColor = nodeColor;
            GroupIcon = groupIcon;
            Origin = origin;
            Location = location ?? new IssueLocation(null, "No related node");
            ReportedOccurrenceCount = reportedOccurrenceCount < 1 ? 1 : reportedOccurrenceCount;
        }

        public string GroupCategory { get; }
        public string NormalizedMessage { get; }
        public string GroupTitle { get; }
        public string Detail { get; }
        public DependencyScanIssueSeverity Severity { get; }
        public string TargetNodeId { get; }
        public bool HasRelatedNode => !string.IsNullOrEmpty(TargetNodeId);
        public Texture NodeIcon { get; }
        public Color NodeColor { get; }
        public Texture GroupIcon { get; }
        public IssueOrigin Origin { get; }
        public IssueLocation Location { get; }
        public int ReportedOccurrenceCount { get; }
    }
}
