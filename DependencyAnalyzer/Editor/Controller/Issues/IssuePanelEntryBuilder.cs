using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Utils;

namespace DependencyAnalyzer.Editor.Controller.Issues
{
    internal static class IssuePanelEntryBuilder
    {
        public static int Count(DependencyGraphData graphData)
        {
            return Build(graphData).Count;
        }

        public static List<IssuePanelEntry> Build(DependencyGraphData graphData)
        {
            var entries = new List<IssuePanelEntry>();
            if (graphData == null)
            {
                return entries;
            }

            var missingNodes = graphData.Nodes
                .Where(node => node.Kind == DependencyNodeKind.MissingReference)
                .OrderBy(node => node.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase);
            foreach (var node in missingNodes)
            {
                entries.Add(new IssuePanelEntry(
                    "Missing Reference: " + IssueTextFormatter.FormatTitle(node.DisplayName),
                    IssueTextFormatter.FormatDetail(string.IsNullOrEmpty(node.Path) ? node.TypeName : node.Path),
                    DependencyScanIssueSeverity.Warning,
                    node.Id,
                    IconUtility.GetIcon(node),
                    IconUtility.GetNodeAccentColor(node)));
            }

            foreach (var issue in graphData.Issues)
            {
                if (!IsDisplayedSeverity(issue.Severity))
                {
                    continue;
                }

                var targetNodeId = IssueTargetResolver.FindIssueEntryTargetNodeId(graphData, issue);
                if (string.IsNullOrEmpty(targetNodeId)
                    || !graphData.TryGetNode(targetNodeId, out var targetNode))
                {
                    continue;
                }

                entries.Add(new IssuePanelEntry(
                    IssueTextFormatter.FormatTitle(targetNode.DisplayName),
                    IssueTextFormatter.FormatDetail(issue.Message),
                    issue.Severity,
                    targetNodeId,
                    IconUtility.GetIcon(targetNode),
                    IconUtility.GetNodeAccentColor(targetNode)));
            }

            return entries
                .OrderByDescending(entry => entry.Severity == DependencyScanIssueSeverity.Error)
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Detail, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string GetSeverityClass(DependencyScanIssueSeverity severity)
        {
            switch (severity)
            {
                case DependencyScanIssueSeverity.Error:
                    return "dependency-issue-row--error";
                case DependencyScanIssueSeverity.Warning:
                    return "dependency-issue-row--warning";
                default:
                    return "dependency-issue-row--info";
            }
        }

        private static bool IsDisplayedSeverity(DependencyScanIssueSeverity severity)
        {
            return severity == DependencyScanIssueSeverity.Error
                || severity == DependencyScanIssueSeverity.Warning;
        }
    }
}
